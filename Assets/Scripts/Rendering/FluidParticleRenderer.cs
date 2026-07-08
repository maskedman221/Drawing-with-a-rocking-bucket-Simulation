using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class FluidParticleRenderer : MonoBehaviour
{
    [Header("Source")]
    public SPHManager sph;
    public PaintingCollision planeCollision;

    [Header("Airborne Drops")]
    public bool renderAirborneDrops = true;
    [FormerlySerializedAs("particleMesh")]
    public Mesh dropMesh;
    [FormerlySerializedAs("particleMaterial")]
    public Material dropMaterial;
    [FormerlySerializedAs("fluidColor")]
    public Color paintColor = new Color(0f, 1f, 0.04f, 1f);
    public bool applyColorUIChange = false;
    private Color previousColor;
    [Min(0.001f)]
    [FormerlySerializedAs("particleRadius")]
    public float dropRadius = 0.035f;
    [Range(0.1f, 3f)]
    public float velocityStretch = 0.85f;
    public ShadowCastingMode shadowCasting = ShadowCastingMode.On;
    public bool receiveShadows = true;
    [Range(0, 31)]
    public int renderLayer;

    [Header("Paint On Plane")]
    public bool renderPlanePaint = true;
    public Material planePaintMaterial;
    [Min(0.001f)]
    public float paintSpotRadius = 0.045f;
    [Min(0.0001f)]
    public float paintThicknessOffset = 0.002f;
    [Range(0f, 3f)]
    public float strokeStretch = 1.1f;
    [Range(5, 16)]
    public int spotSegments = 9;
    [Range(0.01f, 1f)]
    public float minMovingSpotScale = 0.55f;

    [Header("Performance")]
    [Tooltip("Limits how many plane particles are turned into paint spots. 0 means unlimited.")]
    public int maxPlaneSpots;
    [Tooltip("Rebuild the plane mesh every N rendered frames.")]
    [Min(1)]
    public int planeMeshUpdateInterval = 1;

    const int MaxInstancesPerBatch = 1023;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");
    static readonly int PositionsId = Shader.PropertyToID("Positions");
    static readonly int UnderscorePositionsId = Shader.PropertyToID("_Positions");
    static readonly int ColorsId = Shader.PropertyToID("Colors");
    static readonly int UnderscoreColorsId = Shader.PropertyToID("_Colors");

    readonly List<Matrix4x4> dropMatrices = new List<Matrix4x4>(1024);
    readonly Matrix4x4[] batchMatrices = new Matrix4x4[MaxInstancesPerBatch];
    readonly List<Vector3> vertices = new List<Vector3>(8192);
    readonly List<Vector3> normals = new List<Vector3>(8192);
    readonly List<Color> colors = new List<Color>(8192);
    readonly List<int> triangles = new List<int>(16384);

    Mesh paintMesh;
    Mesh generatedDropMesh;
    GameObject paintMeshObject;
    MeshFilter paintMeshFilter;
    MeshRenderer paintMeshRenderer;
    Material runtimeDropMaterial;
    Material runtimeGpuDropMaterial;
    Material runtimePlaneMaterial;
    MaterialPropertyBlock dropProperties;
    ComputeBuffer indirectArgsBuffer;
    ComputeBuffer boundPositionBuffer;
    ComputeBuffer boundColorBuffer;
    int lastIndirectCount = -1;
    int frameCounter;

    void Start()
    {
        previousColor = paintColor;
    }
    void Reset()
    {
        sph = FindFirstObjectByType<SPHManager>();
        planeCollision = FindFirstObjectByType<PaintingCollision>();
    }

    void OnEnable()
    {
        EnsureReferences();
    }

    void LateUpdate()
    {
        EnsureReferences();

        if (sph == null || sph.Particles == null || sph.Particles.Count == 0)
        {
            if (paintMesh != null)
            {
                paintMesh.Clear();
            }

            return;
        }

        EnsureResources();

        if (sph.GPUSimulation)
        {
            if (paintMesh != null)
            {
                paintMesh.Clear();
            }
            
            frameCounter++;

            if (renderAirborneDrops)
            {
                DrawGpuDropsIndirect();
            }
            if (paintColor != ParticleColorPicker.Instance.GetCurrentColor() && applyColorUIChange)
            {
                paintColor = ParticleColorPicker.Instance.GetCurrentColor();
                sph.SetAllParticleColors(paintColor);
            }
            return;
        }

        if (renderPlanePaint && frameCounter % planeMeshUpdateInterval == 0)
        {
            RebuildPaintMesh();
        }

        frameCounter++;

        if (renderAirborneDrops)
        {
            DrawAirborneDrops();
        }
    }

    void EnsureReferences()
    {
        if (sph == null)
        {
            sph = FindFirstObjectByType<SPHManager>();
        }

        if (planeCollision == null)
        {
            planeCollision = sph != null ? sph.planeCollision : FindFirstObjectByType<PaintingCollision>();
        }
    }

    void EnsureResources()
    {
        if (dropMesh == null)
        {
            if (generatedDropMesh == null)
            {
                generatedDropMesh = CreateUvSphereMesh(10, 8);
                generatedDropMesh.name = "Generated Paint Drop Mesh";
                generatedDropMesh.hideFlags = HideFlags.HideAndDontSave;
            }

            dropMesh = generatedDropMesh;
        }

        if (!sph.GPUSimulation && runtimeDropMaterial == null)
        {
            runtimeDropMaterial = CreateDropMaterial();
        }

        if (dropProperties == null)
        {
            dropProperties = new MaterialPropertyBlock();
        }

        if (!sph.GPUSimulation && renderPlanePaint)
        {
            EnsurePaintMeshObject();
        }
    }

    Material CreateDropMaterial()
    {
        Material source = dropMaterial;
        if (source == null || UsesSimulationBufferShader(source.shader))
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            source = new Material(shader);
        }

        Material material = new Material(source)
        {
            name = "Runtime Paint Drop Material",
            hideFlags = HideFlags.HideAndDontSave,
            enableInstancing = true
        };

        SetMaterialColor(material, paintColor);
        if (material.HasProperty(SmoothnessId))
        {
            material.SetFloat(SmoothnessId, 0.82f);
        }

        if (material.HasProperty(MetallicId))
        {
            material.SetFloat(MetallicId, 0f);
        }

        return material;
    }

    Material CreateGpuDropMaterial()
    {
        Material source = null;
        if (dropMaterial != null && dropMaterial.shader != null && dropMaterial.shader.name == "Fluid/ParticlesURP")
        {
            source = dropMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Fluid/ParticlesURP");
            if (shader != null)
            {
                source = new Material(shader);
            }
        }

        if (source == null)
        {
            return null;
        }

        Material material = new Material(source)
        {
            name = "Runtime GPU Paint Drop Material",
            hideFlags = HideFlags.HideAndDontSave,
            enableInstancing = true
        };

        SetMaterialColor(material, Color.white);
        if (material.HasProperty(RadiusId))
        {
            material.SetFloat(RadiusId, dropRadius);
        }

        return material;
    }

    void EnsurePaintMeshObject()
    {
        if (paintMeshObject == null)
        {
            paintMeshObject = new GameObject("SPH Paint On Plane");
            paintMeshObject.hideFlags = HideFlags.DontSave;
            paintMeshObject.layer = renderLayer;
            paintMeshObject.transform.SetParent(transform, false);
            paintMeshObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            paintMeshObject.transform.localScale = Vector3.one;

            paintMeshFilter = paintMeshObject.AddComponent<MeshFilter>();
            paintMeshRenderer = paintMeshObject.AddComponent<MeshRenderer>();
        }

        if (paintMesh == null)
        {
            paintMesh = new Mesh
            {
                name = "SPH Paint Mesh",
                indexFormat = IndexFormat.UInt32
            };
            paintMesh.MarkDynamic();
            paintMeshFilter.sharedMesh = paintMesh;
        }

        if (runtimePlaneMaterial == null)
        {
            runtimePlaneMaterial = CreatePlaneMaterial();
        }

        paintMeshRenderer.sharedMaterial = runtimePlaneMaterial;
        paintMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        paintMeshRenderer.receiveShadows = false;
    }

    Material CreatePlaneMaterial()
    {
        Material source = planePaintMaterial;
        if (source == null || UsesSimulationBufferShader(source.shader))
        {
            Shader shader = Shader.Find("Fluid/WetPlanePaint");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            source = new Material(shader);
        }

        Material material = new Material(source)
        {
            name = "Runtime Wet Plane Paint Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        SetMaterialColor(material, Color.white);
        if (material.HasProperty(SmoothnessId))
        {
            material.SetFloat(SmoothnessId, 0.9f);
        }

        return material;
    }

    static bool UsesSimulationBufferShader(Shader shader)
    {
        if (shader == null)
        {
            return false;
        }

        string shaderName = shader.name;
        return shaderName == "Fluid/ParticlesURP" ||
            shaderName == "Fluid/WorldBlob" ||
            shaderName == "Fluid/Depth" ||
            shaderName == "Fluid/Composite";
    }

    void DrawAirborneDrops()
    {
        if (runtimeDropMaterial == null)
        {
            runtimeDropMaterial = CreateDropMaterial();
        }

        dropMatrices.Clear();

        IReadOnlyList<SPHParticle> particles = sph.Particles;
        for (int i = 0; i < particles.Count; i++)
        {
            SPHParticle particle = particles[i];
            if (particle == null || particle.OnPlane)
            {
                continue;
            }

            Vector3 velocity = particle.velocity;
            float speed = velocity.magnitude;
            Quaternion rotation = speed > 0.02f
                ? Quaternion.FromToRotation(Vector3.up, velocity.normalized)
                : Quaternion.identity;

            float stretch = 1f + Mathf.Clamp(speed * velocityStretch, 0f, 2.5f);
            Vector3 scale = new Vector3(dropRadius, dropRadius * stretch, dropRadius);
            dropMatrices.Add(Matrix4x4.TRS(particle.position, rotation, scale));
        }

        if (dropMatrices.Count == 0)
        {
            return;
        }

        dropProperties.SetColor(BaseColorId, paintColor);
        dropProperties.SetColor(ColorId, paintColor);

        for (int start = 0; start < dropMatrices.Count; start += MaxInstancesPerBatch)
        {
            int count = Mathf.Min(MaxInstancesPerBatch, dropMatrices.Count - start);
            dropMatrices.CopyTo(start, batchMatrices, 0, count);
            Graphics.DrawMeshInstanced(
                dropMesh,
                0,
                runtimeDropMaterial,
                batchMatrices,
                count,
                dropProperties,
                shadowCasting,
                receiveShadows,
                renderLayer);
        }
    }

    void DrawGpuDropsIndirect()
    {
        if (dropMesh == null || !sph.IsReady || sph.ParticleCount == 0)
        {
            return;
        }

        ComputeBuffer positionBuffer = sph.GetPositionBuffer();
        if (positionBuffer == null || !positionBuffer.IsValid())
        {
            return;
        }

        ComputeBuffer colorBuffer = sph.GetColorBuffer();
        if (colorBuffer == null || !colorBuffer.IsValid())
        {
            return;
        }

        if (runtimeGpuDropMaterial == null)
        {
            runtimeGpuDropMaterial = CreateGpuDropMaterial();
        }

        if (runtimeGpuDropMaterial == null)
        {
            return;
        }

        if (sph.ParticleCount != lastIndirectCount)
        {
            RebuildIndirectArgs(sph.ParticleCount);
        }

        if (boundPositionBuffer != positionBuffer)
        {
            boundPositionBuffer = positionBuffer;
            runtimeGpuDropMaterial.SetBuffer(PositionsId, positionBuffer);
            runtimeGpuDropMaterial.SetBuffer(UnderscorePositionsId, positionBuffer);
        }

        if (boundColorBuffer != colorBuffer)
        {
            boundColorBuffer = colorBuffer;
            runtimeGpuDropMaterial.SetBuffer(ColorsId, colorBuffer);
            runtimeGpuDropMaterial.SetBuffer(UnderscoreColorsId, colorBuffer);
        }

        SetMaterialColor(runtimeGpuDropMaterial, Color.white);
        if (runtimeGpuDropMaterial.HasProperty(RadiusId))
        {
            runtimeGpuDropMaterial.SetFloat(RadiusId, dropRadius);
        }

        Graphics.DrawMeshInstancedIndirect(
            dropMesh,
            0,
            runtimeGpuDropMaterial,
            new Bounds(sph.boxCenter, sph.boxSize + Vector3.one * 10f),
            indirectArgsBuffer,
            0,
            null,
            shadowCasting,
            receiveShadows,
            renderLayer);
    }

    void RebuildIndirectArgs(int count)
    {
        indirectArgsBuffer?.Release();
        lastIndirectCount = count;

        uint[] args =
        {
            dropMesh.GetIndexCount(0),
            (uint)count,
            dropMesh.GetIndexStart(0),
            dropMesh.GetBaseVertex(0),
            0
        };

        indirectArgsBuffer = new ComputeBuffer(
            1,
            args.Length * sizeof(uint),
            ComputeBufferType.IndirectArguments);
        indirectArgsBuffer.SetData(args);
    }

    void RebuildPaintMesh()
    {
        EnsurePaintMeshObject();

        vertices.Clear();
        normals.Clear();
        colors.Clear();
        triangles.Clear();

        Transform plane = planeCollision != null ? planeCollision.plane : null;
        Vector3 normal = plane != null ? plane.up.normalized : Vector3.up;
        Vector3 right = plane != null ? plane.right.normalized : Vector3.right;
        Vector3 forward = Vector3.Cross(right, normal).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        IReadOnlyList<SPHParticle> particles = sph.Particles;
        int spots = 0;
        for (int i = 0; i < particles.Count; i++)
        {
            if (maxPlaneSpots > 0 && spots >= maxPlaneSpots)
            {
                break;
            }

            SPHParticle particle = particles[i];
            if (particle == null || !particle.OnPlane)
            {
                continue;
            }

            float jitter = Mathf.Lerp(0.82f, 1.18f, Hash01(i));
            AddPaintSpot(
                particle.position + normal * paintThicknessOffset,
                particle.velocity,
                particle.color,
                normal,
                right,
                forward,
                paintSpotRadius * jitter,
                i);
            spots++;
        }

        paintMesh.Clear();
        if (vertices.Count == 0)
        {
            return;
        }

        paintMesh.SetVertices(vertices);
        paintMesh.SetNormals(normals);
        paintMesh.SetColors(colors);
        paintMesh.SetTriangles(triangles, 0, true);
        paintMesh.RecalculateBounds();
    }

    void AddPaintSpot(
        Vector3 center,
        Vector3 velocity,
        Color color,
        Vector3 normal,
        Vector3 right,
        Vector3 forward,
        float radius,
        int particleIndex)
    {
        int centerIndex = vertices.Count;
        vertices.Add(center);
        normals.Add(normal);
        colors.Add(new Color(color.r, color.g, color.b, 0.94f));

        Vector3 tangentVelocity = Vector3.ProjectOnPlane(velocity, normal);
        float speed = tangentVelocity.magnitude;
        Vector3 majorAxis = speed > 0.001f ? tangentVelocity.normalized : right;
        Vector3 minorAxis = Vector3.Cross(normal, majorAxis).normalized;

        float major = radius * (1f + Mathf.Clamp(speed * strokeStretch, 0f, 2.2f));
        float minor = Mathf.Max(radius * minMovingSpotScale, radius / Mathf.Max(major / radius, 1f));
        float angleOffset = Hash01(particleIndex * 17 + 3) * Mathf.PI * 2f;

        for (int s = 0; s < spotSegments; s++)
        {
            float t = angleOffset + (s / (float)spotSegments) * Mathf.PI * 2f;
            Vector3 rim = center + majorAxis * (Mathf.Cos(t) * major) + minorAxis * (Mathf.Sin(t) * minor);
            vertices.Add(rim);
            normals.Add(normal);
            colors.Add(new Color(color.r, color.g, color.b, 0.78f));
        }

        for (int s = 0; s < spotSegments; s++)
        {
            int a = centerIndex;
            int b = centerIndex + 1 + s;
            int c = centerIndex + 1 + ((s + 1) % spotSegments);
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }

    static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }
    }

    static float Hash01(int value)
    {
        unchecked
        {
            uint x = (uint)value;
            x ^= x >> 16;
            x *= 0x7feb352dU;
            x ^= x >> 15;
            x *= 0x846ca68bU;
            x ^= x >> 16;
            return (x & 0x00ffffff) / 16777215f;
        }
    }

    static Mesh CreateUvSphereMesh(int longitudeSegments, int latitudeSegments)
    {
        List<Vector3> meshVertices = new List<Vector3>();
        List<Vector3> meshNormals = new List<Vector3>();
        List<int> meshTriangles = new List<int>();

        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float v = lat / (float)latitudeSegments;
            float theta = v * Mathf.PI;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float u = lon / (float)longitudeSegments;
                float phi = u * Mathf.PI * 2f;
                Vector3 normal = new Vector3(
                    Mathf.Cos(phi) * sinTheta,
                    cosTheta,
                    Mathf.Sin(phi) * sinTheta);
                meshVertices.Add(normal);
                meshNormals.Add(normal);
            }
        }

        int row = longitudeSegments + 1;
        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int a = lat * row + lon;
                int b = a + row;
                int c = b + 1;
                int d = a + 1;

                meshTriangles.Add(a);
                meshTriangles.Add(b);
                meshTriangles.Add(c);
                meshTriangles.Add(a);
                meshTriangles.Add(c);
                meshTriangles.Add(d);
            }
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(meshVertices);
        mesh.SetNormals(meshNormals);
        mesh.SetTriangles(meshTriangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDisable()
    {
        if (paintMesh != null)
        {
            paintMesh.Clear();
        }
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            Destroy(runtimeDropMaterial);
            Destroy(runtimeGpuDropMaterial);
            Destroy(runtimePlaneMaterial);
            Destroy(generatedDropMesh);
            Destroy(paintMesh);
            Destroy(paintMeshObject);
        }
        else
        {
            DestroyImmediate(runtimeDropMaterial);
            DestroyImmediate(runtimeGpuDropMaterial);
            DestroyImmediate(runtimePlaneMaterial);
            DestroyImmediate(generatedDropMesh);
            DestroyImmediate(paintMesh);
            DestroyImmediate(paintMeshObject);
        }

        indirectArgsBuffer?.Release();
        indirectArgsBuffer = null;
        boundPositionBuffer = null;
        boundColorBuffer = null;
    }
}
