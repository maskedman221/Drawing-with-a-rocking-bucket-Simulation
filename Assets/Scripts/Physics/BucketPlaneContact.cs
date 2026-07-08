using UnityEngine;

public enum BucketContactMode
{
    None = 0,
    Edge = 1,
    Flat = 2
}

/// <summary>
/// Capped cylinder vs plane: analytic signed distance + CPU mirror of BucketPlaneCollision.compute.
/// Cylinder axis = bucketUp, cross-section (r, θ) in (right, forward).
/// Plane: dot(P - planePosition, planeNormal) = 0.
/// </summary>
public static class BucketPlaneContact
{
    public const int MaxClampIterations = 8;

    public static float SignedPlaneDistance(Vector3 point, Vector3 planePosition, Vector3 planeNormal)
    {
        return Vector3.Dot(point - planePosition, planeNormal);
    }

    public static void BuildBucketAxes(
        Vector3 bucketUp,
        Vector3 planeNormal,
        Vector3 preferredRight,
        out Vector3 right,
        out Vector3 forward)
    {
        bucketUp = bucketUp.sqrMagnitude > 1e-8f ? bucketUp.normalized : Vector3.up;
        planeNormal = planeNormal.sqrMagnitude > 1e-8f ? planeNormal.normalized : Vector3.up;

        right = Vector3.ProjectOnPlane(preferredRight, bucketUp);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.Cross(bucketUp, planeNormal);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.Cross(bucketUp, Vector3.forward);
        right.Normalize();

        forward = Vector3.Cross(right, bucketUp);
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.Cross(bucketUp, Vector3.right);
        forward.Normalize();
    }

    /// <summary>
    /// Minimum signed distance from plane to capped cylinder hull (negative = penetration).
    /// Uses plane/cylinder geometry: z(θ) = -(Ar cos θ + Br sin θ + D)/C in the bucket cross-section basis.
    /// </summary>
    public static float CylinderPlaneMinDistance(
        Vector3 bucketCenter,
        Vector3 bucketAxis,
        float bottomExtent,
        float topExtent,
        float radius,
        Vector3 planePosition,
        Vector3 planeNormal)
    {
        bucketAxis = bucketAxis.sqrMagnitude > 1e-8f ? bucketAxis.normalized : Vector3.up;
        planeNormal = planeNormal.sqrMagnitude > 1e-8f ? planeNormal.normalized : Vector3.up;

        float nParallel = Vector3.Dot(planeNormal, bucketAxis);
        float nPerp = Mathf.Sqrt(Mathf.Max(0f, 1f - nParallel * nParallel));

        float tMin = -bottomExtent;
        float tMax = topExtent;
        float axialBase = Vector3.Dot(bucketCenter - planePosition, planeNormal);

        float dAtMin = axialBase + tMin * nParallel - radius * nPerp;
        float dAtMax = axialBase + tMax * nParallel - radius * nPerp;
        return Mathf.Min(dAtMin, dAtMax);
    }

    public static float SampleMinimumPlaneDistance(
        Vector3 ropeEnd,
        Vector3 bucketUp,
        Vector3 bucketRight,
        Vector3 bucketForward,
        Vector3 planePosition,
        Vector3 planeNormal,
        float attachmentDistance,
        float bottomExtentFromCenter,
        float topExtentFromCenter,
        float bucketRadius)
    {
        Vector3 bucketCenter = ropeEnd - bucketUp * attachmentDistance;
        return CylinderPlaneMinDistance(
            bucketCenter,
            bucketUp,
            bottomExtentFromCenter,
            topExtentFromCenter,
            bucketRadius,
            planePosition,
            planeNormal);
    }

    /// <summary>
    /// Damped wall response: reflect normal component with restitution, friction on tangent.
    /// v' = v - (1+e)*vn*n for inbound contact (vn &lt; 0), plus tangential damping.
    /// </summary>
    public static Vector3 ResolvePlaneContactVelocity(
        Vector3 incomingVelocity,
        Vector3 planeNormal,
        float restitution,
        float friction,
        bool inContact)
    {
        planeNormal = planeNormal.sqrMagnitude > 1e-8f ? planeNormal.normalized : Vector3.up;
        restitution = Mathf.Clamp01(restitution);
        friction = Mathf.Clamp01(friction);

        float vn = Vector3.Dot(incomingVelocity, planeNormal);
        Vector3 tangent = incomingVelocity - vn * planeNormal;

        if (!inContact)
            return incomingVelocity;

        if (vn < 0f)
        {
            float outbound = -vn * restitution;
            return tangent * (1f - friction) + planeNormal * outbound;
        }

        return tangent;
    }

