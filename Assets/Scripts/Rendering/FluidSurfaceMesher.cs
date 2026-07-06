using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class FluidSurfaceMesher
{
    public struct Settings
    {
        public float cellSize;
        public float influenceRadius;
        public float isoLevel;
        public int maxCellsPerAxis;
        public bool includePlaneParticles;
        public int smoothingIterations;
        public float smoothingStrength;
    }

    static readonly Vector3Int[] CubeCornerOffsets =
    {
        new Vector3Int(0, 0, 0),
        new Vector3Int(1, 0, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, 0, 1),
        new Vector3Int(1, 0, 1),
        new Vector3Int(1, 1, 1),
        new Vector3Int(0, 1, 1)
    };

    static readonly int[,] Tetrahedra =
    {
        { 0, 5, 1, 6 },
        { 0, 1, 2, 6 },
        { 0, 2, 3, 6 },
        { 0, 3, 7, 6 },
        { 0, 7, 4, 6 },
        { 0, 4, 5, 6 }
    };

    public static bool BuildMesh(
        IReadOnlyList<SPHParticle> sourceParticles,
        Transform meshTransform,
        Mesh mesh,
        Settings settings)
    {
        if (sourceParticles == null || mesh == null)
        {
            return false;
        }

        float influenceRadius = Mathf.Max(0.001f, settings.influenceRadius);
        float isoLevel = Mathf.Max(0.001f, settings.isoLevel);
        int maxCellsPerAxis = Mathf.Max(4, settings.maxCellsPerAxis);

        List<Vector3> particles = new List<Vector3>(sourceParticles.Count);
        Bounds bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < sourceParticles.Count; i++)
        {
            SPHParticle particle = sourceParticles[i];
            if (particle == null || (!settings.includePlaneParticles && particle.OnPlane))
            {
                continue;
            }

            Vector3 position = particle.position;
            particles.Add(position);

            if (!hasBounds)
            {
                bounds = new Bounds(position, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(position);
            }
        }

        if (particles.Count < 4 || !hasBounds)
        {
            mesh.Clear();
            return false;
        }

        bounds.Expand(influenceRadius * 2f);

        float cellSize = Mathf.Max(0.002f, settings.cellSize);
        Vector3 size = bounds.size;
        cellSize = Mathf.Max(
            cellSize,
            Mathf.Max(size.x, Mathf.Max(size.y, size.z)) / maxCellsPerAxis
        );

        int cellsX = Mathf.Clamp(Mathf.CeilToInt(size.x / cellSize), 1, maxCellsPerAxis);
        int cellsY = Mathf.Clamp(Mathf.CeilToInt(size.y / cellSize), 1, maxCellsPerAxis);
        int cellsZ = Mathf.Clamp(Mathf.CeilToInt(size.z / cellSize), 1, maxCellsPerAxis);

        int sampleX = cellsX + 1;
        int sampleY = cellsY + 1;
        int sampleZ = cellsZ + 1;
        float[] field = new float[sampleX * sampleY * sampleZ];

        Dictionary<Vector3Int, List<int>> particleHash = BuildParticleHash(particles, influenceRadius);

        Vector3 min = bounds.min;
        for (int z = 0; z < sampleZ; z++)
        {
            for (int y = 0; y < sampleY; y++)
            {
                for (int x = 0; x < sampleX; x++)
                {
                    Vector3 samplePosition = min + new Vector3(x * cellSize, y * cellSize, z * cellSize);
                    field[SampleIndex(x, y, z, sampleX, sampleY)] =
                        EvaluateField(samplePosition, particles, particleHash, influenceRadius);
                }
            }
        }

        List<Vector3> vertices = new List<Vector3>(4096);
        List<int> triangles = new List<int>(8192);
        Dictionary<Vector3Int, int> weldedVertices = new Dictionary<Vector3Int, int>(4096);
        Vector3[] cornerPositions = new Vector3[8];
        float[] cornerValues = new float[8];

        for (int z = 0; z < cellsZ; z++)
        {
            for (int y = 0; y < cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    float minValue = float.MaxValue;
                    float maxValue = float.MinValue;

                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3Int offset = CubeCornerOffsets[corner];
                        int sx = x + offset.x;
                        int sy = y + offset.y;
                        int sz = z + offset.z;

                        cornerPositions[corner] = min + new Vector3(sx * cellSize, sy * cellSize, sz * cellSize);
                        float value = field[SampleIndex(sx, sy, sz, sampleX, sampleY)];
                        cornerValues[corner] = value;
                        minValue = Mathf.Min(minValue, value);
                        maxValue = Mathf.Max(maxValue, value);
                    }

                    if (minValue >= isoLevel || maxValue < isoLevel)
                    {
                        continue;
                    }

                    for (int tet = 0; tet < 6; tet++)
                    {
                        PolygonizeTetrahedron(
                            Tetrahedra[tet, 0],
                            Tetrahedra[tet, 1],
                            Tetrahedra[tet, 2],
                            Tetrahedra[tet, 3],
                            cornerPositions,
                            cornerValues,
                            isoLevel,
                            meshTransform,
                            vertices,
                            triangles,
                            weldedVertices
                        );
                    }
                }
            }
        }

        if (vertices.Count == 0 || triangles.Count == 0)
        {
            mesh.Clear();
            return false;
        }

        Smooth(vertices, triangles, settings.smoothingIterations, settings.smoothingStrength);

        mesh.Clear();
        mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return true;
    }

    static Dictionary<Vector3Int, List<int>> BuildParticleHash(List<Vector3> particles, float cellSize)
    {
        Dictionary<Vector3Int, List<int>> hash = new Dictionary<Vector3Int, List<int>>();
        for (int i = 0; i < particles.Count; i++)
        {
            Vector3Int key = HashPosition(particles[i], cellSize);
            if (!hash.TryGetValue(key, out List<int> indices))
            {
                indices = new List<int>();
                hash.Add(key, indices);
            }

            indices.Add(i);
        }

        return hash;
    }

    static float EvaluateField(
        Vector3 samplePosition,
        List<Vector3> particles,
        Dictionary<Vector3Int, List<int>> particleHash,
        float influenceRadius)
    {
        float radiusSqr = influenceRadius * influenceRadius;
        Vector3Int sampleKey = HashPosition(samplePosition, influenceRadius);
        float value = 0f;

        for (int z = -1; z <= 1; z++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    Vector3Int key = sampleKey + new Vector3Int(x, y, z);
                    if (!particleHash.TryGetValue(key, out List<int> indices))
                    {
                        continue;
                    }

                    for (int i = 0; i < indices.Count; i++)
                    {
                        float distanceSqr = (samplePosition - particles[indices[i]]).sqrMagnitude;
                        if (distanceSqr >= radiusSqr)
                        {
                            continue;
                        }

                        float falloff = 1f - distanceSqr / radiusSqr;
                        value += falloff * falloff * falloff;
                    }
                }
            }
        }

        return value;
    }

    static void PolygonizeTetrahedron(
        int a,
        int b,
        int c,
        int d,
        Vector3[] positions,
        float[] values,
        float isoLevel,
        Transform meshTransform,
        List<Vector3> vertices,
        List<int> triangles,
        Dictionary<Vector3Int, int> weldedVertices)
    {
        int insideCount = 0;
        int i0 = -1;
        int i1 = -1;
        int i2 = -1;
        int i3 = -1;
        int o0 = -1;
        int o1 = -1;
        int o2 = -1;
        int o3 = -1;
        int outsideCount = 0;

        AddClassifiedVertex(a, values[a] >= isoLevel, ref insideCount, ref i0, ref i1, ref i2, ref i3, ref outsideCount, ref o0, ref o1, ref o2, ref o3);
        AddClassifiedVertex(b, values[b] >= isoLevel, ref insideCount, ref i0, ref i1, ref i2, ref i3, ref outsideCount, ref o0, ref o1, ref o2, ref o3);
        AddClassifiedVertex(c, values[c] >= isoLevel, ref insideCount, ref i0, ref i1, ref i2, ref i3, ref outsideCount, ref o0, ref o1, ref o2, ref o3);
        AddClassifiedVertex(d, values[d] >= isoLevel, ref insideCount, ref i0, ref i1, ref i2, ref i3, ref outsideCount, ref o0, ref o1, ref o2, ref o3);

        if (insideCount == 0 || insideCount == 4)
        {
            return;
        }

        if (insideCount == 1)
        {
            int v0 = AddInterpolatedVertex(i0, o0, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
            int v1 = AddInterpolatedVertex(i0, o1, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
            int v2 = AddInterpolatedVertex(i0, o2, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
            AddTriangle(v0, v2, v1, triangles);
            return;
        }

        if (insideCount == 3)
        {
            int v0 = AddInterpolatedVertex(o0, i0, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
            int v1 = AddInterpolatedVertex(o0, i1, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
            int v2 = AddInterpolatedVertex(o0, i2, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
            AddTriangle(v0, v1, v2, triangles);
            return;
        }

        int q0 = AddInterpolatedVertex(i0, o0, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
        int q1 = AddInterpolatedVertex(i0, o1, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
        int q2 = AddInterpolatedVertex(i1, o0, positions, values, isoLevel, meshTransform, vertices, weldedVertices);
        int q3 = AddInterpolatedVertex(i1, o1, positions, values, isoLevel, meshTransform, vertices, weldedVertices);

        AddTriangle(q0, q2, q1, triangles);
        AddTriangle(q1, q2, q3, triangles);
    }

    static void AddClassifiedVertex(
        int index,
        bool inside,
        ref int insideCount,
        ref int i0,
        ref int i1,
        ref int i2,
        ref int i3,
        ref int outsideCount,
        ref int o0,
        ref int o1,
        ref int o2,
        ref int o3)
    {
        if (inside)
        {
            SetIndexedValue(insideCount++, index, ref i0, ref i1, ref i2, ref i3);
        }
        else
        {
            SetIndexedValue(outsideCount++, index, ref o0, ref o1, ref o2, ref o3);
        }
    }

    static void SetIndexedValue(int offset, int value, ref int v0, ref int v1, ref int v2, ref int v3)
    {
        switch (offset)
        {
            case 0:
                v0 = value;
                break;
            case 1:
                v1 = value;
                break;
            case 2:
                v2 = value;
                break;
            default:
                v3 = value;
                break;
        }
    }

    static int AddInterpolatedVertex(
        int a,
        int b,
        Vector3[] positions,
        float[] values,
        float isoLevel,
        Transform meshTransform,
        List<Vector3> vertices,
        Dictionary<Vector3Int, int> weldedVertices)
    {
        float denominator = values[b] - values[a];
        float t = Mathf.Abs(denominator) > 0.00001f
            ? Mathf.Clamp01((isoLevel - values[a]) / denominator)
            : 0.5f;

        Vector3 worldPosition = Vector3.Lerp(positions[a], positions[b], t);
        Vector3 localPosition = meshTransform != null
            ? meshTransform.InverseTransformPoint(worldPosition)
            : worldPosition;

        Vector3Int key = new Vector3Int(
            Mathf.RoundToInt(localPosition.x * 10000f),
            Mathf.RoundToInt(localPosition.y * 10000f),
            Mathf.RoundToInt(localPosition.z * 10000f)
        );

        if (weldedVertices.TryGetValue(key, out int existingIndex))
        {
            return existingIndex;
        }

        int index = vertices.Count;
        vertices.Add(localPosition);
        weldedVertices.Add(key, index);
        return index;
    }

    static void AddTriangle(int a, int b, int c, List<int> triangles)
    {
        if (a == b || b == c || c == a)
        {
            return;
        }

        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    static void Smooth(List<Vector3> vertices, List<int> triangles, int iterations, float strength)
    {
        iterations = Mathf.Max(0, iterations);
        strength = Mathf.Clamp01(strength);

        if (iterations == 0 || strength <= 0f || vertices.Count == 0)
        {
            return;
        }

        List<int>[] neighbors = new List<int>[vertices.Count];
        for (int i = 0; i < triangles.Count; i += 3)
        {
            AddNeighbor(neighbors, triangles[i], triangles[i + 1]);
            AddNeighbor(neighbors, triangles[i], triangles[i + 2]);
            AddNeighbor(neighbors, triangles[i + 1], triangles[i]);
            AddNeighbor(neighbors, triangles[i + 1], triangles[i + 2]);
            AddNeighbor(neighbors, triangles[i + 2], triangles[i]);
            AddNeighbor(neighbors, triangles[i + 2], triangles[i + 1]);
        }

        Vector3[] smoothed = new Vector3[vertices.Count];
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                List<int> vertexNeighbors = neighbors[i];
                if (vertexNeighbors == null || vertexNeighbors.Count == 0)
                {
                    smoothed[i] = vertices[i];
                    continue;
                }

                Vector3 average = Vector3.zero;
                for (int n = 0; n < vertexNeighbors.Count; n++)
                {
                    average += vertices[vertexNeighbors[n]];
                }

                average /= vertexNeighbors.Count;
                smoothed[i] = Vector3.Lerp(vertices[i], average, strength);
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = smoothed[i];
            }
        }
    }

    static void AddNeighbor(List<int>[] neighbors, int vertex, int neighbor)
    {
        if (vertex == neighbor)
        {
            return;
        }

        List<int> list = neighbors[vertex];
        if (list == null)
        {
            list = new List<int>(8);
            neighbors[vertex] = list;
        }

        if (!list.Contains(neighbor))
        {
            list.Add(neighbor);
        }
    }

    static Vector3Int HashPosition(Vector3 position, float cellSize)
    {
        return new Vector3Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize),
            Mathf.FloorToInt(position.z / cellSize)
        );
    }

    static int SampleIndex(int x, int y, int z, int sampleX, int sampleY)
    {
        return x + sampleX * (y + sampleY * z);
    }
}
