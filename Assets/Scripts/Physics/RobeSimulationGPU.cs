using UnityEngine;
using System;

public class RopeSimulationGPU : MonoBehaviour, IDisposable
{
    [Header("Compute Shader")]
    public ComputeShader ropeCompute;
    
    [Header("Rope Parameters")]
    public int pointCount = 20;
    public float ropeLength = 5f;
    public int iterations = 8;
    public float stiffness = 0.8f;
    public float maxStretchMultiplier = 1.5f;
    public float ropeDamping = 0.999f;
    public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    
    [Header("References")]
    public Transform anchorTransform;
    public Transform bucketTransform;
    public PaintingCollision planeCollision;
    public BucketVolume bucketVolume;
    public float bucketAttachmentDistance = 0.5f;
    
    [Header("Debug")]
    public bool visualizeRope = true;
    public bool readBackData = true;
    public bool simulateInUpdate = false;

    // Buffers
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer previousPositionsBuffer;
    private ComputeBuffer inverseMassesBuffer;
    
    // Kernels
    private int integrateKernel;
    private int solveConstraintsKernel;
    private int pendulumKernel;
    private ComputeShader bucketPlaneCollisionCompute;
    private int bucketPlaneKernel;
    private int ropePlaneKernel;
    
    // Data
    private Vector3[] positions;
    private Vector3[] previousPositions;
    private float[] inverseMasses;
    
    // Public properties
    public Vector3[] Positions => positions;
    public Vector3[] PreviousPositions => previousPositions;
    public int PointCount => pointCount;
    public bool IsInitialized => positionsBuffer != null && positionsBuffer.IsValid();
    
    // State for pendulum
    private float previousTheta = 0f;
    private float previousPhi = 0f;
    
    public BucketContactMode LastContactMode { get; private set; } = BucketContactMode.None;
    
    [Tooltip("Push interior rope points above the plane. Off avoids stiff/rope weirdness near the surface.")]
    public bool resolveRopeSegmentPlaneCollision = false;

    [Header("Plane Collision")]
    [Tooltip("Extra constraint passes after plane contact. Keep low for performance.")]
    [Range(0, 1)]
    public int planeContactConstraintPasses = 0;
    [Tooltip("Skip GPU plane collision when the bucket hull is farther than this above the plane.")]
    [Min(0.05f)]
    public float planeCollisionStartDistance = 0.35f;

    // Drag state
    private bool isDragging = false;
    private Vector3 draggedPosition = Vector3.zero;

    void Start()
    {
        if (!IsInitialized)
            Initialize();
    }

    public void Initialize()
    {
        if (ropeCompute == null)
        {
            Debug.LogError("Rope compute shader not assigned!");
            return;
        }

        // Find kernels
        integrateKernel = ropeCompute.FindKernel("IntegrateRope");
        solveConstraintsKernel = ropeCompute.FindKernel("SolveConstraints");
        pendulumKernel = ropeCompute.FindKernel("ApplyPendulum");

        bucketPlaneCollisionCompute = Resources.Load<ComputeShader>("BucketPlaneCollision");
        if (bucketPlaneCollisionCompute != null)
        {
            bucketPlaneKernel = bucketPlaneCollisionCompute.FindKernel("ResolveBucketPlaneCollision");
            ropePlaneKernel = bucketPlaneCollisionCompute.FindKernel("ResolveRopePlaneCollision");
        }
        else
        {
            Debug.LogWarning("RopeSimulationGPU: BucketPlaneCollision.compute not found in Resources — bucket will pass through the plane.");
            ropePlaneKernel = -1;
        }

        // Initialize rope data
        InitializeRope();
        
        // Create and set buffers using ComputeHelper
        CreateBuffers();
    }

