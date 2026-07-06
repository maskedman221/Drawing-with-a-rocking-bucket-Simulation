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

    public void Simulate(float dt, Vector3 anchorPosition, bool dragging, Vector3 draggedPos)
    {
        if (!IsInitialized)
            return;

        // Store drag state for use in constraints
        isDragging = dragging;
        draggedPosition = draggedPos;

        // Set common parameters
        SetCommonParameters(dt, anchorPosition);

        // 1. Integrate motion
        ropeCompute.SetInt("isDragging", isDragging ? 1 : 0);
        ropeCompute.SetVector("draggedPosition", draggedPosition);
        
        ComputeHelper.Dispatch(ropeCompute, pointCount, 1, 1, integrateKernel);

        // 2. Solve constraints multiple times. Even/odd phases avoid adjacent
        // segments writing the same point at the same time on the GPU.
        for (int i = 0; i < iterations; i++)
        {
            ropeCompute.SetInt("isDragging", isDragging ? 1 : 0);
            ropeCompute.SetVector("draggedPosition", draggedPosition);
            ropeCompute.SetInt("constraintPhase", 0);
            ComputeHelper.Dispatch(ropeCompute, pointCount, 1, 1, solveConstraintsKernel);
            ropeCompute.SetInt("constraintPhase", 1);
            ComputeHelper.Dispatch(ropeCompute, pointCount, 1, 1, solveConstraintsKernel);
        }

        // 3. Apply pendulum behavior. This kernel is currently a no-op, but
        // keep the dispatch out of the hot path until it actually changes data.

        // 4. Read back data if needed
        if (readBackData)
        {
            ReadBackData();
            
            // Update pendulum state for next frame
            UpdatePendulumState(dt, anchorPosition);
        }
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
