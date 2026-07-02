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
        if(dist <= nozzleRadius && localPos.y < -halfH + 0.05f)
        {
            // Debug.Log("emitting  "+dist + "  " + nozzleRadius);
            localVel.x *= nozzleTangentialDamping;
            localVel.z *= nozzleTangentialDamping;
            localVel.y = -Mathf.Max(Mathf.Abs(localVel.y), nozzleExitSpeed);

            Vector3 inheritedBucketVelocity =
                transform.InverseTransformDirection(bucketVelocity) *
                bucketVelocityInheritance;
            localVel += inheritedBucketVelocity;

            localPos.y = -halfH - 0.002f;
            localPos.y += collisionYOffset;
            pos = transform.TransformPoint(localPos);
            vel = transform.TransformDirection(localVel);
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
