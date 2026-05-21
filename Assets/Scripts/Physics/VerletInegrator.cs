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
}