#ifndef SURFACE_COLLISION_HLSL
#define SURFACE_COLLISION_HLSL

float SurfaceHash01(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    value ^= value >> 16;
    return (value & 0x00ffffffu) / 16777215.0f;
}

float3 SurfaceSafeNormalize(float3 v, float3 fallback)
{
    float lenSq = dot(v, v);
    return lenSq > 1e-8 ? v * rsqrt(lenSq) : fallback;
}

float SurfaceEffectiveRestitution(float restitution, float absorption)
{
    return restitution * (1.0 - saturate(absorption));
}

float SurfaceEffectiveFriction(
    float staticFriction,
    float dynamicFriction,
    float tangentialSpeed,
    float surfaceWetness,
    float wetnessSlideFactor)
{
    float speedBlend = smoothstep(0.0, 0.12, tangentialSpeed);
    float mu = lerp(staticFriction, dynamicFriction, speedBlend);
    mu *= (1.0 - saturate(wetnessSlideFactor) * saturate(surfaceWetness));
    return saturate(mu);
}

float3 SurfaceRandomTangent(float3 planeNormal, uint seed)
{
    float3 refAxis = abs(planeNormal.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 tangentA = normalize(cross(refAxis, planeNormal));
    float3 tangentB = cross(planeNormal, tangentA);
    float angle = SurfaceHash01(seed * 17u + 3u) * 6.28318530718f;
    return tangentA * cos(angle) + tangentB * sin(angle);
}

struct SurfaceCollisionResult
{
    float3 position;
    float3 velocity;
    bool hadContact;
    bool shouldFreeze;
    bool hadImpact;
};

SurfaceCollisionResult ResolveSurfacePlaneCollision(
    float3 position,
    float3 velocity,
    float3 planePosition,
    float3 planeNormal,
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
    float3 gravityWorld,
    uint particleIndex,
    bool slidePhase)
{
    SurfaceCollisionResult result;
    result.position = position;
    result.velocity = velocity;
    result.hadContact = false;
    result.shouldFreeze = false;
    result.hadImpact = false;

    planeNormal = SurfaceSafeNormalize(planeNormal, float3(0, 1, 0));
    float signedDistance = dot(position - planePosition, planeNormal);

    if (signedDistance > 0.0)
    {
        // Slide-phase capture band keeps paint on a rotating/moving plane.
        if (!slidePhase || signedDistance > 0.08)
            return result;
    }

    result.hadContact = true;
    result.position = position - planeNormal * signedDistance;

    float3 normalVelocity = dot(velocity, planeNormal) * planeNormal;
    float3 tangentialVelocity = velocity - normalVelocity;
    float tangentialSpeed = length(tangentialVelocity);
    float impactSpeed = length(velocity);

    if (!slidePhase)
    {
        float e = saturate(restitution);
        if (dot(normalVelocity, planeNormal) < 0.0)
        {
            normalVelocity *= -e;
            result.hadImpact = true;
            // Absorption removes tangential splash energy — not the normal bounce.
            tangentialVelocity *= (1.0 - saturate(absorption));
        }

        if (spread > 0.001 && impactSpeed > 0.01)
        {
            float3 randomTangent = SurfaceRandomTangent(planeNormal, particleIndex);
            tangentialVelocity += randomTangent * spread * impactSpeed;
        }
    }
    else
    {
        normalVelocity = 0.0;
    }

    float mu = SurfaceEffectiveFriction(
        staticFriction,
        dynamicFriction,
        tangentialSpeed,
        surfaceWetness,
        wetnessSlideFactor);

    // Coulomb slope: slide only when tan(angle) > mu.  a = g(sinθ - μ cosθ) downhill.
    float gMag = length(gravityWorld);
    float3 gravityParallel = gravityWorld - dot(gravityWorld, planeNormal) * planeNormal;
    float sinSlope = length(gravityParallel) / max(gMag, 0.0001);
    float cosSlope = abs(dot(planeNormal, float3(0.0, 1.0, 0.0)));

    if (slidePhase && sinSlope > 0.0001 && sinSlope > mu * cosSlope)
    {
        float3 downSlope = gravityParallel / max(length(gravityParallel), 0.0001);
        float accel = gMag * (sinSlope - mu * cosSlope);
        tangentialVelocity += downSlope * accel * deltaTime;
    }

    float frictionScale = (!slidePhase && result.hadImpact) ? 0.4 : 1.0;
    tangentialVelocity *= (1.0 - mu * frictionScale);

    if (slidePhase || !result.hadImpact)
        tangentialVelocity *= exp(-max(0.0, paintViscosity) * deltaTime);

    float3 finalVelocity = normalVelocity + tangentialVelocity;
    float normalSpeed = abs(dot(finalVelocity, planeNormal));
    tangentialSpeed = length(finalVelocity - dot(finalVelocity, planeNormal) * planeNormal);
    float outboundNormalSpeed = dot(finalVelocity, planeNormal);

    // Keep bouncing particles airborne — only freeze when settled on the surface.
    if (outboundNormalSpeed > stopSpeedThreshold)
    {
        result.velocity = finalVelocity;
        result.shouldFreeze = false;
    }
    else if (normalSpeed < stopSpeedThreshold && tangentialSpeed < stopSpeedThreshold)
    {
        result.velocity = 0.0;
        result.shouldFreeze = true;
    }
    else
    {
        result.velocity = finalVelocity;
    }

    return result;
}

#endif
