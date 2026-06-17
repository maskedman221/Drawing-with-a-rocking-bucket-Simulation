// using System.Collections.Generic;
// using UnityEngine;

// public class PBFManager : MonoBehaviour
// {
//     [Header("Particles")]
//     public int particleCount = 125;
//     public GameObject particlePrefab;

//     [Header("Physics")]
//     public float gravity = -9.81f;
//     public float dt = 0.005f;
//     public float restDensity = 10f;
//     public float smoothingRadius = 1f;
//     public float mass = 10f;

//     [Header("Solver")]
//     public int solverIterations = 5;
//     public float lambda = 0.5f;

//     [Header("Damping (paint feel)")]
//     public float velocityDamping = 0.98f;

//     class Particle
//     {
//         public Vector3 position;
//         public Vector3 predictedPosition;
//         public Vector3 velocity;
//         public float density;
//         public GameObject obj;
//     }

//     List<Particle> particles = new List<Particle>();

//     Dictionary<Vector3Int, List<Particle>> grid = new Dictionary<Vector3Int, List<Particle>>();

//     #region KERNELS

//     float Poly6(float r2, float h)
//     {
//         float h2 = h * h;
//         float diff = h2 - r2;
//         if (diff > 0)
//             return diff * diff * diff;
//         return 0f;
//     }

//     float Spiky(float r, float h)
//     {
//         if (r > 0 && r < h)
//         {
//             float x = h - r;
//             return x * x;
//         }
//         return 0f;
//     }

//     #endregion

//     #region INIT

//     void Start()
//     {
//         int gridSize = 5;
//         float spacing = 0.5f;

//         for (int x = 0; x < gridSize; x++)
//         for (int y = 0; y < gridSize; y++)
//         for (int z = 0; z < gridSize; z++)
//         {
//             Particle p = new Particle();

//             Vector3 pos = transform.position + new Vector3(
//                 x * spacing,
//                 y * spacing + 2f,
//                 z * spacing
//             );

//             p.position = pos;
//             p.predictedPosition = pos;
//             p.velocity = Vector3.zero;

//             p.obj = Instantiate(particlePrefab, pos, Quaternion.identity);

//             particles.Add(p);
//         }
//     }

//     #endregion

//     #region GRID

//     Vector3Int GetCell(Vector3 pos)
//     {
//         int c = Mathf.FloorToInt(smoothingRadius);
//         return new Vector3Int(
//             Mathf.FloorToInt(pos.x / c),
//             Mathf.FloorToInt(pos.y / c),
//             Mathf.FloorToInt(pos.z / c)
//         );
//     }

//     void BuildGrid()
//     {
//         grid.Clear();

//         foreach (var p in particles)
//         {
//             Vector3Int cell = GetCell(p.predictedPosition);

//             if (!grid.ContainsKey(cell))
//                 grid[cell] = new List<Particle>();

//             grid[cell].Add(p);
//         }
//     }

//     List<Particle> GetNeighbors(Particle p)
//     {
//         List<Particle> result = new List<Particle>();

//         Vector3Int cell = GetCell(p.predictedPosition);

//         for (int x = -1; x <= 1; x++)
//         for (int y = -1; y <= 1; y++)
//         for (int z = -1; z <= 1; z++)
//         {
//             Vector3Int ncell = cell + new Vector3Int(x, y, z);

//             if (grid.TryGetValue(ncell, out var list))
//                 result.AddRange(list);
//         }

//         return result;
//     }

//     #endregion

//     #region PBF CORE

//     void PredictPositions()
//     {
//         foreach (var p in particles)
//         {
//             p.velocity += new Vector3(0, gravity, 0) * dt;
//             p.predictedPosition = p.position + p.velocity * dt;
//         }
//     }

//     void ComputeDensities()
//     {
//         foreach (var p in particles)
//         {
//             float density = 0f;

//             foreach (var n in GetNeighbors(p))
//             {
//                 float r2 = (p.predictedPosition - n.predictedPosition).sqrMagnitude;
//                 density += mass * Poly6(r2, smoothingRadius);
//             }

//             p.density = density;
//         }
//     }

//    void SolveConstraints()
//     {
//         foreach (var p in particles)
//         {
//             float constraint = (p.density - restDensity);

//             if (constraint <= 0) continue; // IMPORTANT stability trick

//             Vector3 correction = Vector3.zero;
//             float totalWeight = 0f;

//             foreach (var n in GetNeighbors(p))
//             {
//                 if (p == n) continue;

//                 Vector3 dir = p.predictedPosition - n.predictedPosition;
//                 float dist = dir.magnitude;

//                 if (dist > 0.0001f && dist < smoothingRadius)
//                 {
//                     float w = Spiky(dist, smoothingRadius);
//                     correction += dir.normalized * w;
//                     totalWeight += w;
//                 }
//             }

//             if (totalWeight > 0)
//             {
//                 correction /= totalWeight;
//                 p.predictedPosition -= correction * lambda * constraint * 0.01f;
//             }
//         }
//     }
//     void ResolveCollisions(Particle p)
//     {
//         Vector3 min = Vector3.zero;
//         Vector3 max = new Vector3(5, 5, 5); // you can replace with bucket size

//         Vector3 pos = p.predictedPosition;

//         float bounce = 0.3f;

//         // X
//         if (pos.x < min.x)
//         {
//             pos.x = min.x;
//             p.velocity.x *= -bounce;
//         }
//         else if (pos.x > max.x)
//         {
//             pos.x = max.x;
//             p.velocity.x *= -bounce;
//         }

//         // Y (ground)
//         if (pos.y < min.y)
//         {
//             pos.y = min.y;
//             p.velocity.y *= -bounce;
//         }
//         else if (pos.y > max.y)
//         {
//             pos.y = max.y;
//             p.velocity.y *= -bounce;
//         }

//         // Z
//         if (pos.z < min.z)
//         {
//             pos.z = min.z;
//             p.velocity.z *= -bounce;
//         }
//         else if (pos.z > max.z)
//         {
//             pos.z = max.z;
//             p.velocity.z *= -bounce;
//         }

//         p.predictedPosition = pos;
//     }
//     void UpdateVelocities()
//     {
//         foreach (var p in particles)
//         {
//             p.velocity = (p.predictedPosition - p.position) / dt;
//             p.velocity *= velocityDamping;
//         }
//     }

//     void UpdatePositions()
//     {
//         foreach (var p in particles)
//         {
//             p.position = p.predictedPosition;
//             p.obj.transform.position = p.position;
//         }
//     }

//     #endregion

//     #region SIMULATION LOOP

//     void Update()
//     {
//         Simulate();
//     }

//     void Simulate()
//     {
//         PredictPositions();

//         BuildGrid();

//         for (int i = 0; i < solverIterations; i++)
//         {
//             ComputeDensities();
//             SolveConstraints();
//             foreach (var p in particles)
//                 ResolveCollisions(p);
//         }

//         UpdateVelocities();
//         UpdatePositions();
//     }

//     #endregion
// }