using UnityEngine;

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
    private BucketDragController dragController;

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
        bucketVolume = bucket != null ? bucket.GetComponent<BucketVolume>() : null;
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
        Vector3 ropeEnd = ropeSystem.GetBucketPosition();
        if (dragController != null)
        {
            dragController.SetCurrentRopeEnd(ropeEnd);
        }

        // 3. Drive the bucket directly from the rope.
        float dt = Time.fixedDeltaTime;
        bucketVelocity = dt > 0f ? (ropeEnd - lastBucketPos) / dt : Vector3.zero;
        lastBucketPos = ropeEnd;

        // 4. Update bucket
        if (bucket != null)
        {
            if (bucketVolume != null)
            {
                bucketVolume.SetExternalVelocity(bucketVelocity);
            }

            // Orient bucket to follow rope direction
            Vector3 ropeVector = ropeEnd - anchor.position;
            float l = ropeVector.magnitude;
            if (l > 0.001f)
            {
                bucket.up = (anchor.position - ropeEnd).normalized;
            }

            // Position bucket at rope end with offset
            bucket.position = ropeEnd - bucket.TransformVector(bucketAttachmentLocalOffset);
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
    }

    void OnDestroy()
    {
        ropeSystem?.Dispose();
    }

    // Public properties
    public Vector3 BucketVelocity => bucketVelocity;
    public Vector3 BucketPosition => bucket != null ? bucket.position : Vector3.zero;
    public RopeSimulationGPU RopeSystem => ropeSystem;

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
