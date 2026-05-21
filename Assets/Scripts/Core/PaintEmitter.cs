using System.Collections.Generic;
using UnityEngine;

public class PaintEmitter : MonoBehaviour
{
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

    private float accumulator;
    public GameObject paintPrefab;
    private Vector3 previousBucketPos;

    [Header("Paint Amount")]
    public float totalPaint = 5f; // liters
    public float consumptionRate = 0.1f;
    void Start()
    {
        previousBucketPos = bucket.position;
    }
    void Update()
    {
        Emit(Time.deltaTime);
        UpdateParticles(Time.deltaTime);
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
        float Q = ComputeFlow()*totalPaint;
        // float Q = 8f *totalPaint;
        accumulator += Q * dt;
        Debug.Log(accumulator);
        while (accumulator > particleStep)
        {
            Debug.Log("Emitting paint");
            accumulator -= particleStep;

            Vector3 bucketVelocity =
            (bucket.position - previousBucketPos) / dt;

            previousBucketPos = bucket.position;
            // Vector3 direction =
            //     nozzle.forward;
            Vector3 direction = Vector3.down;
            Vector3 velocity =
                direction * 2f +
                bucketVelocity * 0.3f +
                Random.insideUnitSphere * 0.1f;

            particles.Add(
                new PaintParticle(
                    nozzle.position,
                    velocity,
                    paintPrefab));
            totalPaint -= consumptionRate * dt;
            totalPaint = Mathf.Max(totalPaint, 0f);        
        }
    }
    void UpdateParticles(float dt)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            if (!particles[i].Update(dt))
            {
                particles.RemoveAt(i);
            }
        }
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;

        foreach (var p in particles)
        {
            Gizmos.DrawSphere(p.Position, 0.02f);
        }
    }
}