    public static Vector3 ClampRopeEndAbovePlane(
        Vector3 ropeEnd,
        Vector3 anchorPosition,
        Vector3 planePosition,
        Vector3 planeNormal,
        float bucketRadius,
        float bottomExtentFromCenter,
        float topExtentFromCenter,
        float attachmentDistance,
        float skin,
        Vector3 bucketUpHint,
        Vector3 preferredBucketRight)
    {
        planeNormal = planeNormal.sqrMagnitude > 1e-8f ? planeNormal.normalized : Vector3.up;
        Vector3 rope = ropeEnd;

        for (int iter = 0; iter < MaxClampIterations; iter++)
        {
            Vector3 bucketUp = bucketUpHint.sqrMagnitude > 1e-6f
                ? bucketUpHint.normalized
                : (anchorPosition - rope).sqrMagnitude > 1e-6f
                    ? (anchorPosition - rope).normalized
                    : Vector3.up;

            float minDistance = SampleMinimumPlaneDistance(
                rope,
                bucketUp,
                Vector3.zero,
                Vector3.zero,
                planePosition,
                planeNormal,
                attachmentDistance,
                bottomExtentFromCenter,
                topExtentFromCenter,
                bucketRadius);

            if (minDistance < skin - 0.0001f)
                rope += planeNormal * (skin - minDistance);
            else
                break;
        }

        return rope;
    }

    public static Vector3 SnapRopeEndToPlaneContact(
        Vector3 ropeEnd,
        Vector3 anchorPosition,
        Vector3 planePosition,
        Vector3 planeNormal,
        float bucketRadius,
        float bottomExtentFromCenter,
        float topExtentFromCenter,
        float attachmentDistance,
        float skin,
        Vector3 bucketUpHint,
        Vector3 preferredBucketRight)
    {
        planeNormal = planeNormal.sqrMagnitude > 1e-8f ? planeNormal.normalized : Vector3.up;
        Vector3 bucketUp = bucketUpHint.sqrMagnitude > 1e-6f
            ? bucketUpHint.normalized
            : (anchorPosition - ropeEnd).sqrMagnitude > 1e-6f
                ? (anchorPosition - ropeEnd).normalized
                : Vector3.up;

        float minDistance = SampleMinimumPlaneDistance(
            ropeEnd,
            bucketUp,
            Vector3.zero,
            Vector3.zero,
            planePosition,
            planeNormal,
            attachmentDistance,
            bottomExtentFromCenter,
            topExtentFromCenter,
            bucketRadius);

        return ropeEnd - planeNormal * (minDistance - skin);
    }

    public static BucketContactMode Classify(
        Vector3 ropeEnd,
        Vector3 anchorPosition,
        Vector3 planePosition,
        Vector3 planeNormal,
        float bucketRadius,
        float bottomExtentFromCenter,
        float topExtentFromCenter,
        float attachmentDistance,
        float skin,
        Vector3 preferredBucketRight,
        float flatAlignmentDotThreshold,
        Vector3 bucketUpHint)
    {
        planeNormal = planeNormal.sqrMagnitude > 1e-8f ? planeNormal.normalized : Vector3.up;
        Vector3 bucketUp = bucketUpHint.sqrMagnitude > 1e-6f
            ? bucketUpHint.normalized
            : (anchorPosition - ropeEnd).sqrMagnitude > 1e-8f
                ? (anchorPosition - ropeEnd).normalized
                : Vector3.up;

        float minDistance = SampleMinimumPlaneDistance(
            ropeEnd,
            bucketUp,
            Vector3.zero,
            Vector3.zero,
            planePosition,
            planeNormal,
            attachmentDistance,
            bottomExtentFromCenter,
            topExtentFromCenter,
            bucketRadius);

        if (minDistance >= skin)
            return BucketContactMode.None;

        float bottomParallelDot = Mathf.Abs(Vector3.Dot(bucketUp, planeNormal));
        return bottomParallelDot >= flatAlignmentDotThreshold
            ? BucketContactMode.Flat
            : BucketContactMode.Edge;
    }
}