    void InitializeRope()
    {
        float segmentLength = ropeLength / Mathf.Max(1, pointCount - 1);
        
        positions = new Vector3[pointCount];
        previousPositions = new Vector3[pointCount];
        inverseMasses = new float[pointCount];

        Vector3 anchorPos = anchorTransform != null ? anchorTransform.position : Vector3.zero;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 pos = anchorPos + Vector3.down * segmentLength * i;
            float mass = (i == pointCount - 1) ? 3f : 1f; // Bucket point heavier
            
            positions[i] = pos;
            previousPositions[i] = pos;
            inverseMasses[i] = 1f / mass;
        }
    }

    void CreateBuffers()
    {
        // Release existing buffers
        ReleaseBuffers();
        
        // Create buffers using ComputeHelper
        ComputeHelper.CreateStructuredBuffer<Vector3>(ref positionsBuffer, pointCount);
        ComputeHelper.CreateStructuredBuffer<Vector3>(ref previousPositionsBuffer, pointCount);
        ComputeHelper.CreateStructuredBuffer<float>(ref inverseMassesBuffer, pointCount);
        
        // Set data
        positionsBuffer.SetData(positions);
        previousPositionsBuffer.SetData(previousPositions);
        inverseMassesBuffer.SetData(inverseMasses);

        // Set buffers for all kernels using ComputeHelper
        int[] kernels = new int[] { integrateKernel, solveConstraintsKernel, pendulumKernel };
        
        ComputeHelper.SetBuffer(ropeCompute, positionsBuffer, "Positions", kernels);
        ComputeHelper.SetBuffer(ropeCompute, previousPositionsBuffer, "PreviousPositions", kernels);
        ComputeHelper.SetBuffer(ropeCompute, inverseMassesBuffer, "InverseMasses", kernels);

        if (bucketPlaneCollisionCompute != null && bucketPlaneKernel >= 0)
        {
            ComputeHelper.SetBuffer(
                bucketPlaneCollisionCompute,
                positionsBuffer,
                "Positions",
                bucketPlaneKernel);
            ComputeHelper.SetBuffer(
                bucketPlaneCollisionCompute,
                previousPositionsBuffer,
                "PreviousPositions",
                bucketPlaneKernel);
        }
    }

    void ReleaseBuffers()
    {
        ComputeHelper.Release(positionsBuffer, previousPositionsBuffer, inverseMassesBuffer);
        positionsBuffer = null;
        previousPositionsBuffer = null;
        inverseMassesBuffer = null;
    }

    void Update()
    {
        if (!IsInitialized)
            return;

        if (!simulateInUpdate)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Get positions from transforms
        Vector3 anchorPos = anchorTransform != null ? anchorTransform.position : Vector3.zero;
        Vector3 draggedPos = bucketTransform != null ? bucketTransform.position : GetBucketPosition();

        bool dragging = false;

        // Simulate
        Simulate(dt, anchorPos, dragging, draggedPos);
    }

    bool planeCollisionResolvedThisFrame;

    public void Simulate(float dt, Vector3 anchorPosition, bool dragging, Vector3 draggedPos)
    {
        if (!IsInitialized)
            return;

        isDragging = dragging;
        draggedPosition = draggedPos;
        planeCollisionResolvedThisFrame = false;

        SetCommonParameters(dt, anchorPosition);

        ropeCompute.SetInt("isDragging", isDragging ? 1 : 0);
        ropeCompute.SetVector("draggedPosition", draggedPosition);
        ComputeHelper.Dispatch(ropeCompute, pointCount, 1, 1, integrateKernel);

        // Resolve plane before constraints so the rope solver does not fight the contact impulse.
        if (ShouldResolvePlaneCollision(anchorPosition))
        {
            SetBucketPlaneCollisionParameters(dt, anchorPosition);
            DispatchBucketPlaneCollision();
            planeCollisionResolvedThisFrame = true;
        }

        for (int i = 0; i < iterations; i++)
        {
            DispatchConstraintPhase();
        }

        EnforceBucketHullAbovePlane(dt, anchorPosition);

        if (readBackData)
        {
            ReadBackData();
            UpdateContactMode(anchorPosition);
            UpdatePendulumState(dt, anchorPosition);
        }
    }

    void DispatchConstraintPhase()
    {
        ropeCompute.SetInt("isDragging", isDragging ? 1 : 0);
        ropeCompute.SetVector("draggedPosition", draggedPosition);
        ropeCompute.SetInt("constraintPhase", 0);
        ComputeHelper.Dispatch(ropeCompute, pointCount, 1, 1, solveConstraintsKernel);
        ropeCompute.SetInt("constraintPhase", 1);
        ComputeHelper.Dispatch(ropeCompute, pointCount, 1, 1, solveConstraintsKernel);
    }

    bool ShouldResolvePlaneCollision(Vector3 anchorPosition)
    {
        if (bucketPlaneCollisionCompute == null ||
            bucketPlaneKernel < 0 ||
            planeCollision == null ||
            planeCollision.plane == null ||
            bucketVolume == null ||
            positions == null ||
            positions.Length == 0)
        {
            return false;
        }

        Vector3 planePos = planeCollision.plane.position;
        Vector3 planeNormal = planeCollision.GetPlaneNormal();
        Vector3 ropeEnd = positions[pointCount - 1];
        Vector3 bucketUp = (anchorPosition - ropeEnd).sqrMagnitude > 1e-6f
            ? (anchorPosition - ropeEnd).normalized
            : Vector3.up;

        Vector3 bottomCenter = ropeEnd
            - bucketUp * bucketAttachmentDistance
            - bucketUp * bucketVolume.PlaneCollisionBottomExtent;

        float startDist = bucketVolume.EffectivePlaneSkin + planeCollisionStartDistance;
        float coarseDistance = BucketPlaneContact.SignedPlaneDistance(bottomCenter, planePos, planeNormal);
        return coarseDistance < startDist + bucketVolume.PlaneContactRadius;
    }

    void GetBucketPlaneAxes(Vector3 anchorPosition, Vector3 ropeEnd, Vector3 planeNormal, out Vector3 bucketUp, out Vector3 bucketRight, out Vector3 bucketForward)
    {
        bucketUp = (anchorPosition - ropeEnd).sqrMagnitude > 1e-6f
            ? (anchorPosition - ropeEnd).normalized
            : bucketTransform != null
                ? bucketTransform.up
                : Vector3.up;

        bucketRight = bucketVolume != null
            ? bucketVolume.GetPreferredPlaneContactRight(bucketTransform, bucketUp, planeNormal)
            : Vector3.Cross(bucketUp, planeNormal);
        bucketRight = Vector3.ProjectOnPlane(bucketRight, bucketUp);
        if (bucketRight.sqrMagnitude < 1e-6f)
            bucketRight = Vector3.Cross(bucketUp, planeNormal);
        if (bucketRight.sqrMagnitude < 1e-6f)
            bucketRight = Vector3.Cross(bucketUp, Vector3.forward);
        bucketRight.Normalize();

        if (bucketVolume != null && Mathf.Abs(bucketVolume.twistAngleDegrees) > 0.001f)
            bucketRight = Quaternion.AngleAxis(bucketVolume.twistAngleDegrees, bucketUp) * bucketRight;

        bucketForward = Vector3.Cross(bucketRight, bucketUp).normalized;
    }

    void SetBucketPlaneCollisionParameters(float dt, Vector3 anchorPosition)
    {
        Transform planeTransform = planeCollision.plane;
        Vector3 planeNormal = planeCollision.GetPlaneNormal();

        bucketPlaneCollisionCompute.SetInt("enableBucketPlaneCollision", 1);
        bucketPlaneCollisionCompute.SetInt("numPoints", pointCount);
        bucketPlaneCollisionCompute.SetFloat("deltaTime", dt);
        bucketPlaneCollisionCompute.SetVector("anchorPosition", anchorPosition);
        bucketPlaneCollisionCompute.SetVector("bucketPlanePosition", planeTransform.position);
        bucketPlaneCollisionCompute.SetVector("bucketPlaneNormal", planeNormal);
        bucketPlaneCollisionCompute.SetVector("gravityWorld", gravity);
        bucketPlaneCollisionCompute.SetFloat("bucketAttachmentDistance", bucketAttachmentDistance);
        bucketPlaneCollisionCompute.SetFloat("bucketTwistAngleRadians", bucketVolume.TwistAngleRadians);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneBottomExtent", bucketVolume.PlaneCollisionBottomExtent);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneTopExtent", bucketVolume.PlaneCollisionTopExtent);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneCollisionRadius", bucketVolume.PlaneContactRadius);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneCollisionSkin", bucketVolume.EffectivePlaneSkin);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneBounce", bucketVolume.planeCollisionBounce);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneFriction", bucketVolume.planeCollisionFriction);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneMaxBounceSpeed", bucketVolume.planeMaxBounceSpeed);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneSlideGravity", bucketVolume.planeSlideGravity);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneFlatAlignmentDot", bucketVolume.PlaneFlatAlignmentDot);
        bucketPlaneCollisionCompute.SetFloat("bucketPlaneImpactSpeedThreshold", bucketVolume.planeImpactSpeedThreshold);
    }

    void EnforceBucketHullAbovePlane(float dt, Vector3 anchorPosition)
    {
        if (bucketPlaneCollisionCompute == null ||
            bucketPlaneKernel < 0 ||
            planeCollision == null ||
            planeCollision.plane == null ||
            bucketVolume == null)
        {
            return;
        }

        SetBucketPlaneCollisionParameters(dt, anchorPosition);
        DispatchBucketPlaneCollision(positionOnly: true);
    }

    void DispatchBucketPlaneCollision(bool positionOnly = false)
    {
        if (bucketPlaneCollisionCompute == null || bucketPlaneKernel < 0)
            return;

        bucketPlaneCollisionCompute.SetInt("bucketPlanePositionOnly", positionOnly ? 1 : 0);
        ComputeHelper.Dispatch(bucketPlaneCollisionCompute, 1, 1, 1, bucketPlaneKernel);
    }

    void DispatchRopePlaneCollision()
    {
        if (!resolveRopeSegmentPlaneCollision ||
            bucketPlaneCollisionCompute == null ||
            ropePlaneKernel < 0)
            return;

        ComputeHelper.Dispatch(bucketPlaneCollisionCompute, pointCount, 1, 1, ropePlaneKernel);
    }

    void ResolveBucketPlaneCollision(float dt, Vector3 anchorPosition)
    {
        if (bucketPlaneCollisionCompute == null ||
            bucketPlaneKernel < 0 ||
            planeCollision == null ||
            planeCollision.plane == null ||
            bucketVolume == null)
        {
            LastContactMode = BucketContactMode.None;
            bucketVolume?.SetContactMode(BucketContactMode.None);
            return;
        }

        SetBucketPlaneCollisionParameters(dt, anchorPosition);
        DispatchBucketPlaneCollision();
    }

    void ResolveRopePlaneCollision(float dt, Vector3 anchorPosition)
    {
        if (bucketPlaneCollisionCompute == null ||
            ropePlaneKernel < 0 ||
            planeCollision == null ||
            planeCollision.plane == null ||
            bucketVolume == null)
        {
            return;
        }

        SetBucketPlaneCollisionParameters(dt, anchorPosition);
        DispatchRopePlaneCollision();
    }

    public void SetRopeEndPosition(Vector3 worldPosition, bool preserveVelocity = true)
    {
        if (!IsInitialized || pointCount <= 0)
            return;

        worldPosition = SanitizePosition(worldPosition);

        int last = pointCount - 1;
        if (positions != null && positions.Length > last)
        {
            if (preserveVelocity && previousPositions != null && previousPositions.Length > last)
            {
                Vector3 impliedVelocity = positions[last] - previousPositions[last];
                positions[last] = worldPosition;
                previousPositions[last] = worldPosition - impliedVelocity;
            }
            else
            {
                positions[last] = worldPosition;
                if (previousPositions != null && previousPositions.Length > last)
                    previousPositions[last] = worldPosition;
            }
        }

        UploadRopeEndBuffers();
    }

    /// <summary>
    /// Move rope end for plane contact without preserving inbound normal velocity (prevents pop-bounce).
    /// </summary>
    public void SetRopeEndPositionWithPlaneContact(Vector3 worldPosition, Vector3 planeNormal, float dt)
    {
        if (!IsInitialized || pointCount <= 0)
            return;

        int last = pointCount - 1;
        if (positions == null || positions.Length <= last)
            return;

        worldPosition = SanitizePosition(worldPosition);

        float safeDt = Mathf.Max(dt, 1e-4f);
        Vector3 oldPos = SanitizePosition(positions[last]);
        Vector3 velocity = Vector3.zero;
        if (previousPositions != null && previousPositions.Length > last)
            velocity = (oldPos - SanitizePosition(previousPositions[last])) / safeDt;

        velocity = SanitizeVelocity(velocity);
        planeNormal = planeNormal.sqrMagnitude > 1e-8f ? planeNormal.normalized : Vector3.up;
        float normalSpeed = Vector3.Dot(velocity, planeNormal);
        if (normalSpeed < 0f)
            velocity -= planeNormal * normalSpeed;

        positions[last] = worldPosition;
        if (previousPositions != null && previousPositions.Length > last)
            previousPositions[last] = worldPosition - velocity * safeDt;

        UploadRopeEndBuffers();
    }

    static bool IsFiniteVector(Vector3 v) =>
        float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

    static Vector3 SanitizePosition(Vector3 v)
    {
        if (IsFiniteVector(v))
            return v;
        return Vector3.zero;
    }

    static Vector3 SanitizeVelocity(Vector3 v)
    {
        if (IsFiniteVector(v))
            return Vector3.ClampMagnitude(v, 50f);
        return Vector3.zero;
    }

    bool SanitizeRopePositions()
    {
        if (positions == null)
            return false;

        bool repaired = false;
        Vector3 fallback = anchorTransform != null ? anchorTransform.position : Vector3.zero;
        for (int i = 0; i < pointCount && i < positions.Length; i++)
        {
            if (!IsFiniteVector(positions[i]))
            {
                positions[i] = i > 0 ? positions[i - 1] : fallback;
                repaired = true;
            }

            if (previousPositions != null && i < previousPositions.Length && !IsFiniteVector(previousPositions[i]))
            {
                previousPositions[i] = positions[i];
                repaired = true;
            }
        }

        return repaired;
    }

    void UploadRopeEndBuffers()
    {
        if (positionsBuffer == null || !positionsBuffer.IsValid() || pointCount <= 0)
            return;

        int last = pointCount - 1;
        positionsBuffer.SetData(positions, last, last, 1);
        if (previousPositionsBuffer != null && previousPositionsBuffer.IsValid())
            previousPositionsBuffer.SetData(previousPositions, last, last, 1);
    }

    public bool IsRopeEndPenetratingPlane(Vector3 ropeEnd, Vector3 anchorPosition)
    {
        if (bucketVolume == null || planeCollision == null || planeCollision.plane == null)
            return false;

        Vector3 planeNormal = planeCollision.GetPlaneNormal();
        GetBucketPlaneAxes(anchorPosition, ropeEnd, planeNormal, out Vector3 bucketUp, out Vector3 right, out Vector3 forward);

        float minDistance = BucketPlaneContact.SampleMinimumPlaneDistance(
            ropeEnd,
            bucketUp,
            right,
            forward,
            planeCollision.plane.position,
            planeNormal,
            bucketAttachmentDistance,
            bucketVolume.PlaneCollisionBottomExtent,
            bucketVolume.PlaneCollisionTopExtent,
            bucketVolume.PlaneContactRadius);

        return minDistance < bucketVolume.EffectivePlaneSkin;
    }

    public Vector3 ClampRopeEndAbovePlane(Vector3 ropeEnd, Vector3 anchorPosition)
    {
        if (bucketVolume == null || planeCollision == null || planeCollision.plane == null)
            return ropeEnd;

        Vector3 planeNormal = planeCollision.GetPlaneNormal();
        GetBucketPlaneAxes(anchorPosition, ropeEnd, planeNormal, out Vector3 bucketUp, out Vector3 bucketRight, out _);

        return BucketPlaneContact.ClampRopeEndAbovePlane(
            ropeEnd,
            anchorPosition,
            planeCollision.plane.position,
            planeNormal,
            bucketVolume.PlaneContactRadius,
            bucketVolume.PlaneCollisionBottomExtent,
            bucketVolume.PlaneCollisionTopExtent,
            bucketAttachmentDistance,
            bucketVolume.EffectivePlaneSkin,
            bucketUp,
            bucketRight);
    }

    /// <summary>
    /// Final hull clamp using the positioned bucket transform (accounts for scale/orient).
    /// Applies a smoothed, capped correction and damps inbound normal velocity.
    /// </summary>
    public Vector3 EnforceRopeEndFromBucketTransform(Vector3 anchorPosition, float dt)
    {
        if (bucketTransform == null ||
            bucketVolume == null ||
            planeCollision == null ||
            planeCollision.plane == null ||
            positions == null ||
            positions.Length == 0)
        {
            return GetBucketPosition();
        }

        int last = pointCount - 1;
        Vector3 ropeEnd = positions[last];
        Vector3 planePos = planeCollision.plane.position;
        Vector3 planeNormal = planeCollision.GetPlaneNormal();
        float skin = bucketVolume.EffectivePlaneSkin;

        bucketVolume.GetPlaneHullWorld(
            out Vector3 center,
            out Vector3 axis,
            out float bottomExtent,
            out float topExtent,
            out float hullRadius);

        if (!IsFiniteVector(center) || !IsFiniteVector(axis))
            return ropeEnd;

        float minDistance = BucketPlaneContact.CylinderPlaneMinDistance(
            center,
            axis,
            bottomExtent,
            topExtent,
            hullRadius,
            planePos,
            planeNormal);

        if (!float.IsFinite(minDistance))
            return ropeEnd;

        float penetration = skin - minDistance;
        if (penetration <= bucketVolume.planeContactPostCorrectionDeadZone)
            return ropeEnd;

        float lift = Mathf.Min(
            penetration * bucketVolume.planeContactPostCorrectionSharpness,
            bucketVolume.planeContactMaxCorrectionPerStep);

        if (lift <= 1e-6f)
            return ropeEnd;

        Vector3 corrected = ropeEnd + planeNormal * lift;
        SetRopeEndPositionWithPlaneContact(corrected, planeNormal, dt);
        return corrected;
    }

    public bool IsRopeEndNearPlaneContact(Vector3 ropeEnd, Vector3 anchorPosition)
    {
        if (bucketVolume == null || planeCollision == null || planeCollision.plane == null)
            return false;

        Vector3 planeNormal = planeCollision.GetPlaneNormal();
        GetBucketPlaneAxes(anchorPosition, ropeEnd, planeNormal, out Vector3 bucketUp, out Vector3 right, out Vector3 forward);

        float minDistance = BucketPlaneContact.SampleMinimumPlaneDistance(
            ropeEnd,
            bucketUp,
            right,
            forward,
            planeCollision.plane.position,
            planeNormal,
            bucketAttachmentDistance,
            bucketVolume.PlaneCollisionBottomExtent,
            bucketVolume.PlaneCollisionTopExtent,
            bucketVolume.PlaneContactRadius);

        float skin = bucketVolume.EffectivePlaneSkin;
        return minDistance < skin + 0.15f;
    }

    public Vector3 SnapRopeEndToPlaneContact(Vector3 ropeEnd, Vector3 anchorPosition)
    {
        if (bucketVolume == null || planeCollision == null || planeCollision.plane == null)
            return ropeEnd;

        Vector3 planeNormal = planeCollision.GetPlaneNormal();
        GetBucketPlaneAxes(anchorPosition, ropeEnd, planeNormal, out Vector3 bucketUp, out Vector3 bucketRight, out _);

        return BucketPlaneContact.SnapRopeEndToPlaneContact(
            ropeEnd,
            anchorPosition,
            planeCollision.plane.position,
            planeNormal,
            bucketVolume.PlaneContactRadius,
            bucketVolume.PlaneCollisionBottomExtent,
            bucketVolume.PlaneCollisionTopExtent,
            bucketAttachmentDistance,
            bucketVolume.EffectivePlaneSkin,
            bucketUp,
            bucketRight);
    }

    void UpdateContactMode(Vector3 anchorPosition)
    {
        if (bucketVolume == null || planeCollision == null || planeCollision.plane == null || positions == null || positions.Length == 0)
        {
            LastContactMode = BucketContactMode.None;
            bucketVolume?.SetContactMode(BucketContactMode.None);
            return;
        }

        Vector3 ropeEnd = positions[pointCount - 1];
        Vector3 planeNormal = planeCollision.GetPlaneNormal();
        GetBucketPlaneAxes(anchorPosition, ropeEnd, planeNormal, out Vector3 bucketUp, out Vector3 bucketRight, out Vector3 bucketForward);

        float minDistance = BucketPlaneContact.SampleMinimumPlaneDistance(
            ropeEnd,
            bucketUp,
            bucketRight,
            bucketForward,
            planeCollision.plane.position,
            planeNormal,
            bucketAttachmentDistance,
            bucketVolume.PlaneCollisionBottomExtent,
            bucketVolume.PlaneCollisionTopExtent,
            bucketVolume.PlaneContactRadius);

        if (minDistance >= bucketVolume.EffectivePlaneSkin + 0.02f)
        {
            LastContactMode = BucketContactMode.None;
            bucketVolume.SetContactMode(BucketContactMode.None);
            return;
        }

        LastContactMode = BucketPlaneContact.Classify(
            ropeEnd,
            anchorPosition,
            planeCollision.plane.position,
            planeNormal,
            bucketVolume.PlaneContactRadius,
            bucketVolume.PlaneCollisionBottomExtent,
            bucketVolume.PlaneCollisionTopExtent,
            bucketAttachmentDistance,
            bucketVolume.EffectivePlaneSkin,
            bucketRight,
            bucketVolume.PlaneFlatAlignmentDot,
            bucketUp);
        bucketVolume.SetContactMode(LastContactMode);
    }

    void SetCommonParameters(float dt, Vector3 anchorPos)
    {
        float segmentLength = ropeLength / Mathf.Max(1, pointCount - 1);
        
        ropeCompute.SetInt("numPoints", pointCount);
        ropeCompute.SetFloat("deltaTime", dt);
        ropeCompute.SetFloat("segmentLength", segmentLength);
        ropeCompute.SetFloat("stiffness", stiffness);
        ropeCompute.SetFloat("maxStretchMultiplier", maxStretchMultiplier);
        ropeCompute.SetFloat("ropeDamping", ropeDamping);
        ropeCompute.SetVector("gravity", gravity);
        ropeCompute.SetVector("anchorPosition", anchorPos);
        
        // Pendulum state
        ropeCompute.SetFloat("previousTheta", previousTheta);
        ropeCompute.SetFloat("previousPhi", previousPhi);
    }

    void ReadBackData()
    {
        if (positionsBuffer != null && positionsBuffer.IsValid())
        {
            positionsBuffer.GetData(positions);
            if (SanitizeRopePositions())
            {
                positionsBuffer.SetData(positions);
                if (previousPositionsBuffer != null && previousPositionsBuffer.IsValid())
                    previousPositionsBuffer.SetData(previousPositions);
            }
        }
    }

    void UpdatePendulumState(float dt, Vector3 anchorPosition)
    {
        if (positions == null || positions.Length == 0)
            return;

        Vector3 bucketPos = positions[pointCount - 1];
        Vector3 ropeVector = bucketPos - anchorPosition;
        float l = ropeVector.magnitude;

        if (l < 0.001f) return;

        previousTheta = Mathf.Acos(Mathf.Clamp(-ropeVector.y / l, -1f, 1f));
        previousPhi = Mathf.Atan2(ropeVector.z, ropeVector.x);
    }

    public Vector3 GetBucketPosition()
    {
        if (readBackData)
        {
            return positions != null && positions.Length > 0 ? positions[pointCount - 1] : Vector3.zero;
        }
        else
        {
            // If not reading back, we need a separate buffer for bucket position
            Debug.LogWarning("Read back data is disabled. Cannot get bucket position.");
            return Vector3.zero;
        }
    }

    public Vector3 GetPointPosition(int index)
    {
        if (!readBackData)
        {
            Debug.LogWarning("Read back data is disabled. Use ReadBackData() first.");
            return Vector3.zero;
        }
        
        return positions != null && index < positions.Length ? positions[index] : Vector3.zero;
    }

    public void SetAnchorPosition(Vector3 position)
    {
        if (anchorTransform != null)
        {
            anchorTransform.position = position;
        }
    }

    public void SetBucketPosition(Vector3 position)
    {
        if (bucketTransform != null)
        {
            bucketTransform.position = position;
        }
    }

    public void Dispose()
    {
        ReleaseBuffers();
        
        positions = null;
        previousPositions = null;
        inverseMasses = null;
    }

    void OnDestroy()
    {
        Dispose();
    }

    // void OnDrawGizmos()
    // {
    //     if (!visualizeRope || positions == null || positions.Length == 0)
    //         return;

    //     // Draw rope segments
    //     Gizmos.color = Color.cyan;
    //     for (int i = 0; i < positions.Length - 1; i++)
    //     {
    //         Gizmos.DrawLine(positions[i], positions[i + 1]);
            
    //         // Draw intermediate points smaller
    //         if (i > 0 && i < positions.Length - 1)
    //         {
    //             Gizmos.DrawSphere(positions[i], 0.03f);
    //         }
    //     }
        
    //     // Draw anchor point
    //     if (anchorTransform != null)
    //     {
    //         Gizmos.color = Color.red;
    //         Gizmos.DrawSphere(anchorTransform.position, 0.08f);
    //     }
        
    //     // Draw bucket point larger
    //     if (positions.Length > 0)
    //     {
    //         Gizmos.color = Color.yellow;
    //         Gizmos.DrawSphere(positions[positions.Length - 1], 0.1f);
    //     }
    // }
}
