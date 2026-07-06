using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "SurfaceMaterial", menuName = "Fluid/Surface Material")]
public class SurfaceMaterial : ScriptableObject
{
    [Header("Physical Properties")]
    [Range(0f, 1f)]
    public float restitution = 0.3f;    // Bounce (0 = no bounce, 1 = perfect bounce)
    
    [Range(0f, 1f)]
    public float friction = 0.5f;       // Sliding resistance (0 = no friction, 1 = full friction)
    
    [Range(0f, 1f)]
    public float roughness = 0.2f;      // Random spreading on impact
    
    [Header("Visual Properties")]
    [Range(0f, 1f)]
    public float absorption = 0.5f;     // How much paint is absorbed (for future use)
    
    [Range(0f, 1f)]
    public float wetness = 0f;          // How wet the surface is (for future use)
}

// Alternative MonoBehaviour version if you prefer to attach to GameObjects
/*
public class SurfaceMaterial : MonoBehaviour
{
    [Range(0f, 1f)]
    public float restitution = 0.3f;
    
    [Range(0f, 1f)]
    public float friction = 0.5f;
    
    [Range(0f, 1f)]
    public float roughness = 0.2f;
    
    [Range(0f, 1f)]
    public float absorption = 0.5f;
}
*/