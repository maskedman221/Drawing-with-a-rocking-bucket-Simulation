using UnityEngine;

public class SPHCubeRenderer : MonoBehaviour
{
    public SPHManager sph;

    public Mesh particleMesh;
    public Material particleMaterial;

    ComputeBuffer positionBuffer;
    ComputeBuffer argsBuffer;

    Vector3[] positions;
    Material runtimeMaterial;

    int lastCount = -1;

    void Update()
    {
        if (sph == null || particleMesh == null || particleMaterial == null)
            return;

        var particles = sph.GetParticles();

        if (particles == null || particles.Count == 0)
            return;

        if (particles.Count != lastCount)
        {
            lastCount = particles.Count;
            RebuildBuffers(lastCount);
        }

        if (positions == null || positions.Length != particles.Count)
            return;

        for (int i = 0; i < particles.Count; i++)
            positions[i] = particles[i].position;

        positionBuffer.SetData(positions);

        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            runtimeMaterial,
            new Bounds(sph.boxCenter, sph.boxSize + Vector3.one * 10f),
            argsBuffer
        );
    }

    void RebuildBuffers(int count)
    {
        positionBuffer?.Release();
        argsBuffer?.Release();

        positions = new Vector3[count];
        positionBuffer = new ComputeBuffer(count, sizeof(float) * 3);

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);

        runtimeMaterial = new Material(particleMaterial);
        runtimeMaterial.SetBuffer("Positions", positionBuffer);
        runtimeMaterial.SetBuffer("_Positions", positionBuffer);

        uint[] args = new uint[5];
        args[0] = particleMesh.GetIndexCount(0);
        args[1] = (uint)count;
        args[2] = particleMesh.GetIndexStart(0);
        args[3] = particleMesh.GetBaseVertex(0);
        args[4] = 0;

        argsBuffer =
            new ComputeBuffer(
                1,
                args.Length * sizeof(uint),
                ComputeBufferType.IndirectArguments);

        argsBuffer.SetData(args);
    }

    void OnDestroy()
    {
        positionBuffer?.Release();
        argsBuffer?.Release();

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}
