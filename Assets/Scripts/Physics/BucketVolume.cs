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
    [Range(0f, 1f)]
    public float nozzleTangentialDamping = 0.08f;
    [Range(0f, 1f)]
    public float bucketVelocityInheritance = 0.15f;

    [Header("Slosh")]
    public float inertiaStrength = 0f;

    Vector3 lastPosition;
    Vector3 bucketVelocity;

    public Vector3 Velocity => bucketVelocity;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        // bucketVelocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
    }

    public bool Constrain(ref Vector3 pos, ref Vector3 vel)
    {
        Vector3 localPos = transform.InverseTransformPoint(pos);
        Vector3 localVel = transform.InverseTransformDirection(vel);
        localPos.y -= collisionYOffset;
        float halfH = height * 0.2f;

        // inertia
        Vector3 inertialForce = -bucketVelocity * inertiaStrength;
        localVel += transform.InverseTransformDirection(inertialForce) * Time.deltaTime;

        Vector2 xz = new Vector2(localPos.x, localPos.z);
        float dist = xz.magnitude;
        // float margin = 0.3f;

        // if(localPos.y < -halfH - margin ||localPos.y >  halfH + margin)
        // {
        //     return;
        // }
        // Capture only a THIN layer right at the bucket floor. A large capture
        // depth grabs a big slug of overlapping particles in one frame, which
        // then bursts apart. A thin layer meters the flow into a steady stream.
        if(dist <= nozzleRadius && localPos.y < -halfH + nozzleCaptureDepth)
        {
            // Place the particle just below the nozzle opening (done in local space).
            localPos.y = -halfH - 0.002f;
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
        else if (localPos.y < -halfH)
        {
            localPos.y = -halfH;
            localVel.y = Mathf.Abs(localVel.y) * bounce;
        }
        else if (localPos.y > halfH)
        {
            localPos.y = halfH;
            localVel.y = -Mathf.Abs(localVel.y) * bounce;
        }

        // cylinder wall
        // Vector2 xz = new Vector2(localPos.x, localPos.z);
        // float dist = xz.magnitude;

        if (dist > radius)
        {

            Vector2 normal = xz.normalized;
            xz = normal * radius;

            localPos.x = xz.x;
            localPos.z = xz.y;

            Vector2 velXZ = new Vector2(localVel.x, localVel.z);
            float dot = Vector2.Dot(velXZ, normal);
            velXZ -= 2f * dot * normal;

            localVel.x = velXZ.x * bounce;
            localVel.z = velXZ.y * bounce;

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

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireCube(
            new Vector3(0, height * 0.8f, 0),
            new Vector3(radius * 2f, height, radius * 2f)
        );
    }
}
