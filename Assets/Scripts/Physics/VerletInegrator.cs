using UnityEngine;

public class VerletIntegrator
{
    private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    private float damping = 0.999f;

    public void Integrate(RopePoint point, float dt)
    {
        Vector3 velocity = (point.Position - point.PreviousPosition) * damping;

        Vector3 nextPosition = point.Position +velocity +(gravity / point.InverseMass) * dt * dt;

        point.PreviousPosition = point.Position;
        point.Position = nextPosition;
    }

    public bool PaintIntegrate(PaintParticle p, float dt, float viscosity)
    {
        Vector3 current = p.Position;

        // Verlet velocity approximation
        Vector3 velocity = (p.Position - p.PreviousPosition);

        // Apply viscosity as displacement damping (NOT velocity damping)
        float damping = 1f - Mathf.Clamp01(viscosity * dt);
        velocity *= damping;

        // Store current position for next frame
        p.PreviousPosition = current;

        // Integrate motion
        p.Position += velocity;
        p.Position += gravity * dt * dt;

        // Lifetime
        p.Life -= dt;

        return p.Life > 0f;
    }
}