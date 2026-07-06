using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct SpatialIndex
{
    public uint particleIndex;
    public uint hash;
    public uint key;
}

public class SPHManager : MonoBehaviour
{
    [Header("Particles")]
    public int gridSize = 10;
    public float particleSpacing = 0.008f;
    [Header("Container")]
    public Vector3 boxCenter = new Vector3(0, 2, 0);
    public Vector3 boxSize = new Vector3(5, 5, 5);
    public Vector3 boxRotation = Vector3.zero;

    [Header("Simulation")]
    public float gravity = -9.81f;
    public float dt = 0.005f;
    public int solverIterations = 4;
    public bool showContainer = false;
    public bool GPUSimulation = true;

    [Header("Debug")]
    public bool debugBucketStages = false;
    [Min(1)]
    public int debugBucketStageInterval = 30;
    [Header("SPH")]
    public bool autoTuneSmoothingRadius = false;
    [Range(0f, 1000f)]
    public float smoothingRadiusToSpacing = 1.25f;
    public float smoothingRadius = 1f;
    public float targetDensity = 20f;
    public float pressureMultiplier = 30f;
    public float nearPressureMultiplier = 60f;
    public float viscosityStrength = 0.15f;

    [Tooltip("Hard cap on particle speed. Prevents SPH pressure spikes from making the nozzle stream burst/explode. Set 0 to disable.")]
    public float maxSpeed = 6f;

    [Header("Collision")]
    public float wallBounce = 0.05f;
    public BucketVolume bucket;
    public PaintingCollision planeCollision;

    List<SPHParticle> particles = new();


    Dictionary<Vector3Int, List<SPHParticle>> grid =
        new Dictionary<Vector3Int, List<SPHParticle>>();

    public IReadOnlyList<SPHParticle> Particles => particles;
    [Header("Compute Shader")]
    public ComputeShader simulationShader;
    public ComputeShader bucketCollisionCompute;
    public ComputeShader planeCollisionCompute;
    public int ParticleCount => particles.Count;
    public bool IsReady =>
        positionBuffer != null &&
        positionBuffer.IsValid() &&
        particles.Count > 0;
    const float PI = Mathf.PI;
    Vector3[] positions;
    Vector3[] predictedPositions;
    Vector3[] velocities;
    Vector2[] densities;
    ComputeBuffer positionBuffer;
    ComputeBuffer predictedBuffer;
    ComputeBuffer velocityBuffer;
    ComputeBuffer densityBuffer;
    ComputeBuffer spatialIndicesBuffer;
    ComputeBuffer spatialOffsetsBuffer;
    ComputeBuffer particalStateBuffer;
    ComputeBuffer nozzleDropCounterBuffer;
    int externalKernel;
    int clearSpatialOffsetsKernel;
    int updateSpatialHashKernel;
    int calculateDensitiesKernel;
    int calculatePressureForceKernel;
    int calculateViscosityKernel;
    int updatePositionsKernel;
    int bucketKernel;
    int bucketMotionKernel;
    int planeKernel;
    int[] particleState;
    int debugBucketStageFrame;
    bool hasPreviousBucketFrame;
    Vector3 previousBucketPosition;
    Vector3 previousBucketRight;
    Vector3 previousBucketUp;
    Vector3 previousBucketForward;
    readonly uint[] nozzleDropCounterReset = new uint[1];
    float nozzleDropBudget;
    FluidParticleRenderer particleRenderer = new FluidParticleRenderer();
    #region Kernels

    #endregion

