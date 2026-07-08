using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public enum BucketParticleSpawnMode
{
    CubeGrid = 0,
    Cylinder = 1
}

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
    public int maxParticles = 110000;
    [Tooltip("CubeGrid = original git spawn (gridSize³). Cylinder = round fill capped by bucket volume.")]
    public BucketParticleSpawnMode bucketSpawnMode = BucketParticleSpawnMode.CubeGrid;
    int activeParticleCount;
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
    public bool enableSPHForces = true;
    [Tooltip("Allow landed plane paint particles to participate in SPH density/pressure/viscosity. Disable for better FPS.")]
    public bool enablePlaneSPHForces = true;
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

    [Header("Color Mixing Settings")]
    public bool enableColorMixing = true;
    public float colorMixRadius = 0.05f;
    public float colorMixSpeed = 2f;
    public int maxColorMixNeighbors = 16;

    public int ParticleCount => activeParticleCount;
    public bool IsReady =>
        positionBuffer != null &&
        positionBuffer.IsValid() &&
        activeParticleCount > 0;
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
    ComputeBuffer rimSpillCounterBuffer;
    ComputeBuffer colorBuffer;
    int externalKernel;
    int clearSpatialOffsetsKernel;
    int updateSpatialHashKernel;
    int calculateDensitiesKernel;
    int calculatePressureForceKernel;
    int calculateViscosityKernel;
    int updatePositionsKernel;
    int mixingColorKernel;
    int bucketKernel;
    int bucketMotionKernel;
    int planeMotionKernel;
    int planeKernel;
    int[] particleState;
    int debugBucketStageFrame;
    bool hasPreviousBucketFrame;
    Vector3 previousBucketPosition;
    Vector3 previousBucketRight;
    Vector3 previousBucketUp;
    Vector3 previousBucketForward;
    Vector4[] particleColors;
    bool hasPreviousPlaneFrame;
    Vector3 previousPlanePosition;
    Vector3 previousPlaneRight;
    Vector3 previousPlaneUp;
    Vector3 previousPlaneForward;
    readonly uint[] nozzleDropCounterReset = new uint[1];
    readonly uint[] rimSpillCounterReset = new uint[1];
    float nozzleDropBudget;
    float rimSpillBudget;
    FluidParticleRenderer particleRenderer = new FluidParticleRenderer();
    // [Header("Particle Color")]
    // public Color initialParticleColor = new Color(1f, 0.08f, 0.04f, 1f);
    #region Kernels

    #endregion

    void Start()
    {
        InitializeParticles();
        InitializeComputeShader();
    }

    void Update()
    {
        if (!showContainer || !GPUSimulation || !IsReady)
            return;

        float stepDt = Mathf.Min(Time.deltaTime, maxTimestep) * simulationSpeed;
        SimulateGPU(stepDt);
    }

    void InitializeParticles()
    {
        particles.Clear();
        ApplySmoothingRadiusTuning();

        float spacing = Mathf.Max(0.001f, particleSpacing);

        if (bucket != null && !showContainer)
        {
            if (bucketSpawnMode == BucketParticleSpawnMode.Cylinder)
                InitializeBucketCylinderParticles(spacing);
            else
                InitializeBucketCubeGridParticles(spacing);
        }
        else
            InitializeBoxGridParticles(spacing);

        if (particles.Count == 0)
        {
            Debug.LogWarning("SPHManager: no particles were spawned. Check gridSize, bucket size, and particle spacing.");
            activeParticleCount = 0;
            positions = new Vector3[maxParticles];
            velocities = new Vector3[maxParticles];
            predictedPositions = new Vector3[maxParticles];
            particleState = new int[maxParticles];
            particleColors = new Vector4[maxParticles];
            return;
        }

        maxParticles = Mathf.Max(maxParticles, particles.Count);
        CapturePreviousBucketFrame();
        positions = new Vector3[maxParticles];
        velocities = new Vector3[maxParticles];
        predictedPositions = new Vector3[maxParticles];
        particleState = new int[maxParticles];
        particleColors = new Vector4[maxParticles];
        activeParticleCount = particles.Count;
        for(int i=0;i<activeParticleCount;i++)
        {
            positions[i] = particles[i].position;
            velocities[i] = particles[i].velocity;
            predictedPositions[i] = particles[i].position;
            particleState[i] = particles[i].OnPlane ? 1 : 0;
            particleColors[i] = ColorToVector(particles[i].color);
        }
    }

    void InitializeBoxGridParticles(float spacing)
    {
        Vector3 startCenter = showContainer ? boxCenter : transform.position;
        Quaternion containerRotation = Quaternion.Euler(boxRotation);

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 localPos = new Vector3(
                        (x - gridSize * 0.5f) * spacing,
                        (y - gridSize * 0.5f) * spacing,
                        (z - gridSize * 0.5f) * spacing
                    );

                    Vector3 worldPos = startCenter + containerRotation * localPos;
                    AddSpawnedParticle(worldPos);
                }
            }
        }
    }

    void InitializeBucketCubeGridParticles(float spacing)
    {
        int targetCount = Mathf.Min(gridSize * gridSize * gridSize, maxParticles);
        float maxRadius = bucket.FluidWallRadius * 0.98f;
        float floorY = bucket.FluidFloorLocalY + spacing * 0.5f;
        float ceilingY = bucket.FluidCeilingLocalY - spacing * 0.25f;

        for (int x = 0; x < gridSize && particles.Count < targetCount; x++)
        {
            for (int y = 0; y < gridSize && particles.Count < targetCount; y++)
            {
                for (int z = 0; z < gridSize && particles.Count < targetCount; z++)
                {
                    Vector3 localPos = new Vector3(
                        (x - gridSize * 0.5f) * spacing,
                        Mathf.Lerp(floorY, ceilingY, y / Mathf.Max(1f, gridSize - 1f)),
                        (z - gridSize * 0.5f) * spacing
                    );

                    if (localPos.x * localPos.x + localPos.z * localPos.z > maxRadius * maxRadius)
                        continue;

                    Vector3 worldPos = bucket.transform.TransformPoint(localPos);
                    AddSpawnedParticle(worldPos);
                }
            }
        }
    }

    void InitializeBucketCylinderParticles(float spacing)
    {
        float maxRadius = bucket.FluidWallRadius * 0.98f;
        float floorY = bucket.FluidFloorLocalY + spacing * 0.5f;
        float ceilingY = bucket.FluidCeilingLocalY - spacing * 0.25f;
        if (ceilingY <= floorY)
            ceilingY = floorY + spacing;

        int maxLayers = Mathf.Max(1, gridSize);
        int xzExtent = Mathf.Max(1, Mathf.CeilToInt(maxRadius / spacing));
        int targetCount = gridSize * gridSize * gridSize;

        for (int layer = 0; layer < maxLayers && particles.Count < targetCount; layer++)
        {
            float y = floorY + layer * spacing;
            if (y > ceilingY)
                break;

            float xzOffset = (layer & 1) == 1 ? spacing * 0.5f : 0f;

            for (int ix = -xzExtent; ix <= xzExtent && particles.Count < targetCount; ix++)
            {
                for (int iz = -xzExtent; iz <= xzExtent && particles.Count < targetCount; iz++)
                {
                    float x = ix * spacing + xzOffset;
                    float z = iz * spacing;
                    if (x * x + z * z > maxRadius * maxRadius)
                        continue;

                    Vector3 localPos = new Vector3(x, y, z);
                    Vector3 worldPos = bucket.transform.TransformPoint(localPos);
                    AddSpawnedParticle(worldPos);
                }
            }
        }
    }

    void AddSpawnedParticle(Vector3 worldPos)
    {
        SPHParticle p = new SPHParticle();
        p.position = worldPos;
        p.velocity = Vector3.zero;
        p.OnPlane = false;
        p.IsinsidetheBucket = true;
        p.color = particleRenderer.paintColor;
        particles.Add(p);
    }

    int SyncActiveParticleCountWithList()
    {
        if (particles == null)
            return 0;

        if (activeParticleCount > particles.Count)
        {
            Debug.LogWarning(
                $"SPHManager: activeParticleCount ({activeParticleCount}) exceeded particles list ({particles.Count}). Clamping to list size.");
            activeParticleCount = particles.Count;
        }

        return activeParticleCount;
    }

    void OnValidate()
    {
        maxParticles = Mathf.Max(1, maxParticles);
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
        mixingColorKernel = simulationShader.FindKernel("MixingColor");
        if (bucketCollisionCompute != null)
        {
            bucketMotionKernel = bucketCollisionCompute.FindKernel("ApplyBucketMotion");
            bucketKernel = bucketCollisionCompute.FindKernel("ResolveBucketCollision");
        }

        if (planeCollisionCompute != null)
        {
            planeMotionKernel = planeCollisionCompute.FindKernel("ApplyPlaneMotion");
            planeKernel = planeCollisionCompute.FindKernel("ResolvePlaneCollision");
        }
        positionBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(maxParticles);
        velocityBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(maxParticles);
        predictedBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(maxParticles);
        particalStateBuffer =ComputeHelper.CreateStructuredBuffer<int>(maxParticles);
        densityBuffer =ComputeHelper.CreateStructuredBuffer<Vector2>(maxParticles);
        spatialIndicesBuffer =ComputeHelper.CreateStructuredBuffer<SpatialIndex>(maxParticles);
        spatialOffsetsBuffer =ComputeHelper.CreateStructuredBuffer<uint>(maxParticles);
        colorBuffer = ComputeHelper.CreateStructuredBuffer<Vector4>(maxParticles);
        nozzleDropCounterBuffer = ComputeHelper.CreateStructuredBuffer<uint>(1);
        rimSpillCounterBuffer = ComputeHelper.CreateStructuredBuffer<uint>(1);
        positionBuffer.SetData(positions);
        velocityBuffer.SetData(velocities);
        predictedBuffer.SetData(predictedPositions);
        particalStateBuffer.SetData(particleState);
        colorBuffer.SetData(particleColors);
        nozzleDropCounterBuffer.SetData(nozzleDropCounterReset);
        rimSpillCounterBuffer.SetData(rimSpillCounterReset);

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
        ComputeHelper.SetBuffer(simulationShader,predictedBuffer,"PredictedPositions",mixingColorKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialIndicesBuffer,"SpatialIndices",mixingColorKernel);
        ComputeHelper.SetBuffer(simulationShader,spatialOffsetsBuffer,"SpatialOffsets",mixingColorKernel);
        ComputeHelper.SetBuffer(simulationShader,colorBuffer,"Colors",mixingColorKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",externalKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",clearSpatialOffsetsKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",updateSpatialHashKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculateDensitiesKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculatePressureForceKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculateViscosityKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",updatePositionsKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",mixingColorKernel);
        if (bucketCollisionCompute != null)
        {
            ComputeHelper.SetBuffer(bucketCollisionCompute,positionBuffer,"Positions",bucketMotionKernel, bucketKernel);
            ComputeHelper.SetBuffer(bucketCollisionCompute,velocityBuffer,"Velocities",bucketMotionKernel, bucketKernel);
            ComputeHelper.SetBuffer(bucketCollisionCompute,particalStateBuffer,"ParticleState",bucketMotionKernel, bucketKernel);
            ComputeHelper.SetBuffer(bucketCollisionCompute,nozzleDropCounterBuffer,"NozzleDropCounter",bucketKernel);
            ComputeHelper.SetBuffer(bucketCollisionCompute,rimSpillCounterBuffer,"RimSpillCounter",bucketKernel);
        }

        if (planeCollisionCompute != null)
        {
            ComputeHelper.SetBuffer(planeCollisionCompute,positionBuffer,"Positions",planeMotionKernel, planeKernel);
            ComputeHelper.SetBuffer(planeCollisionCompute,velocityBuffer,"Velocities",planeMotionKernel, planeKernel);
            ComputeHelper.SetBuffer(planeCollisionCompute,particalStateBuffer,"ParticleState",planeMotionKernel, planeKernel);
        }
        SetComputeShaderParameters();
    }
    void SetComputeShaderParameters()
    {
        ApplySmoothingRadiusTuning();

        simulationShader.SetFloat("gravity",gravity);
        simulationShader.SetFloat("deltaTime",dt);
        simulationShader.SetInt("numParticles",activeParticleCount);
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
        simulationShader.SetFloat("colorMixRadius", colorMixRadius);
        simulationShader.SetFloat("colorMixSpeed", colorMixSpeed);
        simulationShader.SetInt("maxColorMixNeighbors", maxColorMixNeighbors);
        simulationShader.SetInt("enablePlaneSPHForces", enablePlaneSPHForces ? 1 : 0);

        if (bucketCollisionCompute != null && bucket != null)
        {
            bucketCollisionCompute.SetInt("numParticles", activeParticleCount);
            bucketCollisionCompute.SetFloat("deltaTime", dt);
            bucketCollisionCompute.SetFloat("bucketRadius", bucket.FluidWallRadius);
            bucketCollisionCompute.SetFloat("bucketHeight", bucket.CylinderHeightSpan);
            bucketCollisionCompute.SetFloat("bucketCylinderBottomLocal", bucket.FluidFloorLocalY);
            bucketCollisionCompute.SetFloat("bucketCylinderTopLocal", bucket.FluidCeilingLocalY);
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
            bucketCollisionCompute.SetInt("bucketContactMode", (int)bucket.ContactMode);
            bucketCollisionCompute.SetFloat("bucketTwistAngle", bucket.TwistAngleRadians);
            bucketCollisionCompute.SetFloat("spillTiltDegrees", bucket.spillTiltDegrees);
            bucketCollisionCompute.SetFloat("rimSpillSpeed", bucket.rimSpillSpeed);
            bucketCollisionCompute.SetFloat("rimSpillFillFraction", bucket.rimSpillFillFraction);
            bucketCollisionCompute.SetInt("allowRimSpill", bucket.enableRimSpill ? 1 : 0);
            bucketCollisionCompute.SetInt("enableRimSpillFromTilt", bucket.enableRimSpillFromTilt ? 1 : 0);
            bucketCollisionCompute.SetInt("rimSpillMaxDropsThisStep", 0);
        }

        if (planeCollisionCompute != null && planeCollision != null && planeCollision.plane != null)
        {
            Transform planeTransform = planeCollision.plane;
            planeCollisionCompute.SetInt("numParticles", activeParticleCount);
            planeCollisionCompute.SetFloat("deltaTime", dt);
            planeCollisionCompute.SetVector("planePosition", planeTransform.position);
            planeCollisionCompute.SetVector("planeNormal", planeCollision.GetPlaneNormal());
            planeCollisionCompute.SetVector("planeRight", planeTransform.right);
            planeCollisionCompute.SetVector("planeUp", planeTransform.up);
            planeCollisionCompute.SetVector("planeForward", planeTransform.forward);
            if (!hasPreviousPlaneFrame)
            {
                CapturePreviousPlaneFrame();
            }
            planeCollisionCompute.SetVector("previousPlanePosition", previousPlanePosition);
            planeCollisionCompute.SetVector("previousPlaneRight", previousPlaneRight);
            planeCollisionCompute.SetVector("previousPlaneUp", previousPlaneUp);
            planeCollisionCompute.SetVector("previousPlaneForward", previousPlaneForward);
            planeCollisionCompute.SetFloat("surfaceWetness", planeCollision.SurfaceWetness);
            planeCollisionCompute.SetVector("gravityWorld", new Vector3(0f, gravity, 0f));
            SurfaceMaterial mat = planeCollision.surfaceMaterial;
            if (mat != null)
            {
                planeCollisionCompute.SetFloat("wetnessSlideFactor", mat.wetnessSlideFactor);
                planeCollisionCompute.SetFloat("paintViscosity", mat.paintViscosity);
                planeCollisionCompute.SetFloat("stopSpeedThreshold", mat.stopSpeedThreshold);
                planeCollisionCompute.SetFloat("planeRestitution", mat.restitution);
                planeCollisionCompute.SetFloat("planeStaticFriction", mat.staticFriction);
                planeCollisionCompute.SetFloat("planeDynamicFriction", mat.dynamicFriction);
                planeCollisionCompute.SetFloat("planeSpread", mat.spread);
                planeCollisionCompute.SetFloat("planeAbsorption", mat.absorption);
            }
            else
            {
                planeCollisionCompute.SetFloat("wetnessSlideFactor", 0.5f);
                planeCollisionCompute.SetFloat("paintViscosity", 2f);
                planeCollisionCompute.SetFloat("stopSpeedThreshold", 0.015f);
                planeCollisionCompute.SetFloat("planeRestitution", 0.3f);
                planeCollisionCompute.SetFloat("planeStaticFriction", 0.7f);
                planeCollisionCompute.SetFloat("planeDynamicFriction", 0.4f);
                planeCollisionCompute.SetFloat("planeSpread", 0.06f);
                planeCollisionCompute.SetFloat("planeAbsorption", 0.5f);
            }

        }
    }
    public void SimulateGPU(float stepDt)
    {
        SetComputeShaderParameters();
        bool debugThisStep = ShouldDebugBucketStages();
        simulationShader.SetFloat("deltaTime", stepDt);
        if (bucketCollisionCompute != null)
            bucketCollisionCompute.SetFloat("deltaTime", stepDt);
        if (planeCollisionCompute != null)
            planeCollisionCompute.SetFloat("deltaTime", stepDt);

        bool gpuPlaneStep = planeCollision != null
            && planeCollision.isActiveAndEnabled
            && GPUSimulation
            && planeCollisionCompute != null
            && planeCollision.plane != null
            && planeCollision.surfaceMaterial != null;

        if (gpuPlaneStep)
        {
            ComputeHelper.Dispatch(planeCollisionCompute, activeParticleCount, kernelIndex: planeMotionKernel);
        }

        if (debugThisStep)
            LogBucketStage("before bucket motion");

        if (bucketCollisionCompute != null && bucket != null && bucket.isActiveAndEnabled)
        {
            ComputeHelper.Dispatch(bucketCollisionCompute, activeParticleCount, kernelIndex: bucketMotionKernel);
        }
        if (debugThisStep)
            LogBucketStage("after bucket motion");

        ComputeHelper.Dispatch(simulationShader, activeParticleCount, kernelIndex: externalKernel);
        ComputeHelper.Dispatch(simulationShader, activeParticleCount, kernelIndex: clearSpatialOffsetsKernel);
        ComputeHelper.Dispatch(simulationShader, activeParticleCount, kernelIndex: updateSpatialHashKernel);

        if (enableColorMixing)
        {
            ComputeHelper.Dispatch(simulationShader, activeParticleCount, kernelIndex: mixingColorKernel);
        }

        if (enableSPHForces)
        {
            ComputeHelper.Dispatch(simulationShader, activeParticleCount, kernelIndex: calculateDensitiesKernel);
            ComputeHelper.Dispatch(simulationShader, activeParticleCount, kernelIndex: calculatePressureForceKernel);
            ComputeHelper.Dispatch(simulationShader, activeParticleCount, kernelIndex: calculateViscosityKernel);

            if (debugThisStep)
                LogBucketStage("after SPH forces");
        }

        ComputeHelper.Dispatch(simulationShader, activeParticleCount, kernelIndex: updatePositionsKernel);
        if (debugThisStep)
            LogBucketStage("after UpdatePositions");

        if (bucketCollisionCompute != null && bucket != null && bucket.isActiveAndEnabled)
        {
            bool blockNozzle = bucket.ContactMode == BucketContactMode.Flat;
            int nozzleDropsThisStep = blockNozzle ? 0 : ConsumeNozzleDropBudget(stepDt);
            int rimSpillDropsThisStep = ConsumeRimSpillBudget(stepDt);

            nozzleDropCounterReset[0] = 0;
            rimSpillCounterReset[0] = 0;
            nozzleDropCounterBuffer.SetData(nozzleDropCounterReset);
            rimSpillCounterBuffer.SetData(rimSpillCounterReset);
            bucketCollisionCompute.SetInt("nozzleMaxDropsThisStep", nozzleDropsThisStep);
            bucketCollisionCompute.SetInt("rimSpillMaxDropsThisStep", rimSpillDropsThisStep);
            bucketCollisionCompute.SetInt("bucketContactMode", (int)bucket.ContactMode);
            bucketCollisionCompute.SetFloat("bucketTwistAngle", bucket.TwistAngleRadians);

            ComputeHelper.Dispatch(bucketCollisionCompute, activeParticleCount, kernelIndex: bucketKernel);
        }
        if (debugThisStep)
            LogBucketStage("after bucket collision");

        CapturePreviousBucketFrame();

        if (gpuPlaneStep && planeCollisionCompute != null && planeCollision != null && planeCollision.isActiveAndEnabled)
        {
            ComputeHelper.Dispatch(planeCollisionCompute, activeParticleCount, kernelIndex: planeKernel);
            CapturePreviousPlaneFrame();
        }

        if (GPUSimulation)
        {
            return;
        }

        SimulateCpuCollisionFallback(stepDt);
    }

    void SimulateCpuCollisionFallback(float stepDt)
    {
        if (showContainer)
        {
            return;
        }

        velocityBuffer.GetData(velocities);
        positionBuffer.GetData(positions);
        particalStateBuffer.GetData(particleState);

        int count = SyncActiveParticleCountWithList();
        if (count == 0)
            return;

        for(int i=0;i<count;i++)
        {
            if (!particles[i].OnPlane)
            {
                if (bucket != null && particles[i].IsinsidetheBucket)
                    particles[i].IsinsidetheBucket = bucket.Constrain(ref positions[i], ref velocities[i]);

                if (planeCollision != null)
                {
                    SurfaceCollisionMath.Result planeResult = planeCollision.Constrain(
                        ref positions[i],
                        ref velocities[i],
                        particles[i].OnPlane,
                        stepDt,
                        gravity,
                        (uint)i);

                    if (planeResult.shouldFreeze)
                        particles[i].OnPlane = true;
                }

                particles[i].position = positions[i];
                particles[i].velocity = velocities[i];
                particleState[i] = particles[i].OnPlane
                    ? 1
                    : (particles[i].IsinsidetheBucket ? 0 : 2);
            }
            else
            {
                if (planeCollision != null && velocities[i].sqrMagnitude > 1e-6f)
                {
                    SurfaceCollisionMath.Result planeResult = planeCollision.Constrain(
                        ref positions[i],
                        ref velocities[i],
                        true,
                        stepDt,
                        gravity,
                        (uint)i);

                    if (planeResult.shouldFreeze)
                        particles[i].OnPlane = true;
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

    public ComputeBuffer GetColorBuffer()
    {
        return colorBuffer;
    }

    public ComputeBuffer GetParticleStateBuffer()
    {
        return particalStateBuffer;
    }

    /// <summary>Lightweight GPU→CPU sync for rendering plane paint on the GPU simulation path.</summary>
    public void SyncParticlesForRendering()
    {
        SyncParticlesFromGpu();
    }

    public void ApplySimulationSettings(
        int newGridSize,
        float newParticleSpacing,
        float newGravity,
        float newTargetDensity,
        float newPressureMultiplier,
        float newNearPressureMultiplier,
        float newViscosityStrength,
        float newMaxSpeed,
        float newWallBounce,
        float newSimulationSpeed
        )
    {
        bool needsReinitialize =
            newGridSize != gridSize ||
            !Mathf.Approximately(newParticleSpacing, particleSpacing);

        gridSize = Mathf.Max(1, newGridSize);
        particleSpacing = Mathf.Max(0.001f, newParticleSpacing);
        gravity = newGravity;
        targetDensity = Mathf.Max(0.0001f, newTargetDensity);
        pressureMultiplier = newPressureMultiplier;
        nearPressureMultiplier = newNearPressureMultiplier;
        viscosityStrength = Mathf.Max(0f, newViscosityStrength);
        maxSpeed = Mathf.Max(0f, newMaxSpeed);
        wallBounce = Mathf.Clamp01(newWallBounce);
        simulationSpeed = Mathf.Max(0f, newSimulationSpeed);

        if (needsReinitialize && Application.isPlaying)
        {
            ReleaseComputeBuffers();
            InitializeParticles();
            InitializeComputeShader();
        }
        else if (Application.isPlaying && IsReady)
        {
            SetComputeShaderParameters();
        }
    }
    public void ApplySimulationSettings2(
        int newGridSize,
        float newParticleSpacing,
        float newGravity,
        float newTargetDensity,
        float newPressureMultiplier,
        float newNearPressureMultiplier,
        float newViscosityStrength,
        float newMaxSpeed,
        float newWallBounce,
        float newSimulationSpeed,
        float boxsizeX,
        float boxsizeY,
        float boxsizeZ,
        float boxOrientX,
        float boxOrientY,
        float boxOrientZ)
    {
        bool needsReinitialize =
            newGridSize != gridSize ||
            !Mathf.Approximately(newParticleSpacing, particleSpacing);

        gridSize = Mathf.Max(1, newGridSize);
        particleSpacing = Mathf.Max(0.001f, newParticleSpacing);
        gravity = newGravity;
        targetDensity = Mathf.Max(0.0001f, newTargetDensity);
        pressureMultiplier = newPressureMultiplier;
        nearPressureMultiplier = newNearPressureMultiplier;
        viscosityStrength = Mathf.Max(0f, newViscosityStrength);
        maxSpeed = Mathf.Max(0f, newMaxSpeed);
        wallBounce = Mathf.Clamp01(newWallBounce);
        simulationSpeed = Mathf.Max(0f, newSimulationSpeed);
        boxSize = new Vector3(boxsizeX, boxsizeY, boxsizeZ);
        boxRotation = new Vector3(boxOrientX, boxOrientY, boxOrientZ);
        if (needsReinitialize && Application.isPlaying)
        {
            ReleaseComputeBuffers();
            InitializeParticles();
            InitializeComputeShader();
        }
        else if (Application.isPlaying && IsReady)
        {
            SetComputeShaderParameters();
        }
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
        if (bucket == null || activeParticleCount == 0)
            return 0;

        float particlesPerSecond = bucket.MeteredNozzleParticlesPerSecond;

        nozzleDropBudget += particlesPerSecond * Mathf.Max(0f, stepDt);
        nozzleDropBudget = Mathf.Min(nozzleDropBudget, activeParticleCount);

        int dropsThisStep = Mathf.Min(Mathf.FloorToInt(nozzleDropBudget), activeParticleCount);
        nozzleDropBudget -= dropsThisStep;
        return dropsThisStep;
    }

    int ConsumeRimSpillBudget(float stepDt)
    {
        if (bucket == null || activeParticleCount == 0 || !bucket.enableRimSpill)
            return 0;

        bool contact = bucket.ContactMode != BucketContactMode.None;
        float tilt = Vector3.Angle(-bucket.transform.up, Vector3.down);
        bool tilted = bucket.enableRimSpillFromTilt && tilt >= bucket.spillTiltDegrees;
        if (!contact && !tilted)
            return 0;

        float particlesPerSecond = bucket.rimSpillParticlesPerSecond;
        rimSpillBudget += particlesPerSecond * Mathf.Max(0f, stepDt);
        rimSpillBudget = Mathf.Min(rimSpillBudget, activeParticleCount);

        int dropsThisStep = Mathf.Min(Mathf.FloorToInt(rimSpillBudget), activeParticleCount);
        rimSpillBudget -= dropsThisStep;
        return dropsThisStep;
    }

    bool ShouldDebugBucketStages()
    {
        if (!debugBucketStages || bucket == null || activeParticleCount == 0)
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
        if (positions == null || positions.Length != maxParticles)
            positions = new Vector3[maxParticles];
        if (velocities == null || velocities.Length != maxParticles)
            velocities = new Vector3[maxParticles];
        if (particleState == null || particleState.Length != maxParticles)
            particleState = new int[maxParticles];

        positionBuffer.GetData(positions, 0, 0, activeParticleCount);
        velocityBuffer.GetData(velocities, 0, 0, activeParticleCount);
        particalStateBuffer.GetData(particleState, 0, 0, activeParticleCount);

        int count = SyncActiveParticleCountWithList();
        if (count == 0)
            return;

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

        float floorY = bucket.CollisionFloorShiftedY;
        float ceilingY = bucket.CollisionCeilingShiftedY;
        float wallRadius = bucket.FluidWallRadius;
        float nearWallRadius = wallRadius * 0.85f;
        float atWallRadius = wallRadius * 0.98f;
        float bucketFrameDelta = (bucketPosition - previousBucketPosition).magnitude;
        float bucketFrameAngle = Vector3.Angle(previousBucketUp, bucketUp);
        float velocityCarryDistance = bucket.Velocity.magnitude * Mathf.Min(Time.deltaTime, maxTimestep) * simulationSpeed;

        for (int i = 0; i < count; i++)
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
                collisionLocalY < floorY + bucket.nozzleCaptureDepth)
            {
                inNozzle++;
            }
            if (radius > wallRadius ||
                collisionLocalY < floorY ||
                collisionLocalY > ceilingY)
            {
                outsideShape++;
            }
        }

        int inBucketCount = Mathf.Max(1, inBucket);
        localCenterXZ /= inBucketCount;

        Debug.Log(
            $"[BucketDebug] {stage} " +
            $"inBucket={inBucket} dropping={dropping} plane={onPlane} " +
            $"centerXZ={localCenterXZ} centerRatio={(localCenterXZ.magnitude / Mathf.Max(bucket.radius, 0.0001f)):0.00} " +
            $"avgR={(radiusSum / inBucketCount):0.000} maxR={maxRadius:0.000} radius={wallRadius:0.000} " +
            $"nearWall={nearWall} atWall={atWall} outside={outsideShape} nozzle={inNozzle} " +
            $"yRange=({minCollisionY:0.000},{maxCollisionY:0.000}) floorY={floorY:0.000} ceilY={ceilingY:0.000} " +
            $"avgSpeed={(speedSum / inBucketCount):0.000} maxSpeed={maxSpeedSeen:0.000} " +
            $"avgOut={(outwardVelocitySum / inBucketCount):0.000} maxOut={maxOutwardVelocity:0.000} " +
            $"bucketVel={bucket.Velocity.magnitude:0.000} motionInherit={bucket.bucketMotionInheritance:0.00} " +
            $"frameDelta={bucketFrameDelta:0.0000} frameAngle={bucketFrameAngle:0.00} velCarry={velocityCarryDistance:0.0000}"
        );
    }

    void CapturePreviousPlaneFrame()
    {
        if (planeCollision == null || planeCollision.plane == null)
            return;

        Transform planeTransform = planeCollision.plane;
        previousPlanePosition = planeTransform.position;
        previousPlaneRight = planeTransform.right;
        previousPlaneUp = planeTransform.up;
        previousPlaneForward = planeTransform.forward;
        hasPreviousPlaneFrame = true;
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

    public void AddParticles(Color color , int count)
    {
        if (count <= 0)
            return;

        if (!IsReady)
            return;

        SyncParticlesFromGpu();
        SyncActiveParticleCountWithList();

        int start = activeParticleCount;
        int addCount = Mathf.Min(count, maxParticles - activeParticleCount);
        if (addCount <= 0)
            return;

        float highestY = float.MinValue;

        foreach (var p in particles)
        {
            if (!p.OnPlane && p.IsinsidetheBucket)
                highestY = Mathf.Max(highestY, p.position.y);
        }

        if (highestY == float.MinValue)
            highestY = bucket.transform.position.y;

        Vector3 spawnCenter = new Vector3(
            bucket.transform.position.x,
            highestY + particleSpacing * 2f,
            bucket.transform.position.z
        );
        for(int i=0 ; i<addCount ; i++)
        {
            Vector3 offset = Random.insideUnitSphere * particleSpacing * 2f;
            SPHParticle p = new SPHParticle();
            p.position = spawnCenter + offset;
            p.predictedPosition = p.position;
            p.velocity = Vector3.zero;
            p.OnPlane = false;
            p.IsinsidetheBucket = true;
            p.color = color;
            particles.Add(p);

            int index = start + i;
            positions[index] = p.position;
            predictedPositions[index] = p.predictedPosition;
            velocities[index] = p.velocity;
            particleState[index] = 0;
            particleColors[index] = ColorToVector(color);
        }

        activeParticleCount += addCount;
        SyncActiveParticleCountWithList();

        positionBuffer.SetData(positions, start, start, addCount);
        predictedBuffer.SetData(predictedPositions, start, start, addCount);
        velocityBuffer.SetData(velocities, start, start, addCount);
        particalStateBuffer.SetData(particleState, start, start, addCount);
        colorBuffer.SetData(particleColors, start, start, addCount);

        SetComputeShaderParameters();
    }

    public void SetAllParticleColors(Color color)
    {
        if (particles == null || activeParticleCount == 0)
            return;

        int count = SyncActiveParticleCountWithList();
        if (count == 0)
            return;

        if (particleColors == null || particleColors.Length != maxParticles)
            particleColors = new Vector4[maxParticles];

        Vector4 newColor = ColorToVector(color);

        for (int i = 0; i < count; i++)
        {
            particles[i].color = color;
            particleColors[i] = newColor;
        }

        if (colorBuffer != null && colorBuffer.IsValid())
            colorBuffer.SetData(particleColors, 0, 0, count);
    }

    void OnDestroy()
    {
        ReleaseComputeBuffers();
    }

    void ReleaseComputeBuffers()
    {
        positionBuffer?.Release();
        predictedBuffer?.Release();
        velocityBuffer?.Release();
        densityBuffer?.Release();
        spatialIndicesBuffer?.Release();
        spatialOffsetsBuffer?.Release();
        particalStateBuffer?.Release();
        nozzleDropCounterBuffer?.Release();
        rimSpillCounterBuffer?.Release();
        colorBuffer?.Release();

        positionBuffer = null;
        predictedBuffer = null;
        velocityBuffer = null;
        densityBuffer = null;
        spatialIndicesBuffer = null;
        spatialOffsetsBuffer = null;
        particalStateBuffer = null;
        nozzleDropCounterBuffer = null;
        rimSpillCounterBuffer = null;
        colorBuffer = null;
    }


    void RebuildParticleArraysFromList()
    {
        int count = SyncActiveParticleCountWithList();
        positions = new Vector3[maxParticles];
        velocities = new Vector3[maxParticles];
        predictedPositions = new Vector3[maxParticles];
        particleState = new int[maxParticles];
        particleColors = new Vector4[maxParticles];

        for (int i = 0; i < count; i++)
        {
            positions[i] = particles[i].position;
            velocities[i] = particles[i].velocity;
            predictedPositions[i] = particles[i].predictedPosition;
            particleState[i] = particles[i].OnPlane
                ? 1
                : (particles[i].IsinsidetheBucket ? 0 : 2);
            particleColors[i] = ColorToVector(particles[i].color);
        }
    }

    static Vector4 ColorToVector(Color color)
    {
        return new Vector4(color.r, color.g, color.b, color.a);
    }

    void SyncParticlesFromGpu()
    {
        if (activeParticleCount == 0)
            return;

        int count = SyncActiveParticleCountWithList();
        if (count == 0)
            return;

        bool hasPositions = positionBuffer != null && positionBuffer.IsValid();
        bool hasVelocities = velocityBuffer != null && velocityBuffer.IsValid();
        bool hasStates = particalStateBuffer != null && particalStateBuffer.IsValid();
        bool hasColors = colorBuffer != null && colorBuffer.IsValid();

        if (!hasPositions && !hasVelocities && !hasStates && !hasColors)
            return;

        if (hasPositions)
        {
            if (positions == null || positions.Length != maxParticles)
                positions = new Vector3[maxParticles];

            positionBuffer.GetData(positions, 0, 0, count);
        }

        if (hasVelocities)
        {
            if (velocities == null || velocities.Length != maxParticles)
                velocities = new Vector3[maxParticles];

            velocityBuffer.GetData(velocities, 0, 0, count);
        }

        if (hasStates)
        {
            if (particleState == null || particleState.Length != maxParticles)
                particleState = new int[maxParticles];

            particalStateBuffer.GetData(particleState, 0, 0, count);
        }

        if (hasColors)
        {
            if (particleColors == null || particleColors.Length != maxParticles)
                particleColors = new Vector4[maxParticles];

            colorBuffer.GetData(particleColors, 0, 0, count);
        }

        for (int i = 0; i < count; i++)
        {
            if (hasPositions)
            {
                particles[i].position = positions[i];
                particles[i].predictedPosition = positions[i];
            }

            if (hasVelocities)
                particles[i].velocity = velocities[i];

            if (hasStates)
            {
                particles[i].OnPlane = particleState[i] == 1;
                particles[i].IsinsidetheBucket = particleState[i] == 0;
            }

            if (hasColors)
            {
                Vector4 particleColor = particleColors[i];
                particles[i].color = new Color(
                    particleColor.x,
                    particleColor.y,
                    particleColor.z,
                    particleColor.w);
            }
        }
    }

}
