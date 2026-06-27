using UnityEngine;

[System.Serializable]
public class SurfaceMaterial : MonoBehaviour
{
    [Range(0f, 1f)]
    public float restitution;   // Bounce
    [Range(0f, 1f)]
    public float friction;      // Sliding resistance
    [Range(0f, 1f)]
    public float absorption;    // Paint absorption
    [Range(0f, 1f)]
    public float roughness;     // Random spreading
}