    void Start()
    {
        ApplySmoothingRadiusTuning();

        float spacing = Mathf.Max(0.001f, particleSpacing);
        Vector3 startCenter = showContainer ? boxCenter : (bucket != null ? bucket.transform.position : transform.position);
        Quaternion containerRotation = Quaternion.Euler(boxRotation);

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 localPos =
                        new Vector3(
                            (x - gridSize * 0.5f) * spacing,
                            (y - gridSize * 0.5f) * spacing,
                            (z - gridSize * 0.5f) * spacing
                        );

                    Vector3 worldPos = showContainer || bucket == null
                        ? startCenter + containerRotation * localPos
                        : bucket.transform.TransformPoint(localPos);
                    SPHParticle p = new SPHParticle();
                    p.position = worldPos;
                    p.velocity = Vector3.zero;
                    p.OnPlane = false;
                    p.color = particleRenderer.paintColor;
                    particles.Add(p);
                }
            }
        }
        CapturePreviousBucketFrame();
        positions = new Vector3[particles.Count];
        velocities = new Vector3[particles.Count];
        predictedPositions = new Vector3[particles.Count];
        particleState = new int[particles.Count];
        for(int i=0;i<particles.Count;i++)
        {
            positions[i] = particles[i].position;
            velocities[i] = particles[i].velocity;
            predictedPositions[i] = particles[i].position;
            particleState[i] = particles[i].OnPlane ? 1 : 0;
        }
        InitializeComputeShader();
    }

    void OnValidate()
    {
        particleSpacing = Mathf.Max(0.001f, particleSpacing);
        smoothingRadiusToSpacing = Mathf.Max(1f, smoothingRadiusToSpacing);

        if (autoTuneSmoothingRadius)
            smoothingRadius = particleSpacing * smoothingRadiusToSpacing;
    }

    void ApplySmoothingRadiusTuning()
    {
        if (!autoTuneSmoothingRadius)
            return;

        smoothingRadius = Mathf.Max(0.001f, particleSpacing * smoothingRadiusToSpacing);
    }
    void InitializeComputeShader()
    {
        externalKernel =simulationShader.FindKernel("ExternalForces");
        clearSpatialOffsetsKernel = simulationShader.FindKernel("ClearSpatialOffsets");
        updateSpatialHashKernel =simulationShader.FindKernel("UpdateSpatialHash");
        calculateDensitiesKernel =simulationShader.FindKernel("CalculateDensities");
        calculatePressureForceKernel =simulationShader.FindKernel("CalculatePressureForce");
        calculateViscosityKernel = simulationShader.FindKernel("CalculateViscosity");
        updatePositionsKernel = simulationShader.FindKernel("UpdatePositions");
        bucketMotionKernel = bucketCollisionCompute.FindKernel("ApplyBucketMotion");
        bucketKernel = bucketCollisionCompute.FindKernel("ResolveBucketCollision");
        planeKernel = planeCollisionCompute.FindKernel("ResolvePlaneCollision");
        positionBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(particles.Count);
        velocityBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(particles.Count);
        predictedBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(particles.Count);
        particalStateBuffer =ComputeHelper.CreateStructuredBuffer<int>(particles.Count);
        densityBuffer =ComputeHelper.CreateStructuredBuffer<Vector2>(particles.Count);
        spatialIndicesBuffer =ComputeHelper.CreateStructuredBuffer<SpatialIndex>(particles.Count);
        spatialOffsetsBuffer =ComputeHelper.CreateStructuredBuffer<uint>(particles.Count);
        nozzleDropCounterBuffer = ComputeHelper.CreateStructuredBuffer<uint>(1);
        positionBuffer.SetData(positions);
        velocityBuffer.SetData(velocities);
        predictedBuffer.SetData(predictedPositions);
        particalStateBuffer.SetData(particleState);
        nozzleDropCounterBuffer.SetData(nozzleDropCounterReset);

        ComputeHelper.SetBuffer(simulationShader,positionBuffer,"Positions",externalKernel);
        ComputeHelper.SetBuffer(simulationShader,velocityBuffer,"Velocities",externalKernel);
        ComputeHelper.SetBuffer(simulationShader,predictedBuffer,"PredictedPositions",externalKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialOffsetsBuffer,"SpatialOffsets",clearSpatialOffsetsKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialIndicesBuffer,"SpatialIndices",updateSpatialHashKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialOffsetsBuffer,"SpatialOffsets",updateSpatialHashKernel);
        ComputeHelper.SetBuffer(simulationShader,predictedBuffer,"PredictedPositions",updateSpatialHashKernel);
        ComputeHelper.SetBuffer(simulationShader,densityBuffer, "Densities",calculateDensitiesKernel);
        ComputeHelper.SetBuffer(simulationShader,predictedBuffer,"PredictedPositions",calculateDensitiesKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialIndicesBuffer,"SpatialIndices",calculateDensitiesKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialOffsetsBuffer,"SpatialOffsets",calculateDensitiesKernel);
        ComputeHelper.SetBuffer(simulationShader,predictedBuffer,"PredictedPositions",calculatePressureForceKernel);
        ComputeHelper.SetBuffer(simulationShader,densityBuffer,"Densities",calculatePressureForceKernel);
        ComputeHelper.SetBuffer(simulationShader,velocityBuffer,"Velocities",calculatePressureForceKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialIndicesBuffer,"SpatialIndices",calculatePressureForceKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialOffsetsBuffer,"SpatialOffsets",calculatePressureForceKernel);
        ComputeHelper.SetBuffer(simulationShader,predictedBuffer,"PredictedPositions",calculateViscosityKernel);
        ComputeHelper.SetBuffer(simulationShader,velocityBuffer,"Velocities",calculateViscosityKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialIndicesBuffer,"SpatialIndices",calculateViscosityKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialOffsetsBuffer,"SpatialOffsets",calculateViscosityKernel);
        ComputeHelper.SetBuffer(simulationShader,positionBuffer,"Positions",updatePositionsKernel);
        ComputeHelper.SetBuffer(simulationShader,velocityBuffer,"Velocities",updatePositionsKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",externalKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",clearSpatialOffsetsKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",updateSpatialHashKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculateDensitiesKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculatePressureForceKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculateViscosityKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",updatePositionsKernel);
        ComputeHelper.SetBuffer(bucketCollisionCompute,positionBuffer,"Positions",bucketMotionKernel, bucketKernel);
        ComputeHelper.SetBuffer(bucketCollisionCompute,velocityBuffer,"Velocities",bucketMotionKernel, bucketKernel);
        ComputeHelper.SetBuffer(bucketCollisionCompute,particalStateBuffer,"ParticleState",bucketMotionKernel, bucketKernel);
        ComputeHelper.SetBuffer(bucketCollisionCompute,nozzleDropCounterBuffer,"NozzleDropCounter",bucketKernel);
        ComputeHelper.SetBuffer(planeCollisionCompute,positionBuffer,"Positions",planeKernel);
        ComputeHelper.SetBuffer(planeCollisionCompute,velocityBuffer,"Velocities",planeKernel);
        ComputeHelper.SetBuffer(planeCollisionCompute,particalStateBuffer,"ParticleState",planeKernel);
        SetComputeShaderParameters();
    }
    void SetComputeShaderParameters()
    {
        ApplySmoothingRadiusTuning();

        simulationShader.SetFloat("gravity",gravity);
        simulationShader.SetFloat("deltaTime",dt);
        simulationShader.SetInt("numParticles",particles.Count);
        simulationShader.SetFloat("smoothingRadius",smoothingRadius);
        simulationShader.SetFloat("targetDensity",targetDensity);
        simulationShader.SetFloat("pressureMultiplier",pressureMultiplier);
        simulationShader.SetFloat("nearPressureMultiplier",nearPressureMultiplier);
        simulationShader.SetFloat("viscosityStrength",viscosityStrength);
        simulationShader.SetVector("boxCenter",boxCenter);
        simulationShader.SetVector("boxSize",boxSize);
        Quaternion boxOrientation = Quaternion.Euler(boxRotation);
        simulationShader.SetVector("boxRight", boxOrientation * Vector3.right);
        simulationShader.SetVector("boxUp", boxOrientation * Vector3.up);
        simulationShader.SetVector("boxForward", boxOrientation * Vector3.forward);
        simulationShader.SetFloat("wallBounce",wallBounce);
        simulationShader.SetFloat("maxSpeed",maxSpeed);
        simulationShader.SetInt("useBoxCollision", showContainer ? 1 : 0);

        bucketCollisionCompute.SetInt("numParticles", particles.Count);
        bucketCollisionCompute.SetFloat("deltaTime", dt);
        bucketCollisionCompute.SetFloat("bucketRadius", bucket.radius);
        bucketCollisionCompute.SetFloat("bucketHeight", bucket.height);
        bucketCollisionCompute.SetFloat("collisionYOffset", bucket.collisionYOffset);
        bucketCollisionCompute.SetFloat("bucketBounce", bucket.bounce);
        bucketCollisionCompute.SetFloat("bucketFriction", bucket.friction);
        bucketCollisionCompute.SetVector("bucketPosition", bucket.transform.position);
        bucketCollisionCompute.SetVector("bucketRight", bucket.transform.right);
        bucketCollisionCompute.SetVector("bucketUp", bucket.transform.up);
        bucketCollisionCompute.SetVector("bucketForward", bucket.transform.forward);
        if (!hasPreviousBucketFrame)
        {
            CapturePreviousBucketFrame();
        }
        bucketCollisionCompute.SetVector("previousBucketPosition", previousBucketPosition);
        bucketCollisionCompute.SetVector("previousBucketRight", previousBucketRight);
        bucketCollisionCompute.SetVector("previousBucketUp", previousBucketUp);
        bucketCollisionCompute.SetVector("previousBucketForward", previousBucketForward);
        bucketCollisionCompute.SetVector("bucketVelocity", bucket.Velocity);
        bucketCollisionCompute.SetFloat("nozzleRadius", bucket.nozzleRadius);
        bucketCollisionCompute.SetFloat("nozzleExitSpeed", bucket.nozzleExitSpeed);
        bucketCollisionCompute.SetFloat("nozzleCaptureDepth", bucket.nozzleCaptureDepth);
        bucketCollisionCompute.SetInt("useMeteredNozzleFlow", bucket.useMeteredNozzleFlow ? 1 : 0);
        bucketCollisionCompute.SetFloat("nozzleFeedRadius", GetNozzleFeedRadius());
        bucketCollisionCompute.SetInt("nozzleMaxDropsThisStep", 0);
        bucketCollisionCompute.SetFloat("nozzleTangentialDamping", bucket.nozzleTangentialDamping);
        bucketCollisionCompute.SetFloat("bucketVelocityInheritance", bucket.bucketVelocityInheritance);
        bucketCollisionCompute.SetFloat("inertiaStrength", bucket.inertiaStrength);
        bucketCollisionCompute.SetFloat("bucketMotionInheritance", bucket.bucketMotionInheritance);

        planeCollisionCompute.SetInt("numParticles", particles.Count);
        planeCollisionCompute.SetFloat("deltaTime", dt);
        planeCollisionCompute.SetVector("planePosition", planeCollision.plane.position);
        planeCollisionCompute.SetVector("planeNormal", planeCollision.plane.up);
        if (planeCollision.surfaceMaterial != null)
        {
            planeCollisionCompute.SetFloat("planeRestitution", planeCollision.surfaceMaterial.restitution);
            planeCollisionCompute.SetFloat("planeFriction", planeCollision.surfaceMaterial.friction);
        }

        planeCollisionCompute.SetFloat("viscosityStrength", viscosityStrength);

    }
    public void SimulateGPU(float stepDt)
    {
        SetComputeShaderParameters();
        bool debugThisStep = ShouldDebugBucketStages();
        // Use the real (clamped) frame time for this step so the fluid moves at
        // real-world speed while staying stable through frame hitches.
        simulationShader.SetFloat("deltaTime", stepDt);
        bucketCollisionCompute.SetFloat("deltaTime", stepDt);
        planeCollisionCompute.SetFloat("deltaTime", stepDt);
        if (debugThisStep)
            LogBucketStage("before bucket motion");

        if (bucket != null && bucket.isActiveAndEnabled)
        {
            ComputeHelper.Dispatch(bucketCollisionCompute, particles.Count, kernelIndex: bucketMotionKernel);
        }
        if (debugThisStep)
            LogBucketStage("after bucket motion");

        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: externalKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: clearSpatialOffsetsKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: updateSpatialHashKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: calculateDensitiesKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: calculatePressureForceKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: calculateViscosityKernel);
        if (debugThisStep)
            LogBucketStage("after SPH forces");

        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: updatePositionsKernel);
        if (debugThisStep)
            LogBucketStage("after UpdatePositions");

        if (bucket != null && bucket.isActiveAndEnabled)
        {
            if (bucket.useMeteredNozzleFlow)
            {
                int nozzleDropsThisStep = ConsumeNozzleDropBudget(stepDt);
                nozzleDropCounterReset[0] = 0;
                nozzleDropCounterBuffer.SetData(nozzleDropCounterReset);
                bucketCollisionCompute.SetInt("nozzleMaxDropsThisStep", nozzleDropsThisStep);
            }

            ComputeHelper.Dispatch(bucketCollisionCompute, particles.Count, kernelIndex: bucketKernel);
        }
        if (debugThisStep)
            LogBucketStage("after bucket collision");

        CapturePreviousBucketFrame();
        if (planeCollision != null && planeCollision.isActiveAndEnabled)
        {
            ComputeHelper.Dispatch(planeCollisionCompute, particles.Count, kernelIndex: planeKernel);
        }

        if (GPUSimulation)
        {
            return;
        }



        if (showContainer)
        {
            return;
        }

        velocityBuffer.GetData(velocities);
        positionBuffer.GetData(positions);
        particalStateBuffer.GetData(particleState);

        
        for(int i=0;i<particles.Count;i++)
        {
            if(!particles[i].OnPlane)
            {

                if(bucket != null && particles[i].IsinsidetheBucket)
                    particles[i].IsinsidetheBucket =bucket.Constrain(ref positions[i], ref velocities[i] );

                if(planeCollision != null)
                if(planeCollision.Constrain(ref positions[i] , ref velocities[i], viscosityStrength , particles[i].OnPlane)){
                    // velocities[i] = Vector3.zero;
                    particles[i].OnPlane = true;
                }
                particles[i].position = positions[i];
                particles[i].velocity = velocities[i];
                // 1 = landed on plane (CPU handled), 0 = fluid inside bucket (full SPH),
                // 2 = dropping/free-fall (gravity only, no SPH so the nozzle stream
                // doesn't build up pressure and explode).
                particleState[i] = particles[i].OnPlane
                    ? 1
                    : (particles[i].IsinsidetheBucket ? 0 : 2);
            }
            else
            {
                if (velocities[i].magnitude > 0.001f)
                {
                    // Apply damping ONLY to horizontal velocity
                    velocities[i].x *= planeCollision.damping ;
                    velocities[i].z *= planeCollision.damping ;

                    Vector3 horizontalVel = new Vector3(velocities[i].x, 0, velocities[i].z);
                    if (horizontalVel.magnitude < 0.001f)
                    {
                        velocities[i].x = 0;
                        velocities[i].z = 0;
                    }
                    // Apply damping ONLY to vertical velocity
                    velocities[i].y += gravity * stepDt;
                    planeCollision.Constrain(ref positions[i] , ref velocities[i], viscosityStrength , particles[i].OnPlane);
                }

                particles[i].position = positions[i];
                particles[i].velocity = velocities[i];
                particleState[i] = particles[i].OnPlane ? 1 : 0;
                                
                
            }
        }
        

        particalStateBuffer.SetData(particleState);
        positionBuffer.SetData(positions);
        velocityBuffer.SetData(velocities);
    }
    [Header("Time")]
    [Tooltip("Overall speed of the simulation. 1 = real time. Raise for a faster pour, lower for slow motion.")]
    public float simulationSpeed = 1f;
    [Tooltip("Largest physics step allowed (seconds). Clamps the frame time so a hitch/low fps can't blow up the SPH. 0.02 = safe down to ~50 fps.")]
    public float maxTimestep = 0.02f;

    // void Update()
    // {
    //     // One physics step per rendered frame (one GPU readback = fast), but
    //     // integrate with the REAL frame time so motion runs at real-world speed
    //     // instead of the fixed 0.005s the shader used before (which looked like
    //     // ~30% slow motion at 60 fps). Clamp it so a frame hitch stays stable.
    //     float stepDt = Mathf.Min(Time.deltaTime, maxTimestep) * simulationSpeed;
    //     SimulateGPU(stepDt);
    // }

    // public void Simulate(float dt)
    // {
    //     ApplyExternalForces(dt);
    //     PredictPositions(dt);
    //     BuildGrid();
    //     for (int iteration = 0; iteration < solverIterations; iteration++)
    //     {
    //         ComputeDensity();
    //         ComputePressure();
    //         ApplyPressureForces(dt);
    //     }
    //     ApplyViscosity(dt);
    //     Integrate(dt);
    // }

    void ApplyExternalForces(float dt)
    {
        foreach (var p in particles)
        {
            p.velocity += Vector3.up * gravity * dt;
            p.velocity += bucket.Velocity * 0.1f;
        }
    }

    void PredictPositions(float dt)
    {
        foreach (var p in particles)
        {
            p.predictedPosition =
                p.position +
                p.velocity * dt;
        }
    }


    void ComputePressure()
    {
        foreach (var p in particles)
        {
            p.pressure =
                (p.density - targetDensity)
                * pressureMultiplier;

            p.nearPressure =
                p.nearDensity
                * nearPressureMultiplier;
        }
    }

    // void Integrate(float dt)
    // {
    //     foreach (var p in particles)
    //     {
    //         p.position += p.velocity * dt;

    //         if (bucket != null)
    //         bucket.Constrain(ref p.position, ref p.velocity);

    //         if(planeCollision != null)
    //         planeCollision.Constrain(ref p.position , ref p.velocity);
    //     }
    // }

    Vector3Int GetCell(Vector3 pos)
    {
        float cellSize = smoothingRadius;

        return new Vector3Int(
            Mathf.FloorToInt(pos.x / cellSize),
            Mathf.FloorToInt(pos.y / cellSize),
            Mathf.FloorToInt(pos.z / cellSize)
        );
    }

    void BuildGrid()
    {
        grid.Clear();

        foreach (var p in particles)
        {
            Vector3Int cell =
                GetCell(p.predictedPosition);

            if (!grid.ContainsKey(cell))
            {
                grid[cell] =
                    new List<SPHParticle>();
            }

            grid[cell].Add(p);
        }
    }

    List<SPHParticle> GetNeighbors(SPHParticle p)
    {
        List<SPHParticle> neighbors =
            new List<SPHParticle>();

        Vector3Int cell =
            GetCell(p.predictedPosition);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3Int nearbyCell =
                        cell +
                        new Vector3Int(x, y, z);

                    if (grid.TryGetValue(
                        nearbyCell,
                        out var cellParticles))
                    {
                        neighbors.AddRange(
                            cellParticles);
                    }
                }
            }
        }

        return neighbors;
    }

    void ResolveBoxCollision(ref Vector3 pos, ref Vector3 vel)
    {
        Quaternion rotation = Quaternion.Euler(boxRotation);
        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        Vector3 half = boxSize * 0.5f;

        Vector3 localPos = inverseRotation * (pos - boxCenter);
        Vector3 localVel = inverseRotation * vel;

        if (localPos.x < -half.x)
        {
            localPos.x = -half.x;
            localVel.x *= -wallBounce;
        }
        else if (localPos.x > half.x)
        {
            localPos.x = half.x;
            localVel.x *= -wallBounce;
        }

        if (localPos.y < -half.y)
        {
            localPos.y = -half.y;
            localVel.y *= -wallBounce;
        }
        else if (localPos.y > half.y)
        {
            localPos.y = half.y;
            localVel.y *= -wallBounce;
        }

        if (localPos.z < -half.z)
        {
            localPos.z = -half.z;
            localVel.z *= -wallBounce;
        }
        else if (localPos.z > half.z)
        {
            localPos.z = half.z;
            localVel.z *= -wallBounce;
        }

        pos = boxCenter + rotation * localPos;
        vel = rotation * localVel;
    }
    public List<SPHParticle> GetParticles()
    {
        return particles;
    }
    void OnDrawGizmos()
    {
        if (!showContainer)
            return;

        DrawRotatedBoxLines();

        // if (particles == null)
        //     return;

        // Quaternion inverseRotation = Quaternion.Inverse(Quaternion.Euler(boxRotation));
        // Vector3 half = boxSize * 0.5f;

        // foreach (var p in particles)
        // {
        //     Vector3 localPos = inverseRotation * (p.position - boxCenter);
        //     bool inside =
        //         Mathf.Abs(localPos.x) <= half.x &&
        //         Mathf.Abs(localPos.y) <= half.y &&
        //         Mathf.Abs(localPos.z) <= half.z;

        //     Gizmos.color = inside ? Color.blue : Color.red;
        //     Gizmos.DrawSphere(p.position, 0.08f);
        // }
    }

    void DrawRotatedBoxLines()
    {
        Quaternion rotation = Quaternion.Euler(boxRotation);
        Vector3 half = boxSize * 0.5f;

        Vector3[] corners =
        {
            new Vector3(-half.x, -half.y, -half.z),
            new Vector3(half.x, -half.y, -half.z),
            new Vector3(half.x, -half.y, half.z),
            new Vector3(-half.x, -half.y, half.z),
            new Vector3(-half.x, half.y, -half.z),
            new Vector3(half.x, half.y, -half.z),
            new Vector3(half.x, half.y, half.z),
            new Vector3(-half.x, half.y, half.z)
        };

        for (int i = 0; i < corners.Length; i++)
            corners[i] = boxCenter + rotation * corners[i];

        Gizmos.color = Color.yellow;

        DrawLine(corners, 0, 1);
        DrawLine(corners, 1, 2);
        DrawLine(corners, 2, 3);
        DrawLine(corners, 3, 0);

        DrawLine(corners, 4, 5);
        DrawLine(corners, 5, 6);
        DrawLine(corners, 6, 7);
        DrawLine(corners, 7, 4);

        DrawLine(corners, 0, 4);
        DrawLine(corners, 1, 5);
        DrawLine(corners, 2, 6);
        DrawLine(corners, 3, 7);
    }

    void DrawLine(Vector3[] points, int start, int end)
    {
        Gizmos.DrawLine(points[start], points[end]);
    }

    public ComputeBuffer GetPositionBuffer()
    {
        return positionBuffer;
    }

    float GetNozzleFeedRadius()
    {
        if (bucket == null)
            return 0f;

        float feedRadius = bucket.radius * Mathf.Clamp01(bucket.nozzleFeedRadiusFraction);
        return Mathf.Clamp(
            Mathf.Max(bucket.nozzleRadius, feedRadius),
            0f,
            Mathf.Max(0f, bucket.radius)
        );
    }

    int ConsumeNozzleDropBudget(float stepDt)
    {
        if (bucket == null || particles.Count == 0)
            return 0;

        float radius = Mathf.Max(0f, bucket.nozzleRadius);
        float referenceRadius = Mathf.Max(0.0001f, bucket.nozzleFlowReferenceRadius);
        float flowScale = (radius * radius) / (referenceRadius * referenceRadius);
        float particlesPerSecond = Mathf.Max(0f, bucket.nozzleParticlesPerSecondAtReferenceRadius) * flowScale;

        nozzleDropBudget += particlesPerSecond * Mathf.Max(0f, stepDt);
        nozzleDropBudget = Mathf.Min(nozzleDropBudget, particles.Count);

        int dropsThisStep = Mathf.Min(Mathf.FloorToInt(nozzleDropBudget), particles.Count);
        nozzleDropBudget -= dropsThisStep;
        return dropsThisStep;
    }

    bool ShouldDebugBucketStages()
    {
        if (!debugBucketStages || bucket == null || particles.Count == 0)
            return false;

        if (positionBuffer == null || !positionBuffer.IsValid() ||
            velocityBuffer == null || !velocityBuffer.IsValid() ||
            particalStateBuffer == null || !particalStateBuffer.IsValid())
        {
            return false;
        }

        debugBucketStageFrame++;
        return debugBucketStageFrame % Mathf.Max(1, debugBucketStageInterval) == 0;
    }

    void LogBucketStage(string stage)
    {
        positionBuffer.GetData(positions);
        velocityBuffer.GetData(velocities);
        particalStateBuffer.GetData(particleState);

        Transform bucketTransform = bucket.transform;
        Vector3 bucketPosition = bucketTransform.position;
        Vector3 bucketRight = bucketTransform.right;
        Vector3 bucketUp = bucketTransform.up;
        Vector3 bucketForward = bucketTransform.forward;

        int inBucket = 0;
        int dropping = 0;
        int onPlane = 0;
        int nearWall = 0;
        int atWall = 0;
        int inNozzle = 0;
        int outsideShape = 0;

        Vector2 localCenterXZ = Vector2.zero;
        float radiusSum = 0f;
        float maxRadius = 0f;
        float speedSum = 0f;
        float maxSpeedSeen = 0f;
        float outwardVelocitySum = 0f;
        float maxOutwardVelocity = 0f;
        float minCollisionY = float.MaxValue;
        float maxCollisionY = float.MinValue;

        float halfHeight = bucket.height * 0.2f;
        float nearWallRadius = bucket.radius * 0.85f;
        float atWallRadius = bucket.radius * 0.98f;
        float bucketFrameDelta = (bucketPosition - previousBucketPosition).magnitude;
        float bucketFrameAngle = Vector3.Angle(previousBucketUp, bucketUp);
        float velocityCarryDistance = bucket.Velocity.magnitude * Mathf.Min(Time.deltaTime, maxTimestep) * simulationSpeed;

        for (int i = 0; i < particleState.Length; i++)
        {
            int state = particleState[i];
            if (state == 1)
            {
                onPlane++;
                continue;
            }

            if (state == 2)
            {
                dropping++;
                continue;
            }

            inBucket++;

            Vector3 offset = positions[i] - bucketPosition;
            Vector3 localPos = new Vector3(
                Vector3.Dot(offset, bucketRight),
                Vector3.Dot(offset, bucketUp),
                Vector3.Dot(offset, bucketForward)
            );
            Vector3 localVel = new Vector3(
                Vector3.Dot(velocities[i], bucketRight),
                Vector3.Dot(velocities[i], bucketUp),
                Vector3.Dot(velocities[i], bucketForward)
            );

            float collisionLocalY = localPos.y - bucket.collisionYOffset;
            minCollisionY = Mathf.Min(minCollisionY, collisionLocalY);
            maxCollisionY = Mathf.Max(maxCollisionY, collisionLocalY);
            Vector2 xz = new Vector2(localPos.x, localPos.z);
            float radius = xz.magnitude;
            float speed = localVel.magnitude;
            float outwardVelocity = 0f;
            if (radius > 0.0001f)
            {
                outwardVelocity = Mathf.Max(0f, Vector2.Dot(new Vector2(localVel.x, localVel.z), xz / radius));
            }

            localCenterXZ += xz;
            radiusSum += radius;
            maxRadius = Mathf.Max(maxRadius, radius);
            speedSum += speed;
            maxSpeedSeen = Mathf.Max(maxSpeedSeen, speed);
            outwardVelocitySum += outwardVelocity;
            maxOutwardVelocity = Mathf.Max(maxOutwardVelocity, outwardVelocity);

            if (radius >= nearWallRadius)
                nearWall++;
            if (radius >= atWallRadius)
                atWall++;
            if (radius <= bucket.nozzleRadius &&
                collisionLocalY < -halfHeight + bucket.nozzleCaptureDepth)
            {
                inNozzle++;
            }
            if (radius > bucket.radius ||
                collisionLocalY < -halfHeight ||
                collisionLocalY > halfHeight)
            {
                outsideShape++;
            }
        }

        int count = Mathf.Max(1, inBucket);
        localCenterXZ /= count;

        Debug.Log(
            $"[BucketDebug] {stage} " +
            $"inBucket={inBucket} dropping={dropping} plane={onPlane} " +
            $"centerXZ={localCenterXZ} centerRatio={(localCenterXZ.magnitude / Mathf.Max(bucket.radius, 0.0001f)):0.00} " +
            $"avgR={(radiusSum / count):0.000} maxR={maxRadius:0.000} radius={bucket.radius:0.000} " +
            $"nearWall={nearWall} atWall={atWall} outside={outsideShape} nozzle={inNozzle} " +
            $"yRange=({minCollisionY:0.000},{maxCollisionY:0.000}) halfH={halfHeight:0.000} " +
            $"avgSpeed={(speedSum / count):0.000} maxSpeed={maxSpeedSeen:0.000} " +
            $"avgOut={(outwardVelocitySum / count):0.000} maxOut={maxOutwardVelocity:0.000} " +
            $"bucketVel={bucket.Velocity.magnitude:0.000} motionInherit={bucket.bucketMotionInheritance:0.00} " +
            $"frameDelta={bucketFrameDelta:0.0000} frameAngle={bucketFrameAngle:0.00} velCarry={velocityCarryDistance:0.0000}"
        );
    }

    void CapturePreviousBucketFrame()
    {
        if (bucket == null)
            return;

        previousBucketPosition = bucket.transform.position;
        previousBucketRight = bucket.transform.right;
        previousBucketUp = bucket.transform.up;
        previousBucketForward = bucket.transform.forward;
        hasPreviousBucketFrame = true;
    }

    void OnDestroy()
    {
        positionBuffer?.Release();
        predictedBuffer?.Release();
        velocityBuffer?.Release();
        densityBuffer?.Release();
        spatialIndicesBuffer?.Release();
        spatialOffsetsBuffer?.Release();
        particalStateBuffer?.Release();
        nozzleDropCounterBuffer?.Release();

        positionBuffer = null;
        predictedBuffer = null;
        velocityBuffer = null;
        densityBuffer = null;
        spatialIndicesBuffer = null;
        spatialOffsetsBuffer = null;
        particalStateBuffer = null;
        nozzleDropCounterBuffer = null;
    }
}
