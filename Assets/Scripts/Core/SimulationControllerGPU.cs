using UnityEngine;

[DefaultExecutionOrder(-50)]
public class SimulationControllerGPU : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform anchor;

    [SerializeField]
    private Transform bucket;

    [SerializeField]
    private Vector3 bucketAttachmentLocalOffset = Vector3.up * 0.5f;

    [SerializeField]
    private RopeRenderer ropeRenderer;

    [SerializeField]
    private RopeTwistArrowRenderer twistArrowRenderer;

    [SerializeField]
    private BucketDragController dragController;

    [SerializeField]
    private PaintingCollision planeCollision;

    [Header("Bucket Twist")]
    [SerializeField]
    private float bucketTwistAngle;

    [Header("Rope Settings")]
    [SerializeField]
    private int pointCount = 25;

    [SerializeField]
    private float ropeLength = 1f;

    [SerializeField]
    private int constraintIterations = 20;

    [Header("Rope Physics")]
    [SerializeField]
    private float stiffness = 0.8f;
    
    [SerializeField]
    private float maxStretchMultiplier = 1.5f;
    
    [SerializeField]
    private float ropeDamping = 0.999f;

    [Header("SPH Settings")]
    // [SerializeField]
    // public float sphDt = 0.005f;
    
    [SerializeField]
    public SPHManager sphManager;

    [Header("Compute Shader")]
    [SerializeField]
    private ComputeShader ropeCompute;

    // GPU Rope System
    private RopeSimulationGPU ropeSystem;
    
    // State
    private Vector3 lastBucketPos;
    private Vector3 bucketVelocity;
    private BucketVolume bucketVolume;

    void Start()
    {
        // Initialize GPU rope system
        ropeSystem = GetComponent<RopeSimulationGPU>();
        if (ropeSystem == null)
            ropeSystem = gameObject.AddComponent<RopeSimulationGPU>();

        ropeSystem.ropeCompute = ropeCompute;
        ropeSystem.pointCount = pointCount;
        ropeSystem.ropeLength = ropeLength;
        ropeSystem.iterations = constraintIterations;
        ropeSystem.stiffness = stiffness;
        ropeSystem.maxStretchMultiplier = maxStretchMultiplier;
        ropeSystem.ropeDamping = ropeDamping;
        ropeSystem.anchorTransform = anchor;
        ropeSystem.bucketTransform = bucket;
        ropeSystem.readBackData = true;
        ropeSystem.simulateInUpdate = false;
        ropeSystem.visualizeRope = true;

        if (planeCollision == null && sphManager != null)
            planeCollision = sphManager.planeCollision;

        bucketVolume = bucket != null ? bucket.GetComponent<BucketVolume>() : null;
        ropeSystem.planeCollision = planeCollision;
        ropeSystem.bucketVolume = bucketVolume;
        ropeSystem.bucketAttachmentDistance = GetWorldBucketAttachmentDistance();

        if (dragController != null)
        {
            dragController.SetRopeLength(ropeLength);
        }
        
        // Manually initialize
        ropeSystem.Initialize();

        // Initialize bucket state
        lastBucketPos = ropeSystem.GetBucketPosition();
        if (dragController != null)
        {
            dragController.SetCurrentRopeEnd(lastBucketPos);
        }

        if (twistArrowRenderer == null)
            twistArrowRenderer = GetComponentInChildren<RopeTwistArrowRenderer>();
        if (twistArrowRenderer != null)
        {
            twistArrowRenderer.ropeSystem = ropeSystem;
            twistArrowRenderer.bucket = bucket;
            twistArrowRenderer.anchor = anchor;
        }

        // Orient bucket before SPHManager.Start() spawns particles in bucket local space.
        if (bucket != null && anchor != null)
            OrientBucketFromRope(ropeSystem.GetBucketPosition());
    }

    void FixedUpdate()
    {
        if (ropeSystem == null || !ropeSystem.IsInitialized)
            return;

        ropeSystem.stiffness = stiffness;
        ropeSystem.maxStretchMultiplier = maxStretchMultiplier;
        ropeSystem.ropeDamping = ropeDamping;
        if (dragController != null)
        {
            dragController.SetRopeLength(ropeLength);
        }

        // Attachment must match TransformVector (includes bucket lossy scale).
        ropeSystem.bucketAttachmentDistance = GetWorldBucketAttachmentDistance();

        // 1. Simulate rope on GPU
        bool isDragging = dragController != null && dragController.IsDragging;
        Vector3 draggedPos = dragController != null ? dragController.DraggedPosition : Vector3.zero;
        
        ropeSystem.Simulate(
            Time.fixedDeltaTime,
            anchor.position,
            isDragging,
            draggedPos
        );

        // 2. Get bucket position from rope
        Vector3 ropeEnd = SanitizePosition(ropeSystem.GetBucketPosition());
        if (dragController != null)
        {
            dragController.SetCurrentRopeEnd(ropeEnd);
        }

        // 3. Drive the bucket directly from the rope.
        float dt = Time.fixedDeltaTime;
        bucketVelocity = dt > 0f ? (ropeEnd - lastBucketPos) / dt : Vector3.zero;
        if (!IsFiniteVector(bucketVelocity))
            bucketVelocity = Vector3.zero;
        else
            bucketVelocity = Vector3.ClampMagnitude(bucketVelocity, 50f);
        lastBucketPos = ropeEnd;

        // 4. Update bucket
        if (bucket != null)
        {
            if (bucketVolume != null)
            {
                bucketVolume.SetExternalVelocity(bucketVelocity);
            }

            if (dragController != null)
                bucketTwistAngle += dragController.ConsumeTwistDelta();

            if (bucketVolume != null)
                bucketVolume.SetTwistAngleDegrees(bucketTwistAngle);

            OrientBucketFromRope(ropeEnd);

            // Position bucket at rope end with offset
            bucket.position = ropeEnd - bucket.TransformVector(bucketAttachmentLocalOffset);

            // Smoothed post-clamp from the visual hull (only near the plane, skip while dragging).
            if (!isDragging && ropeSystem.IsRopeEndNearPlaneContact(ropeEnd, anchor.position))
            {
                Vector3 correctedRopeEnd = ropeSystem.EnforceRopeEndFromBucketTransform(anchor.position, dt);
                if (IsFiniteVector(correctedRopeEnd) && (correctedRopeEnd - ropeEnd).sqrMagnitude > 1e-10f)
                {
                    ropeEnd = correctedRopeEnd;
                    lastBucketPos = ropeEnd;
                    bucket.position = ropeEnd - bucket.TransformVector(bucketAttachmentLocalOffset);
                    if (dragController != null)
                        dragController.SetCurrentRopeEnd(ropeEnd);
                }
            }
        }

        // 5. Run SPH simulation (optional - CPU version)
        RunSPHSimulation();
    }

    void RunSPHSimulation()
    {
        if (sphManager == null)
            return;

        float stepDt = Mathf.Min(Time.fixedDeltaTime, sphManager.maxTimestep) * sphManager.simulationSpeed;
        sphManager.SimulateGPU(stepDt);
        
    }

    void LateUpdate()
    {
        // Render rope
        if (ropeRenderer != null && ropeSystem != null && ropeSystem.Positions != null)
        {
            ropeRenderer.Render(ropeSystem.Positions);
        }

        if (twistArrowRenderer != null)
        {
            twistArrowRenderer.RenderArrow();
        }
    }

    void OnDestroy()
    {
        ropeSystem?.Dispose();
    }

    // Public properties
    public Vector3 BucketVelocity => bucketVelocity;
    public Vector3 BucketPosition => bucket != null ? bucket.position : Vector3.zero;
    public RopeSimulationGPU RopeSystem => ropeSystem;

    float GetWorldBucketAttachmentDistance()
    {
        if (bucket == null)
            return bucketAttachmentLocalOffset.magnitude;

        return bucket.TransformVector(bucketAttachmentLocalOffset).magnitude;
    }

    static bool IsFiniteVector(Vector3 v) =>
        float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

    static Vector3 SanitizePosition(Vector3 v) =>
        IsFiniteVector(v) ? v : Vector3.zero;

    void OrientBucketFromRope(Vector3 ropeEnd)
    {
        if (bucket == null || anchor == null)
            return;

        ropeEnd = SanitizePosition(ropeEnd);
        Vector3 ropeVector = ropeEnd - anchor.position;
        if (ropeVector.sqrMagnitude < 1e-6f)
            return;

        Vector3 up = (anchor.position - ropeEnd).normalized;

        // Same minimal up-alignment as the old bucket.up = ropeDirection (preserves forward).
        Quaternion alignUp = Quaternion.FromToRotation(bucket.up, up);
        bucket.rotation = alignUp * bucket.rotation;

        // Twist is an optional roll around the rope axis for pour direction / rim spill.
        if (Mathf.Abs(bucketTwistAngle) > 0.001f)
            bucket.rotation = Quaternion.AngleAxis(bucketTwistAngle, up) * bucket.rotation;
    }

    public void ApplyRopeSettings(int newPointCount, float newRopeLength,float newStiffness ,float newMaxStretchMultiplier, float newRopeDamping)
    {
        bool needsReinitialize = newPointCount != pointCount || !Mathf.Approximately(newRopeLength, ropeLength);

        pointCount = Mathf.Max(2, newPointCount);
        ropeLength = Mathf.Max(0.001f, newRopeLength);
        maxStretchMultiplier = Mathf.Max(1f, newMaxStretchMultiplier);
        ropeDamping = Mathf.Clamp01(newRopeDamping);
        stiffness = Mathf.Clamp01(newStiffness);

        if (dragController != null)
        {
            dragController.SetRopeLength(ropeLength);
        }

        if (ropeSystem == null)
            return;

        ropeSystem.pointCount = pointCount;
        ropeSystem.ropeLength = ropeLength;
        ropeSystem.maxStretchMultiplier = maxStretchMultiplier;
        ropeSystem.ropeDamping = ropeDamping;

        if (needsReinitialize && ropeSystem.IsInitialized)
        {
            ropeSystem.Dispose();
            ropeSystem.Initialize();
            lastBucketPos = ropeSystem.GetBucketPosition();
        }
    }
}
