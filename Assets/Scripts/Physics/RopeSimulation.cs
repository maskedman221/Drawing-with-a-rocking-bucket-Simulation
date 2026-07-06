using UnityEngine;

public class RopeSimulation
{
    public RopePoint[] Points;

    private float segmentLength;
    private int iterations;
    private float bucketMass;

    private VerletIntegrator integrator;
    private float stiffness = 0.8f;
    private float maxStretchMultiplier = 1.5f;
    private float previousTheta;
    private float previousPhi;
    public RopeSimulation(
        Vector3 anchor,
        int pointCount,
        float ropeLength,
        int iterations,
        float initialAngleDegrees = 0f,
        float initialPhiDegrees = 0f,
        float damping = 0.999f,
        float gravity = 9.81f,
        float bucketMass = 3f)
    {
        this.iterations = iterations;
        this.bucketMass = Mathf.Max(0.1f, bucketMass);

        segmentLength = ropeLength / (pointCount - 1);

        Points = new RopePoint[pointCount];

        float theta = initialAngleDegrees * Mathf.Deg2Rad;
        float phi = initialPhiDegrees * Mathf.Deg2Rad;
        Vector3 ropeDirection = new Vector3(
            Mathf.Sin(theta) * Mathf.Cos(phi),
            -Mathf.Cos(theta),
            Mathf.Sin(theta) * Mathf.Sin(phi));

        if (ropeDirection.sqrMagnitude < 0.0001f)
        {
            ropeDirection = Vector3.down;
        }

        ropeDirection.Normalize();

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 pos = anchor + ropeDirection * segmentLength * i;
            float mass = 1f;

            // Bucket point heavier
            if (i == pointCount - 1)
            {
                mass = this.bucketMass;
            }

            Points[i] =new RopePoint(pos, mass);
        }

        integrator = new VerletIntegrator();
        integrator.SetDamping(damping);
        integrator.SetGravity(gravity);
    }

    public void SetGravity(float gravity)
    {
        integrator.SetGravity(gravity);
    }

    public void SetDamping(float damping)
    {
        integrator.SetDamping(damping);
    }

    public void SetBucketMass(float mass)
    {
        bucketMass = Mathf.Max(0.1f, mass);
    }

    public void Simulate(
        float dt,
        Vector3 anchorPosition,
        bool isDragging,
        Vector3 draggedPosition)
    {
        // 1. Integrate motion (skip last point if dragging)
        for (int i = 1; i < Points.Length; i++)
        {
            if (isDragging && i == Points.Length - 1)
                continue;

            integrator.Integrate(Points[i], dt);
        }
        ApplyPendulumBehavior(dt, anchorPosition);
        // 2. Solve constraints multiple times (stability)
        for (int i = 0; i < iterations; i++)
        {
            SolveConstraints(anchorPosition, isDragging, draggedPosition);
        }
    }

    private void SolveConstraints(
        Vector3 anchorPosition,
        bool isDragging,
        Vector3 draggedPosition)
    {
        // Anchor is always fixed
        Points[0].Position = anchorPosition;

        // If dragging, force bucket position
        if (isDragging)
        {
            RopePoint bucketPoint = Points[Points.Length - 1];

            bucketPoint.PreviousPosition = bucketPoint.Position;

            bucketPoint.Position = draggedPosition;
        }

        // Enforce rope segment lengths
        for (int i = 0; i < Points.Length - 1; i++)
        {
            RopePoint a = Points[i];
            RopePoint b = Points[i + 1];

            Vector3 delta = b.Position - a.Position;

            float distance = delta.magnitude;
            float stretch = distance - segmentLength;

            // Hooke elastic correction
            Vector3 correction =delta.normalized *stretch *stiffness ;

            float maxLength =segmentLength * maxStretchMultiplier;

            if (distance > maxLength)
            {
                float excess = distance - maxLength;

                correction +=delta.normalized * excess;
            }

            if (i == 0)
            {
                // Anchor side fixed
                b.Position -= correction;
            }
            else if (i == Points.Length - 2 && isDragging)
            {
                // Bucket side fixed when dragging
                a.Position += correction;
            }
            else
            {
                float totalInverseMass =
                    a.InverseMass +
                    b.InverseMass;

                if (totalInverseMass <= 0f)
                    return;

                float aWeight =
                    a.InverseMass /
                    totalInverseMass;

                float bWeight =
                    b.InverseMass /
                    totalInverseMass;

                a.Position += correction * aWeight;
                b.Position -= correction * bWeight;
            }
        }
    }

    public Vector3 GetBucketPosition()
    {
        return Points[Points.Length - 1].Position;
    }

        private void ApplyPendulumBehavior(float dt,Vector3 anchorPosition)
        {
        RopePoint bucketPoint = Points[Points.Length - 1];
        Vector3 ropeVector = bucketPoint.Position - anchorPosition;

        float l = ropeVector.magnitude;

        // Prevent division problems
        if (l < 0.001f) return;

        // Compute angles
        float theta =Mathf.Acos(-ropeVector.y / l);

        float phi = Mathf.Atan2(ropeVector.z,ropeVector.x);

        // Angular velocities
        float thetaVelocity = (theta - previousTheta) / dt;

        float phiVelocity = (phi - previousPhi) / dt;

        previousTheta = theta;
        previousPhi = phi;

        // Pendulum equations
        float thetaAcceleration =
        Mathf.Sin(theta)
        *Mathf.Cos(theta)
        *phiVelocity
        *phiVelocity
            -
            (9.81f / l) *
            Mathf.Sin(theta);


        }
}
