using UnityEngine;

public class BucketVolume : MonoBehaviour
{
    [Header("Shape")]
    public float radius = 0.9f;
    public float height = 0.3f;
    public Transform nozzle;
    public float nozzleRadius = 0.1f;
    [Header("Shape Offset")]
    public float collisionYOffset = 0.12f;
    [Header("Fluid Feel")]
    public float bounce = 0.1f;
    public float friction = 0.2f;

    [Header("Nozzle Flow")]
    public float nozzleExitSpeed = 0.8f;
    [Tooltip("How far above the bucket floor a particle can be captured by the nozzle. Keep this SMALL (about one particle spacing) so paint leaves one thin layer at a time instead of dumping a whole slug at once.")]
    public float nozzleCaptureDepth = 0.01f;
    [Tooltip("Use the stabilized nozzle feed area. Both modes use the same nozzleRadius-based drop budget.")]
    public bool useMeteredNozzleFlow = false;
    [HideInInspector]
    public float nozzleParticlesPerSecondPerRadiusSquared = 12000f;
    [Range(0f, 1f)]
    [Tooltip("How much of the bucket floor can feed the nozzle. 1 lets the nozzle keep dripping even when the fluid is pushed to the side.")]
    public float nozzleFeedRadiusFraction = 1f;
    [Range(0f, 1f)]
    public float nozzleTangentialDamping = 0.08f;
    [Range(0f, 1f)]
    public float bucketVelocityInheritance = 0.15f;

    [Header("Slosh")]
    public float inertiaStrength = 0f;
    [Range(0f, 1f)]
    public float bucketMotionInheritance = 0.65f;

    [Header("Plane Contact")]
    [Range(0f, 1f)]
    [Tooltip("How much normal speed is reflected on impact (0 = stick/slide, 0.2 = light damped kick-back).")]
    public float planeCollisionBounce = 0.12f;
    [Range(0f, 1f)]
    public float planeCollisionFriction = 0.5f;
    [Min(0f)]
    public float planeCollisionSkin = 0.008f;
    [Min(0f)]
    [Tooltip("Tiny extra gap to avoid z-fighting. Keep very small.")]
    public float planeCollisionHullMargin = 0.006f;
    public float planeMaxBounceSpeed = 1.8f;
    [Min(0.01f)]
    [Tooltip("Inbound speed (m/s) needed to count as an impact bounce vs resting on the plane.")]
    public float planeImpactSpeedThreshold = 0.12f;
    [Range(0f, 1f)]
    [Tooltip("Scale slide along slopes; only applies when the bucket has settled (not during impact bounce).")]
    public float planeSlideGravity = 0.22f;
    [Range(0f, 25f)]
    [Tooltip("Bucket bottom is 'flat' on the plane only when axis alignment is within this many degrees.")]
    public float planeFlatContactDegrees = 12f;

    [Range(0.1f, 1f)]
    [Tooltip("Fraction of mesh/rope hull penetration corrected per physics step (lower = smoother landing).")]
    public float planeContactPostCorrectionSharpness = 0.4f;
    [Min(0f)]
    [Tooltip("Ignore tiny penetrations below this depth to avoid micro-jitter on rest.")]
    public float planeContactPostCorrectionDeadZone = 0.003f;
    [Min(0.001f)]
    [Tooltip("Max upward rope correction per FixedUpdate (meters). Caps sudden pops.")]
    public float planeContactMaxCorrectionPerStep = 0.035f;

    public float PlaneFlatAlignmentDot =>
        Mathf.Cos(planeFlatContactDegrees * Mathf.Deg2Rad);

    public float EffectivePlaneSkin => planeCollisionSkin + planeCollisionHullMargin;

