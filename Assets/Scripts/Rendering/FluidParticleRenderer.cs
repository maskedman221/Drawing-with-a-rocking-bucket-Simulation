using Unity.Mathematics;
using UnityEngine;

public class FluidParticleRenderer : MonoBehaviour
{
    public SPHManager sph;

    [Header("Rendering")]
    public Mesh particleMesh;
    public Material depthMaterial;
    public Material compositeMaterial;
    public Material particleMaterial;

    [Header("Compute Shaders")]
    public ComputeShader blurShader;
    public ComputeShader normalShader;

    [Header("Fluid Settings")]
    public float particleRadius = 0.16f;
    public Color fluidColor = new Color(0.18f, 0.50f, 1f, 1f);

    [Header("Bilateral Blur Settings")]
    public float depthSigma = 0.05f;
    public int blurRadius = 4;

    [Header("Debug")]
    public bool showIndividualParticles;

    private ComputeBuffer argsBuffer;

    private RenderTexture depthTexture;
    private RenderTexture tempTexture;
    private RenderTexture blurredDepthTexture;
    private RenderTexture normalTexture;

    private int kernelH;
    private int kernelV;
    private int normalKernel;

    private int2 screenSize;

    void Start()
    {
        screenSize = new int2(Screen.width, Screen.height);

        InitializeRenderTextures();
        BuildArgsBuffer(sph.ParticleCount);
        SetupShaders();

        Camera.main.depthTextureMode = DepthTextureMode.Depth;

        if (depthMaterial != null)
        {
            depthMaterial.SetBuffer("Positions", sph.GetPositionBuffer());
            depthMaterial.SetFloat("_ParticleRadius", particleRadius);
        }
    }

    void InitializeRenderTextures()
    {
        depthTexture = CreateRT(RenderTextureFormat.RFloat);
        tempTexture = CreateRT(RenderTextureFormat.RFloat);
        blurredDepthTexture = CreateRT(RenderTextureFormat.RFloat);

        normalTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
        normalTexture.enableRandomWrite = true;
        normalTexture.Create();
    }

    RenderTexture CreateRT(RenderTextureFormat format)
    {
        var rt = new RenderTexture(Screen.width, Screen.height, 0, format);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    void SetupShaders()
    {
        if (blurShader != null)
        {
            kernelH = blurShader.FindKernel("BlurHorizontal");
            kernelV = blurShader.FindKernel("BlurVertical");

            blurShader.SetInts("resolution", Screen.width, Screen.height);
        }

        if (normalShader != null)
        {
            normalKernel = normalShader.FindKernel("CSMain");
            normalShader.SetInts("resolution", Screen.width, Screen.height);
        }
    }

    void LateUpdate()
    {
        if (sph == null) return;

        if (Screen.width != screenSize.x || Screen.height != screenSize.y)
        {
            RecreateRenderTextures();
        }

        if (showIndividualParticles)
        {
            RenderDiscreteParticles();
            return;
        }

        RenderDepth();
        BlurDepthBilateral();
        GenerateNormals();
        Composite();
    }

    void RenderDepth()
    {
        Graphics.SetRenderTarget(depthTexture);
        GL.Clear(true, true, Color.black);

        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            depthMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f),
            argsBuffer
        );
    }

    void BlurDepthBilateral()
    {
        if (blurShader == null) return;

        blurShader.SetTexture(kernelH, "Input", depthTexture);
        blurShader.SetTexture(kernelH, "Output", tempTexture);

        blurShader.SetFloat("depthSigma", depthSigma);
        blurShader.SetInt("radius", blurRadius);

        blurShader.Dispatch(
            kernelH,
            Mathf.CeilToInt(Screen.width / 8f),
            Mathf.CeilToInt(Screen.height / 8f),
            1
        );

        blurShader.SetTexture(kernelV, "Input", tempTexture);
        blurShader.SetTexture(kernelV, "Output", blurredDepthTexture);

        blurShader.SetFloat("depthSigma", depthSigma);
        blurShader.SetInt("radius", blurRadius);

        blurShader.Dispatch(
            kernelV,
            Mathf.CeilToInt(Screen.width / 8f),
            Mathf.CeilToInt(Screen.height / 8f),
            1
        );
    }

    void GenerateNormals()
    {
        if (normalShader == null) return;

        normalShader.SetTexture(normalKernel, "Depth", blurredDepthTexture);
        normalShader.SetTexture(normalKernel, "Normals", normalTexture);

        normalShader.Dispatch(
            normalKernel,
            Mathf.CeilToInt(Screen.width / 8f),
            Mathf.CeilToInt(Screen.height / 8f),
            1
        );
    }

    void Composite()
    {
        if (compositeMaterial == null) return;

        compositeMaterial.SetTexture("_FluidTex", blurredDepthTexture);
        compositeMaterial.SetTexture("_Normals", normalTexture);
        compositeMaterial.SetColor("_Color", fluidColor);

        // IMPORTANT: Unity built-in camera depth
        compositeMaterial.SetTexture("_CameraDepthTexture", Shader.GetGlobalTexture("_CameraDepthTexture"));

        Graphics.Blit(null, (RenderTexture)null, compositeMaterial);
    }

    void RenderDiscreteParticles()
    {
        if (particleMaterial == null) return;

        particleMaterial.SetBuffer("Positions", sph.GetPositionBuffer());
        particleMaterial.SetFloat("_Radius", particleRadius);
        particleMaterial.SetColor("_Color", fluidColor);

        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            particleMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000f),
            argsBuffer
        );
    }

    void RecreateRenderTextures()
    {
        screenSize = new int2(Screen.width, Screen.height);

        depthTexture?.Release();
        tempTexture?.Release();
        blurredDepthTexture?.Release();
        normalTexture?.Release();

        InitializeRenderTextures();
        SetupShaders();
    }

    void BuildArgsBuffer(int count)
    {
        argsBuffer?.Release();

        uint[] args = new uint[5];

        args[0] = particleMesh.GetIndexCount(0);
        args[1] = (uint)count;
        args[2] = particleMesh.GetIndexStart(0);
        args[3] = particleMesh.GetBaseVertex(0);
        args[4] = 0;

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    void OnDestroy()
    {
        argsBuffer?.Release();

        depthTexture?.Release();
        tempTexture?.Release();
        blurredDepthTexture?.Release();
        normalTexture?.Release();
    }
}