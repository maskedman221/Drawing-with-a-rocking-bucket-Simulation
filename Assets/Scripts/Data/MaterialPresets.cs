using UnityEngine;

public class MaterialPresets : MonoBehaviour
{
    public static SurfaceMaterial CreatePaperMaterial()
    {
        SurfaceMaterial mat = ScriptableObject.CreateInstance<SurfaceMaterial>();
        mat.restitution = 0f;
        mat.absorption = 1f;
        mat.staticFriction = 0.9f;
        mat.dynamicFriction = 0.72f;
        mat.spread = 0.22f;
        mat.wetnessSlideFactor = 0.25f;
        mat.paintViscosity = 4f;
        mat.stopSpeedThreshold = 0.022f;
        return mat;
    }

    public SurfaceMaterial CreateWoodMaterial()
    {
        SurfaceMaterial mat = ScriptableObject.CreateInstance<SurfaceMaterial>();
        mat.restitution = 0.1f;
        mat.absorption = 0.85f;
        mat.staticFriction = 0.82f;
        mat.dynamicFriction = 0.62f;
        mat.spread = 0.06f;
        mat.wetnessSlideFactor = 0.28f;
        mat.paintViscosity = 3.8f;
        mat.stopSpeedThreshold = 0.02f;
        return mat;
    }

    public SurfaceMaterial CreateGlassMaterial()
    {
        SurfaceMaterial mat = ScriptableObject.CreateInstance<SurfaceMaterial>();
        mat.restitution = 0.72f;
        mat.absorption = 0f;
        mat.staticFriction = 0.12f;
        mat.dynamicFriction = 0.06f;
        mat.spread = 0.008f;
        mat.wetnessSlideFactor = 0.85f;
        mat.paintViscosity = 0.8f;
        mat.stopSpeedThreshold = 0.014f;
        return mat;
    }

    public SurfaceMaterial CreateWetLandMaterial()
    {
        SurfaceMaterial mat = ScriptableObject.CreateInstance<SurfaceMaterial>();
        mat.restitution = 0.42f;
        mat.absorption = 0.55f;
        mat.staticFriction = 0.72f;
        mat.dynamicFriction = 0.48f;
        mat.spread = 0.05f;
        mat.wetness = 0.35f;
        mat.wetnessSlideFactor = 0.45f;
        mat.paintViscosity = 3.2f;
        mat.stopSpeedThreshold = 0.019f;
        return mat;
    }

    public SurfaceMaterial CreateCarpetMaterial()
    {
        SurfaceMaterial mat = ScriptableObject.CreateInstance<SurfaceMaterial>();
        mat.restitution = 0f;
        mat.absorption = 0.95f;
        mat.staticFriction = 0.92f;
        mat.dynamicFriction = 0.78f;
        mat.spread = 0.28f;
        mat.wetnessSlideFactor = 0.2f;
        mat.paintViscosity = 4.5f;
        mat.stopSpeedThreshold = 0.024f;
        return mat;
    }
}