    [Header("Rim Spill")]
    [Tooltip("Allow rim overflow when the bucket touches the plane (edge/flat contact).")]
    public bool enableRimSpill = true;
    [Tooltip("Also allow rim spill when the bucket is tilted without plane contact. Off keeps classic in-bucket pour.")]
    public bool enableRimSpillFromTilt = false;
    [Range(0f, 90f)]
    public float spillTiltDegrees = 35f;
    [Min(0f)]
    public float rimSpillSpeed = 1.2f;
    [Range(0f, 1f)]
    public float rimSpillFillFraction = 0.35f;
    [Min(0f)]
    public float rimSpillParticlesPerSecond = 800f;

    [Header("Twist")]
    [Range(-180f, 180f)]
    public float twistAngleDegrees;

    BucketContactMode contactMode = BucketContactMode.None;

    public BucketContactMode ContactMode => contactMode;

    public float TwistAngleRadians => twistAngleDegrees * Mathf.Deg2Rad;

    public float PlaneHalfHeight => CylinderHeightSpan * 0.5f;

    /// <summary>
    /// Distance from bucket pivot to the lowest contact ring along -bucket.up.
    /// Auto-read from CapsuleCollider when present; does not use fluid collisionYOffset.
    /// </summary>
    // PlaneCollisionBottomExtent property defined below with collider auto-fit.

    [Header("Fluid Cylinder")]
    [Range(0.05f, 0.5f)]
    [Tooltip("Fluid fill height as a fraction of bucket height. Centered on collisionYOffset.")]
    public float fluidHeightFraction = 0.2f;
    [Range(0.85f, 1f)]
    [Tooltip("Fluid wall radius = BucketVolume.radius × this scale (keep < 1 to stay inside the mesh).")]
    public float fluidCylinderRadiusScale = 0.92f;

    [Header("Plane / Hull Cylinder")]
    [Range(0.9f, 1.1f)]
    [Tooltip("Plane hull radius = BucketVolume.radius × this scale.")]
    public float planeContactRadiusScale = 1f;

    [Min(-0.05f)]
    [Tooltip("Fine-tune where the hull meets the plane. Positive raises the contact ring.")]
    public float planeContactBottomOffset = 0f;

    [Tooltip("When set > 0, overrides auto bottom extent from the collider / height.")]
    [Min(0f)]
    public float planeContactBottomExtentOverride;

    float colliderPlaneBottomExtent = -1f;
    float colliderPlaneTopExtent = -1f;
    float colliderPlaneContactRadius = -1f;
    float colliderBottomLocalY = float.NaN;
    float colliderTopLocalY = float.NaN;
    float hullAxialScale = 1f;
    float hullRadialScale = 1f;

    public float HullAxialScale => hullAxialScale;
    public float HullRadialScale => hullRadialScale;

    public float CylinderBottomLocalY =>
        !float.IsNaN(colliderBottomLocalY) ? colliderBottomLocalY : -height * 0.5f;

    public float CylinderTopLocalY =>
        !float.IsNaN(colliderTopLocalY) ? colliderTopLocalY : height * 0.5f;

    /// <summary>Inner paint region (shifted-space half height). Not the outer capsule hull.</summary>
    public float FluidHalfHeight => height * fluidHeightFraction;

    public float FluidFloorLocalY => collisionYOffset - FluidHalfHeight;
    public float FluidCeilingLocalY => collisionYOffset + FluidHalfHeight;

    public float FluidWallRadius =>
        Mathf.Max(0.01f, radius * fluidCylinderRadiusScale);

    public float CylinderWallRadius => FluidWallRadius;

    public float CylinderHeightSpan => FluidCeilingLocalY - FluidFloorLocalY;

    public float CollisionFloorShiftedY => -FluidHalfHeight;
    public float CollisionCeilingShiftedY => FluidHalfHeight;

    public float PlaneContactRadius
    {
        get
        {
            float configured = radius * planeContactRadiusScale * hullRadialScale;
            if (colliderPlaneContactRadius >= 0f)
                return Mathf.Max(0.01f, configured, colliderPlaneContactRadius);
            return Mathf.Max(0.01f, configured);
        }
    }

