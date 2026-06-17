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

    [Header("Slosh")]
    public float inertiaStrength = 8f;

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

    public void Constrain(ref Vector3 pos, ref Vector3 vel)
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

        if(dist <= nozzleRadius && localPos.y < -halfH + 0.05f)
        { 
            Debug.Log("Emit Leaked Piant");
            localVel += Vector3.down * 0.5f;
            
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
        vel = transform.TransformDirection(localVel) + bucketVelocity;
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