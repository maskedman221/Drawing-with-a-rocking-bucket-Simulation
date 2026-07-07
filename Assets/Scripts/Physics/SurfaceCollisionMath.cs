using UnityEngine;

/// <summary>
/// CPU mirror of Assets/Shaders/SurfaceCollision.hlsl for fallback / debugging.
/// </summary>
public static class SurfaceCollisionMath
{
    public struct Result
    {
        public Vector3 position;
        public Vector3 velocity;
        public bool hadContact;
        public bool shouldFreeze;
        public bool hadImpact;
    }

    public static Result Resolve(
        Vector3 position,
        Vector3 velocity,
        Vector3 planePosition,
        Vector3 planeNormal,
        float restitution,
        float staticFriction,
        float dynamicFriction,
        float spread,
        float absorption,
        float surfaceWetness,
        float wetnessSlideFactor,
        float paintViscosity,
        float stopSpeedThreshold,
        float deltaTime,
        Vector3 gravityWorld,
        uint particleIndex,
        bool slidePhase)
    {
        Result result = new Result
        {
            position = position,
            velocity = velocity
        };

        if (planeNormal.sqrMagnitude < 1e-8f)
            planeNormal = Vector3.up;
        else
            planeNormal.Normalize();

        float signedDistance = Vector3.Dot(position - planePosition, planeNormal);
        if (signedDistance > 0f)
        {
            if (!slidePhase || signedDistance > 0.08f)
                return result;
        }

        result.hadContact = true;
        result.position = position - planeNormal * signedDistance;

        Vector3 normalVelocity = Vector3.Project(velocity, planeNormal);
        Vector3 tangentialVelocity = velocity - normalVelocity;
        float tangentialSpeed = tangentialVelocity.magnitude;
        float impactSpeed = velocity.magnitude;

        if (!slidePhase)
        {
            float e = Mathf.Clamp01(restitution);
            if (Vector3.Dot(normalVelocity, planeNormal) < 0f)
            {
                normalVelocity = -e * normalVelocity;
                result.hadImpact = true;
                tangentialVelocity *= (1f - Mathf.Clamp01(absorption));
            }

            if (spread > 0.001f && impactSpeed > 0.01f)
            {
                Vector3 randomTangent = RandomTangent(planeNormal, particleIndex);
                tangentialVelocity += randomTangent * (spread * impactSpeed);
            }
        }
        else
        {
            normalVelocity = Vector3.zero;
        }

        float mu = EffectiveFriction(
            staticFriction,
            dynamicFriction,
            tangentialSpeed,
            surfaceWetness,
            wetnessSlideFactor);

        Vector3 gravityParallel = gravityWorld - Vector3.Project(gravityWorld, planeNormal);
        float gMag = gravityWorld.magnitude;
        float sinSlope = gMag > 1e-6f ? gravityParallel.magnitude / gMag : 0f;
        float cosSlope = Mathf.Abs(Vector3.Dot(planeNormal, Vector3.up));

        if (slidePhase && sinSlope > 0.0001f && sinSlope > mu * cosSlope)
        {
            Vector3 downSlope = gravityParallel.normalized;
            float accel = gMag * (sinSlope - mu * cosSlope);
            tangentialVelocity += downSlope * (accel * deltaTime);
        }

        float frictionScale = (!slidePhase && result.hadImpact) ? 0.4f : 1f;
        tangentialVelocity *= (1f - mu * frictionScale);

        if (slidePhase || !result.hadImpact)
            tangentialVelocity *= Mathf.Exp(-Mathf.Max(0f, paintViscosity) * deltaTime);

        Vector3 finalVelocity = normalVelocity + tangentialVelocity;
        float normalSpeed = Mathf.Abs(Vector3.Dot(finalVelocity, planeNormal));
        tangentialSpeed = Vector3.ProjectOnPlane(finalVelocity, planeNormal).magnitude;
        float outboundNormalSpeed = Vector3.Dot(finalVelocity, planeNormal);

        if (outboundNormalSpeed > stopSpeedThreshold)
        {
            result.velocity = finalVelocity;
            result.shouldFreeze = false;
        }
        else if (normalSpeed < stopSpeedThreshold && tangentialSpeed < stopSpeedThreshold)
        {
            result.velocity = Vector3.zero;
            result.shouldFreeze = true;
        }
        else
        {
            result.velocity = finalVelocity;
        }

        return result;
    }

    static float EffectiveFriction(
        float staticFriction,
        float dynamicFriction,
        float tangentialSpeed,
        float surfaceWetness,
        float wetnessSlideFactor)
    {
        float speedBlend = Mathf.SmoothStep(0f, 0.12f, tangentialSpeed);
        float mu = Mathf.Lerp(staticFriction, dynamicFriction, speedBlend);
        mu *= (1f - Mathf.Clamp01(wetnessSlideFactor) * Mathf.Clamp01(surfaceWetness));
        return Mathf.Clamp01(mu);
    }

    static Vector3 RandomTangent(Vector3 planeNormal, uint seed)
    {
        Vector3 refAxis = Mathf.Abs(planeNormal.y) < 0.99f ? Vector3.up : Vector3.right;
        Vector3 tangentA = Vector3.Cross(refAxis, planeNormal).normalized;
        Vector3 tangentB = Vector3.Cross(planeNormal, tangentA);
        float angle = Hash01(seed * 17u + 3u) * Mathf.PI * 2f;
        return tangentA * Mathf.Cos(angle) + tangentB * Mathf.Sin(angle);
    }

    static float Hash01(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7feb352dU;
            value ^= value >> 15;
            value *= 0x846ca68bU;
            value ^= value >> 16;
            return (value & 0x00ffffff) / 16777215f;
        }
    }
}