    public float PlaneCollisionBottomExtent
    {
        get
        {
            if (planeContactBottomExtentOverride > 0f)
                return planeContactBottomExtentOverride * hullAxialScale + planeContactBottomOffset;

            float baseExtent = colliderPlaneBottomExtent >= 0f
                ? colliderPlaneBottomExtent
                : Mathf.Max(0f, -CylinderBottomLocalY, radius * 0.15f) * hullAxialScale;
            return baseExtent + planeContactBottomOffset;
        }
    }

    public float PlaneCollisionTopExtent
    {
        get
        {
            if (planeContactBottomExtentOverride > 0f)
                return planeContactBottomExtentOverride * hullAxialScale;

            return colliderPlaneTopExtent >= 0f
                ? colliderPlaneTopExtent
                : Mathf.Max(0f, CylinderTopLocalY) * hullAxialScale;
        }
    }

    public void GetPlaneHullWorld(
        out Vector3 center,
        out Vector3 axis,
        out float bottomExtent,
        out float topExtent,
        out float hullRadius)
    {
        center = transform.position;
        axis = transform.up;
        bottomExtent = PlaneCollisionBottomExtent;
        topExtent = PlaneCollisionTopExtent;
        hullRadius = PlaneContactRadius;
    }

    Vector3 cachedLossyScale = Vector3.one;
    MeshFilter[] hullMeshFilters;
    bool meshLocalExtentsCached;
    float meshBottomLocalY;
    float meshTopLocalY;

    void Awake()
    {
        cachedLossyScale = transform.lossyScale;
        CacheHullMeshFilters();
        RefreshColliderPlaneGeometry();
        lastPosition = transform.position;
    }

    void OnValidate()
    {
        if (Application.isPlaying)
            return;

        meshLocalExtentsCached = false;
        CacheHullMeshFilters();
        RefreshColliderPlaneGeometry();
    }

    void CacheHullMeshFilters()
    {
        if (hullMeshFilters == null || hullMeshFilters.Length == 0)
            hullMeshFilters = GetComponentsInChildren<MeshFilter>(true);
    }

    void CacheMeshLocalExtents(float fallbackBottomY, float fallbackTopY)
    {
        if (meshLocalExtentsCached)
            return;

        meshBottomLocalY = fallbackBottomY;
        meshTopLocalY = fallbackTopY;
        CacheHullMeshFilters();

        if (hullMeshFilters != null)
        {
            for (int i = 0; i < hullMeshFilters.Length; i++)
            {
                MeshFilter mf = hullMeshFilters[i];
                if (mf == null || mf.sharedMesh == null)
                    continue;

                Bounds b = mf.sharedMesh.bounds;
                Vector3 bottom = transform.InverseTransformPoint(
                    mf.transform.TransformPoint(b.center - Vector3.up * b.extents.y));
                Vector3 top = transform.InverseTransformPoint(
                    mf.transform.TransformPoint(b.center + Vector3.up * b.extents.y));
                meshBottomLocalY = Mathf.Min(meshBottomLocalY, bottom.y, top.y);
                meshTopLocalY = Mathf.Max(meshTopLocalY, bottom.y, top.y);
            }
        }

        meshLocalExtentsCached = true;
    }

