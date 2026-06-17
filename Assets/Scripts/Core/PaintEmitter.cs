using System.Collections.Generic;
using UnityEngine;

public class PaintEmitter : MonoBehaviour
{
    private VerletIntegrator verletIntegrator =new VerletIntegrator();
    [Header("References")]
    public Transform bucket;
    public Transform nozzle;

    [Header("Flow Settings")]
    public float viscosity = 2f;
    public float radius = 0.01f;
    public float nozzleLength = 0.2f;
    public float paintHeight = 1f;

    [Header("Emission")]
    public float particleStep = 0.01f;
    
    private List<PaintParticle> particles =
        new List<PaintParticle>();
    private List<PaintLink> links = new List<PaintLink>();
    private float accumulator;
    // public GameObject paintPrefab;
    private Vector3 previousBucketPos;
    public BucketVolume bucketBoundary;

    [Header("Paint Amount")]
    public float totalPaint = 5f; // liters
    public float consumptionRate = 0.1f;
    void Start()
    {
        float bucketArea = 4f;
        previousBucketPos = bucket.position;
        
    }
    void Update()
    {
        Emit(Time.deltaTime);
        UpdateParticles(Time.deltaTime);
        SolveConstraints();
    }
    float ComputeFlow()
    {
        float r4 = Mathf.Pow(radius, 4);

        float Q =
            Mathf.PI *
            r4 *
            9.81f *
            paintHeight
            / (8f * viscosity * nozzleLength);

        return Q;
    }
    void Emit(float dt)
    {
        if (totalPaint <= 0f)
        return;
        paintHeight = totalPaint / 4;
        float Q = ComputeFlow();
        // float Q = 8f *totalPaint;
        accumulator += Q * dt;
        // Debug.Log(accumulator);
        Vector3 bucketVelocity =(bucket.position - previousBucketPos) / dt;
        while (accumulator >= particleStep && totalPaint >= particleStep)
        {
            Debug.Log("Emitting paint");
            accumulator -= particleStep;
            // Vector3 direction =
            //     nozzle.forward;
            // Vector3 direction = Vector3.down;
            Vector3 velocity =bucketVelocity * 0.3f;

            particles.Add(
                new PaintParticle(
                    nozzle.position,
                    velocity,
                    particleStep,
                    Color.blue
                ));
            totalPaint -= particleStep;
            totalPaint = Mathf.Max(totalPaint, 0f); 

            int newIndex = particles.Count - 1;
            if(newIndex > 0)
            {
                links.Add(
                    new PaintLink
                    {
                        A = newIndex - 1,
                        B = newIndex,
                        RestLength = 0.02f
                    });
            }      
        }
        previousBucketPos = bucket.position;
    }
    void UpdateParticles(float dt)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];

            if (!verletIntegrator.PaintIntegrate(p, dt, viscosity))
            {
                RemoveParticle(i);
                continue;
            }

            // APPLY BUCKET CONSTRAINT
            bucketBoundary.Constrain(ref p.Position, ref p.Velocity);

            particles[i] = p;
        }
    }
    void SolveConstraints()
    {
        for (int iteration = 0;iteration < 20;iteration++)
        {
            if (particles.Count > 0)
            {
                particles[0].Position =nozzle.position;
                particles[0].PreviousPosition =nozzle.position;
            }

            float restLength = 0.02f;

            for (int i = links.Count - 1;i >= 0 ;i--)
            {
                PaintLink link = links[i];
                PaintParticle a =particles[link.A];
                PaintParticle b =particles[link.B];

                Vector3 delta =b.Position - a.Position;

                float dist =delta.magnitude;

                if (dist < 0.0001f)
                    continue;
                // if (dist > link.RestLength * 4f)
                // {
                //     links.RemoveAt(i);
                //     continue;
                // }
                float error =dist - restLength;

                Vector3 correction =delta.normalized *error * 0.5f;

                a.Position += correction;
                b.Position -= correction;
                // Debug.DrawLine(
                // a.Position,
                // b.Position,
                // Color.red);
            }
            
        }
    }
    void RemoveParticle(int index)
    {
        particles.RemoveAt(index);

        // remove all links that reference this particle
        for (int i = links.Count - 1; i >= 0; i--)
        {
            if (links[i].A == index || links[i].B == index)
            {
                links.RemoveAt(i);
            }
        }

        // FIX indices after removal
        for (int i = 0; i < links.Count; i++)
        {
            if (links[i].A > index) links[i].A--;
            if (links[i].B > index) links[i].B--;
        }
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;

        foreach (var p in particles)
        {
            Gizmos.DrawSphere(p.Position, p.Radius);
        }
    }
}

public class PaintLink
{
    public int A;
    public int B;

    public float RestLength;
}