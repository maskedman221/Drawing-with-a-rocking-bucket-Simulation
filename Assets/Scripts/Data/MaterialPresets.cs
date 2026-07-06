using UnityEngine;
public class MaterialPresets : MonoBehaviour
{
    public SurfaceMaterial CreateWoodMaterial()
    {
        SurfaceMaterial mat = ScriptableObject.CreateInstance<SurfaceMaterial>();
        mat.restitution = 0.2f;   // Low bounce
        mat.friction = 0.7f;      // High friction
        mat.roughness = 0.3f;     // Some spread
        mat.absorption = 0.8f;    // Absorbs paint
        return mat;
    }
    
    public SurfaceMaterial CreateGlassMaterial()
    {
        SurfaceMaterial mat = ScriptableObject.CreateInstance<SurfaceMaterial>();
        mat.restitution = 0.9f;   // High bounce
        mat.friction = 0.1f;      // Very slippery
        mat.roughness = 0.0f;     // No spread
        mat.absorption = 0.0f;    // No absorption
        return mat;
    }
    
    public SurfaceMaterial CreateCarpetMaterial()
    {
        SurfaceMaterial mat = ScriptableObject.CreateInstance<SurfaceMaterial>();
        mat.restitution = 0.0f;   // No bounce
        mat.friction = 0.9f;      // High friction
        mat.roughness = 0.8f;     // Lots of spread
        mat.absorption = 0.9f;    // High absorption
        return mat;
    }
}