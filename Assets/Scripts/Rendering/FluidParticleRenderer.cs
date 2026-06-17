using UnityEngine;

public class FluidParticleRenderer : MonoBehaviour
{
    public SPHManager sph;

    public Mesh particleMesh;
    public Material particleMaterial;

    ComputeBuffer positionBuffer;
    ComputeBuffer argsBuffer;

    Vector4[] positions;

    int lastCount = -1;

    void LateUpdate()
    {
        var particles = sph.GetParticles();

        if (particles == null || particles.Count == 0)
            return;

        // 🔥 rebuild GPU buffers if particle count changes
        if (particles.Count != lastCount)
        {
            lastCount = particles.Count;
            RebuildBuffers(lastCount);
        }

        // 🔥 safety check
        if (positions == null || positions.Length != particles.Count)
            return;

        for (int i = 0; i < particles.Count; i++)
        {
            positions[i] = new Vector4(
                particles[i].position.x,
                particles[i].position.y,
                particles[i].position.z,
                1f
            );
        }

        positionBuffer.SetData(positions);

        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            particleMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f),
            argsBuffer
        );
    }

    void RebuildBuffers(int count)
    {
        // release old buffers
        if (positionBuffer != null)
            positionBuffer.Release();

        if (argsBuffer != null)
            argsBuffer.Release();

        // allocate CPU cache
        positions = new Vector4[count];

        // GPU buffer for positions
        positionBuffer =
            new ComputeBuffer(count, sizeof(float) * 4);

        particleMaterial.SetBuffer("_Positions", positionBuffer);

        // indirect draw args
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
        if (positionBuffer != null)
            positionBuffer.Release();

        if (argsBuffer != null)
            argsBuffer.Release();
    }
}