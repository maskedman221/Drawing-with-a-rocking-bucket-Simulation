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
    [Header("Container")]
    public Vector3 boxCenter = new Vector3(0, 2, 0);
    public Vector3 boxSize = new Vector3(5, 5, 5);

    [Header("Simulation")]
    public float gravity = -9.81f;
    public float dt = 0.005f;
    public int solverIterations = 4;

    [Header("SPH")]
    public float smoothingRadius = 1f;
    public float targetDensity = 20f;
    public float pressureMultiplier = 30f;
    public float nearPressureMultiplier = 60f;
    public float viscosityStrength = 0.15f;

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

    public int ParticleCount => particles.Count;
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
    int externalKernel;
    int updateSpatialHashKernel;
    int calculateDensitiesKernel;
    int calculatePressureForceKernel;
    int calculateViscosityKernel;
    int updatePositionsKernel;
    int[] particleState;
    GPUSort gpuSort;
    #region Kernels

    #endregion

    void Start()
    {
        float spacing = 0.008f;

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

                    Vector3 worldPos = bucket.transform.TransformPoint(localPos);
                    SPHParticle p = new SPHParticle();
                    p.position = worldPos;
                    p.velocity = Vector3.zero;

                    particles.Add(p);
                }
            }
        }
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
    void InitializeComputeShader()
    {
        externalKernel =simulationShader.FindKernel("ExternalForces");
        updateSpatialHashKernel =simulationShader.FindKernel("UpdateSpatialHash");
        calculateDensitiesKernel =simulationShader.FindKernel("CalculateDensities");
        calculatePressureForceKernel =simulationShader.FindKernel("CalculatePressureForce");
        calculateViscosityKernel = simulationShader.FindKernel("CalculateViscosity");
        updatePositionsKernel = simulationShader.FindKernel("UpdatePositions");
        positionBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(particles.Count);
        velocityBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(particles.Count);
        predictedBuffer =ComputeHelper.CreateStructuredBuffer<Vector3>(particles.Count);
        particalStateBuffer =ComputeHelper.CreateStructuredBuffer<int>(particles.Count);
        densityBuffer =ComputeHelper.CreateStructuredBuffer<Vector2>(particles.Count);
        spatialIndicesBuffer =ComputeHelper.CreateStructuredBuffer<SpatialIndex>(particles.Count);
        spatialOffsetsBuffer =ComputeHelper.CreateStructuredBuffer<uint>(particles.Count);
        positionBuffer.SetData(positions);
        velocityBuffer.SetData(velocities);
        predictedBuffer.SetData(predictedPositions);
        particalStateBuffer.SetData(particleState);

        ComputeHelper.SetBuffer(simulationShader,positionBuffer,"Positions",externalKernel);
        ComputeHelper.SetBuffer(simulationShader,velocityBuffer,"Velocities",externalKernel);
        ComputeHelper.SetBuffer(simulationShader,predictedBuffer,"PredictedPositions",externalKernel);
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
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",updateSpatialHashKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculateDensitiesKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculatePressureForceKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",calculateViscosityKernel);
        ComputeHelper.SetBuffer(simulationShader,particalStateBuffer,"ParticleState",updatePositionsKernel);
        SetComputeShaderParameters();
        gpuSort = new GPUSort();
        gpuSort.SetBuffers(spatialIndicesBuffer,spatialOffsetsBuffer);
    }
    void SetComputeShaderParameters()
    {
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
        simulationShader.SetFloat("wallBounce",wallBounce);
    }
    void SimulateGPU(float dt)
    {
        SetComputeShaderParameters();
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: externalKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: updateSpatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: calculateDensitiesKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: calculatePressureForceKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: calculateViscosityKernel);
        ComputeHelper.Dispatch(simulationShader, particles.Count, kernelIndex: updatePositionsKernel);
        Vector2[] densityData =new Vector2[particles.Count];
        densityBuffer.GetData(densityData);
        // Debug.Log(densityData[0]);
        velocityBuffer.GetData(velocities);
        // Debug.Log(velocities[0]);
        positionBuffer.GetData(positions);
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
                particleState[i] =particles[i].OnPlane ? 1 : 0;
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
                    velocities[i].y += gravity ;
                    planeCollision.Constrain(ref positions[i] , ref velocities[i], viscosityStrength , particles[i].OnPlane);
                }
                                
                
            }
        }

        particalStateBuffer.SetData(particleState);
        positionBuffer.SetData(positions);
        velocityBuffer.SetData(velocities);
    }
    void Update()
    {
        SimulateGPU(dt);
    }

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

    void ResolveBoxCollision(SPHParticle p)
    {
        Vector3 half = boxSize * 0.5f;

        Vector3 min = boxCenter - half;
        Vector3 max = boxCenter + half;

        Vector3 pos = p.position;
        Vector3 vel = p.velocity;

        if (pos.x < min.x)
        {
            pos.x = min.x;
            vel.x *= -wallBounce;
        }
        else if (pos.x > max.x)
        {
            pos.x = max.x;
            vel.x *= -wallBounce;
        }

        if (pos.y < min.y)
        {
            pos.y = min.y;
            vel.y *= -wallBounce;
        }
        else if (pos.y > max.y)
        {
            pos.y = max.y;
            vel.y *= -wallBounce;
        }

        if (pos.z < min.z)
        {
            pos.z = min.z;
            vel.z *= -wallBounce;
        }
        else if (pos.z > max.z)
        {
            pos.z = max.z;
            vel.z *= -wallBounce;
        }

        p.position = pos;
        p.velocity = vel;
    }
    public List<SPHParticle> GetParticles()
    {
        return particles;
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(boxCenter, boxSize);

        // if (particles == null)
        //     return;

        // Gizmos.color = Color.blue;

        // foreach (var p in particles)
        // {
        //     Gizmos.DrawSphere(
        //         p.position,
        //         0.08f);
        // }
    }

    public ComputeBuffer GetPositionBuffer()
    {
        return positionBuffer;
    }
}
