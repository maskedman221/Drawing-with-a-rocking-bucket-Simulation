using UnityEngine;

public class PaintingCollision : MonoBehaviour
{
    [Header("References")]
    public Transform plane;
    public SurfaceMaterial surfaceMaterial;

    [Header("Runtime Wetness")]
    [Tooltip("Ambient wetness from material.wetness (read-only at runtime).")]
    [SerializeField, ReadOnly]
    float surfaceWetnessDisplay;

    [Header("Gizmo")]
    public bool showWetnessGizmo = true;

    public float SurfaceWetness =>
        surfaceMaterial != null ? Mathf.Clamp01(surfaceMaterial.wetness) : 0f;

    void LateUpdate()
    {
        surfaceWetnessDisplay = SurfaceWetness;
    }

    public Vector3 GetPlaneNormal()
    {
        if (plane == null)
            return Vector3.up;

        return plane.up.sqrMagnitude > 1e-8f ? plane.up.normalized : Vector3.up;
    }

    public Vector3 GetPlanePosition() => plane != null ? plane.position : Vector3.zero;

    public SurfaceCollisionMath.Result Constrain(
        ref Vector3 pos,
        ref Vector3 vel,
        bool onPlane,
        float deltaTime,
        float gravityY,
        uint particleIndex)
    {
        if (plane == null || surfaceMaterial == null)
            return default;

        SurfaceMaterial mat = surfaceMaterial;
        Vector3 gravityWorld = new Vector3(0f, gravityY, 0f);

        SurfaceCollisionMath.Result result = SurfaceCollisionMath.Resolve(
            pos,
            vel,
            GetPlanePosition(),
            GetPlaneNormal(),
            mat.restitution,
            mat.staticFriction,
            mat.dynamicFriction,
            mat.spread,
            mat.absorption,
            SurfaceWetness,
            mat.wetnessSlideFactor,
            mat.paintViscosity,
            mat.stopSpeedThreshold,
            deltaTime,
            gravityWorld,
            particleIndex,
            onPlane);

        if (!result.hadContact)
            return result;

        pos = result.position;
        vel = result.velocity;
        return result;
    }

    void OnDrawGizmos()
    {
        if (!showWetnessGizmo || plane == null || !Application.isPlaying)
            return;

        DrawWetnessGizmo();
    }

    void OnDrawGizmosSelected()
    {
        if (!showWetnessGizmo || plane == null || Application.isPlaying)
            return;

        DrawWetnessGizmo();
    }

    void DrawWetnessGizmo()
    {
        Color dryColor = new Color(0.85f, 0.75f, 0.45f, 0.85f);
        Color wetColor = new Color(0.15f, 0.45f, 0.95f, 0.95f);
        Gizmos.color = Color.Lerp(dryColor, wetColor, SurfaceWetness);

        Vector3 normal = GetPlaneNormal();
        Vector3 center = plane.position + normal * 0.003f;
        Vector3 right = plane.right;
        Vector3 forward = plane.forward;
        if (right.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f)
            return;

        float half = 0.55f;
        Vector3 p0 = center - right * half - forward * half;
        Vector3 p1 = center + right * half - forward * half;
        Vector3 p2 = center + right * half + forward * half;
        Vector3 p3 = center - right * half + forward * half;
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);
        Gizmos.DrawLine(p0, p2);
        Gizmos.DrawLine(p1, p3);
    }
}