    void RefreshColliderPlaneGeometry()
    {
        colliderPlaneBottomExtent = -1f;
        colliderPlaneTopExtent = -1f;
        colliderPlaneContactRadius = -1f;
        colliderBottomLocalY = float.NaN;
        colliderTopLocalY = float.NaN;

        Vector3 lossy = transform.lossyScale;
        hullAxialScale = Mathf.Max(Mathf.Abs(lossy.y), 1e-4f);
        hullRadialScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z), 1e-4f);

        float bottomLocalY = -height * 0.5f;
        float topLocalY = height * 0.5f;

        if (TryGetComponent<CapsuleCollider>(out var capsule) && capsule.direction == 1)
        {
            bottomLocalY = capsule.center.y - capsule.height * 0.5f;
            topLocalY = capsule.center.y + capsule.height * 0.5f;
            colliderPlaneContactRadius = capsule.radius * hullRadialScale;
        }

        colliderBottomLocalY = bottomLocalY;
        colliderTopLocalY = topLocalY;
        colliderPlaneBottomExtent = Mathf.Max(0f, -bottomLocalY) * hullAxialScale;
        colliderPlaneTopExtent = Mathf.Max(0f, topLocalY) * hullAxialScale;

        CacheMeshLocalExtents(bottomLocalY, topLocalY);
        colliderBottomLocalY = meshBottomLocalY;
        colliderTopLocalY = meshTopLocalY;
        colliderPlaneBottomExtent = Mathf.Max(colliderPlaneBottomExtent, Mathf.Max(0f, -meshBottomLocalY) * hullAxialScale);
        colliderPlaneTopExtent = Mathf.Max(colliderPlaneTopExtent, Mathf.Max(0f, meshTopLocalY) * hullAxialScale);
    }

    public float GetPlaneCollisionExtent(float normalAlongBucketUp)
    {
        float radialNormalAmount = Mathf.Sqrt(Mathf.Max(0f, 1f - normalAlongBucketUp * normalAlongBucketUp));
        float axialExtent = Mathf.Max(PlaneCollisionBottomExtent, PlaneCollisionTopExtent);
        return axialExtent * Mathf.Abs(normalAlongBucketUp) + PlaneContactRadius * radialNormalAmount;
    }

    public Vector3 GetPreferredPlaneContactRight(Transform bucketTransform, Vector3 bucketUp, Vector3 planeNormal)
    {
        if (bucketTransform != null)
            return bucketTransform.right;

        return Vector3.Cross(bucketUp, planeNormal);
    }

    Vector3 lastPosition;
    Vector3 bucketVelocity;

    public Vector3 Velocity => bucketVelocity;

    public float MeteredNozzleParticlesPerSecond
    {
        get
        {
            float radius = Mathf.Max(0f, nozzleRadius);
            float flowScale = radius * radius;
            return Mathf.Max(0f, nozzleParticlesPerSecondPerRadiusSquared) * flowScale;
        }
    }

    public bool Constrain(ref Vector3 pos, ref Vector3 vel)
    {
        Vector3 localPos = transform.InverseTransformPoint(pos);
        Vector3 localVel = transform.InverseTransformDirection(vel);
        localPos.y -= collisionYOffset;
        float floorY = CollisionFloorShiftedY;
        float ceilingY = CollisionCeilingShiftedY;
        float wallRadius = FluidWallRadius;

        // inertia
        Vector3 inertialForce = -bucketVelocity * inertiaStrength;
        localVel += transform.InverseTransformDirection(inertialForce) * Time.deltaTime;

        Vector2 xz = new Vector2(localPos.x, localPos.z);
        float dist = xz.magnitude;
        float collisionFriction = Mathf.Clamp01(friction);
        // float margin = 0.3f;

        // if(localPos.y < -halfH - margin ||localPos.y >  halfH + margin)
        // {
        //     return;
        // }
        // Capture only a THIN layer right at the bucket floor. A large capture
        // depth grabs a big slug of overlapping particles in one frame, which
        // then bursts apart. A thin layer meters the flow into a steady stream.
        if(dist <= nozzleRadius && localPos.y < floorY + nozzleCaptureDepth)
        {
            // Place the particle just below the nozzle opening (done in local space).
            localPos.y = floorY - 0.002f;
            localPos.y += collisionYOffset;
            pos = transform.TransformPoint(localPos);

            // The nozzle METERS the flow: every particle leaves at the same
            // fixed downward speed in WORLD space, no matter how much pressure
            // the fluid built up inside. Passing the internal velocity through
            // (the old Max(|v|, exitSpeed)) let pressure spikes eject particles
            // violently, which is what made the stream burst at the nozzle.
            Vector3 slosh = transform.TransformDirection(localVel);
            Vector3 worldVel;
            worldVel.y = -nozzleExitSpeed;
            worldVel.x = slosh.x * nozzleTangentialDamping
                       + bucketVelocity.x * bucketVelocityInheritance;
            worldVel.z = slosh.z * nozzleTangentialDamping
                       + bucketVelocity.z * bucketVelocityInheritance;

            vel = worldVel;
            return false;
        }

        // top/bottom
        else if (localPos.y < floorY)
        {
            localPos.y = floorY;
            localVel.y = Mathf.Abs(localVel.y) * bounce;
            localVel.x *= 1f - collisionFriction;
            localVel.z *= 1f - collisionFriction;
        }
        else if (localPos.y > ceilingY)
        {
            localPos.y = ceilingY;
            localVel.y = -Mathf.Abs(localVel.y) * bounce;
            localVel.x *= 1f - collisionFriction;
            localVel.z *= 1f - collisionFriction;
        }

        // cylinder wall
        // Vector2 xz = new Vector2(localPos.x, localPos.z);
        // float dist = xz.magnitude;

        if (dist > wallRadius)
        {

            Vector2 normal = xz.normalized;
            xz = normal * wallRadius;

            localPos.x = xz.x;
            localPos.z = xz.y;

            Vector2 velXZ = new Vector2(localVel.x, localVel.z);
            float normalVelocity = Vector2.Dot(velXZ, normal);
            Vector2 tangentVelocity = velXZ - normalVelocity * normal;

            if (normalVelocity > 0f)
            {
                normalVelocity = -normalVelocity * bounce;
            }

            velXZ = normal * normalVelocity + tangentVelocity;

            localVel.x = velXZ.x;
            localVel.z = velXZ.y;

        }



        // if (localPos.y > nozzle.y)
        // {
        //     EmitPaint();
        // }

        // global damping to remove energy
        localVel *= 0.98f;
        localPos.y += collisionYOffset;
        pos = transform.TransformPoint(localPos);
        vel = transform.TransformDirection(localVel);
        return true;
    }

    public void SetExternalVelocity(Vector3 vel)
    {
        bucketVelocity = vel;
    }

    public void SetContactMode(BucketContactMode mode)
    {
        contactMode = mode;
    }

    public void SetTwistAngleDegrees(float degrees)
    {
        twistAngleDegrees = Mathf.Repeat(degrees + 180f, 360f) - 180f;
    }

    public void AddTwistDelta(float deltaDegrees)
    {
        SetTwistAngleDegrees(twistAngleDegrees + deltaDegrees);
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;

        float bottomY = FluidFloorLocalY;
        float topY = FluidCeilingLocalY;
        float cylRadius = FluidWallRadius;

        Gizmos.DrawWireSphere(new Vector3(0f, bottomY, 0f), cylRadius);
        Gizmos.DrawWireSphere(new Vector3(0f, topY, 0f), cylRadius);

        const int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            Vector3 p0b = new Vector3(Mathf.Cos(a0) * cylRadius, bottomY, Mathf.Sin(a0) * cylRadius);
            Vector3 p1b = new Vector3(Mathf.Cos(a1) * cylRadius, bottomY, Mathf.Sin(a1) * cylRadius);
            Vector3 p0t = new Vector3(Mathf.Cos(a0) * cylRadius, topY, Mathf.Sin(a0) * cylRadius);
            Vector3 p1t = new Vector3(Mathf.Cos(a1) * cylRadius, topY, Mathf.Sin(a1) * cylRadius);
            Gizmos.DrawLine(p0b, p1b);
            Gizmos.DrawLine(p0t, p1t);
            Gizmos.DrawLine(p0b, p0t);
        }
    }
}
