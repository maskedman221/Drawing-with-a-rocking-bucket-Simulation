using UnityEngine;

public class SPHCubeRenderer : MonoBehaviour
{
    public SPHManager sph;

    public Mesh particleMesh;
    public Material particleMaterial;

    ComputeBuffer argsBuffer;

    Material runtimeMaterial;
    ComputeBuffer boundPositionBuffer;
    ComputeBuffer boundColorBuffer;

    int lastCount = -1;
    static readonly int PositionsId = Shader.PropertyToID("Positions");
    static readonly int UnderscorePositionsId = Shader.PropertyToID("_Positions");
    static readonly int ColorsId = Shader.PropertyToID("Colors");
    static readonly int UnderscoreColorsId = Shader.PropertyToID("_Colors");

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

        ComputeBuffer colorBuffer = sph.GetColorBuffer();
        if (colorBuffer != null && colorBuffer.IsValid() && boundColorBuffer != colorBuffer)
            BindColorBuffer(colorBuffer);

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
        boundPositionBuffer = null;
        boundColorBuffer = null;

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);

        runtimeMaterial = CreateRuntimeMaterial();
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
        runtimeMaterial.SetBuffer(PositionsId, positionBuffer);
        runtimeMaterial.SetBuffer(UnderscorePositionsId, positionBuffer);
    }

    void BindColorBuffer(ComputeBuffer colorBuffer)
    {
        if (runtimeMaterial == null || colorBuffer == null)
            return;

        boundColorBuffer = colorBuffer;
        runtimeMaterial.SetBuffer(ColorsId, colorBuffer);
        runtimeMaterial.SetBuffer(UnderscoreColorsId, colorBuffer);
    }

    Material CreateRuntimeMaterial()
    {
        Material source = particleMaterial;
        if (source == null || source.shader == null || !SupportsPositionBuffer(source.shader))
        {
            Shader shader = Shader.Find("Fluid/ParticlesURP");
            if (shader != null)
                source = new Material(shader);
        }

        if (source == null)
            return null;

        Material material = new Material(source)
        {
            enableInstancing = true
        };

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        return material;
    }

    static bool SupportsPositionBuffer(Shader shader)
    {
        if (shader == null)
            return false;

        string shaderName = shader.name;
        return shaderName == "Fluid/ParticlesURP" ||
               shaderName == "Fluid/WorldBlob" ||
               shaderName == "Fluid/Depth";
    }
}
