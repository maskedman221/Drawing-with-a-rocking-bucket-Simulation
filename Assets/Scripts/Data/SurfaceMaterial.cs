using UnityEngine;



[System.Serializable]

[CreateAssetMenu(fileName = "SurfaceMaterial", menuName = "Fluid/Surface Material")]

public class SurfaceMaterial : ScriptableObject

{

    [Header("Impact")]

    [Range(0f, 1f)]

    [Tooltip("Material elasticity on impact (0 = stick, 1 = elastic).")]

    public float restitution = 0.3f;



    [Range(0f, 1f)]

    [Tooltip("Tangential energy absorbed on impact — lowers splash/slide, not the normal bounce.")]

    public float absorption = 0.5f;



    [Range(0f, 1f)]

    public float staticFriction = 0.7f;



    [Range(0f, 1f)]

    public float dynamicFriction = 0.4f;



    [Range(0f, 1f)]

    [Tooltip("Random tangential splash on impact (strength × impact speed).")]

    public float spread = 0.06f;



    [Header("Wetness")]

    [Range(0f, 1f)]

    [Tooltip("Ambient surface wetness — reduces friction via wetnessSlideFactor.")]

    public float wetness = 0f;



    [Range(0f, 1f)]

    [Tooltip("How much ambient wetness reduces friction.")]

    public float wetnessSlideFactor = 0.5f;



    [Header("Slide & Settle")]

    [Min(0f)]

    [Tooltip("Viscous damping while sliding on the surface.")]

    public float paintViscosity = 2f;



    [Min(0.001f)]

    [Tooltip("Speed below which a particle freezes in place.")]

    public float stopSpeedThreshold = 0.015f;

}


