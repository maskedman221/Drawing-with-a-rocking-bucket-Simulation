using UnityEngine;

public class SPHCubeRenderer : MonoBehaviour
{
    public SPHManager sph;

    public Mesh particleMesh;
    public Material particleMaterial;

    ComputeBuffer argsBuffer;

    Material runtimeMaterial;
    ComputeBuffer boundPositionBuffer;

    int lastCount = -1;

    void Update()
    {
        if (sph == null || particleMesh == null || particleMaterial == null)
            return;

        if (!sph.IsReady || sph.ParticleCount == 0)
            return;

        ComputeBuffer positionBuffer = sph.GetPositionBuffer();
        if (positionBuffer == null || !positionBuffer.IsValid())
            return;

        if (sph.ParticleCount != lastCount)
        {
            lastCount = sph.ParticleCount;
            RebuildBuffers(lastCount);
        }

        if (boundPositionBuffer != positionBuffer)
            BindPositionBuffer(positionBuffer);

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
        argsBuffer?.Release();

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);

        runtimeMaterial = new Material(particleMaterial);
        BindPositionBuffer(sph.GetPositionBuffer());

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
        argsBuffer?.Release();

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    void BindPositionBuffer(ComputeBuffer positionBuffer)
    {
        if (runtimeMaterial == null || positionBuffer == null)
            return;

        boundPositionBuffer = positionBuffer;
        runtimeMaterial.SetBuffer("Positions", positionBuffer);
        runtimeMaterial.SetBuffer("_Positions", positionBuffer);
    }
}
