using UnityEngine;

public class SimulationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform anchor;

    [SerializeField]
    private Transform bucket;

    [SerializeField]
    private Vector3 bucketAttachmentLocalOffset = Vector3.up * 0.5f;

    [SerializeField]
    private RopeRenderer ropeRenderer;

    [SerializeField]
    private BucketDragController dragController;

    [Header("Rope Settings")]
    [SerializeField]
    private int pointCount = 25;

    [SerializeField]
    private float ropeLength = 1f;

    [SerializeField]
    private float initialAngle = 0f;

    [SerializeField]
    private float initialPhi = 0f;

    [SerializeField]
    private float ropeDamping = 0.999f;

    [SerializeField]
    private float gravity = 9.81f;

    [SerializeField]
    private float bucketMass = 3f;

    [SerializeField]
    private int constraintIterations = 20;
    [Header("SPH Settings")]
    [SerializeField]
    public float sphDt = 0.005f;
    public SPHManager sphManager;
    private RopeSimulation ropeSimulation;
    
    float sphAccumulator = 0f;
    Vector3 lastBucketPos;
    public Vector3 bucketVelocity;

    public float RopeLength => ropeLength;
    public float InitialAngle => initialAngle;
    public float InitialPhi => initialPhi;
    public float RopeDamping => ropeDamping;
    public float Gravity => gravity;
    public float BucketMass => bucketMass;

    private void Start()
    {
        RebuildRope();
    }

    public void SetRopeLength(float value)
    {
        ropeLength = Mathf.Clamp(value, 0.5f, 10f);
        SyncDragLength();
        RebuildRope();
    }

    public void SetInitialAngle(float value)
    {
        initialAngle = Mathf.Clamp(value, 0f, 90f);
        RebuildRope();
    }

    public void SetInitialPhi(float value)
    {
        initialPhi = Mathf.Repeat(value, 360f);
        RebuildRope();
    }

    public void SetRopeDamping(float value)
    {
        ropeDamping = Mathf.Clamp(value, 0.1f, 1f);
        if (ropeSimulation != null)
        {
            ropeSimulation.SetDamping(ropeDamping);
        }
    }

    public void SetGravity(float value)
    {
        gravity = Mathf.Clamp(value, 0f, 30f);
        if (ropeSimulation != null)
        {
            ropeSimulation.SetGravity(gravity);
        }
    }

    public void SetBucketMass(float value)
    {
        bucketMass = Mathf.Max(0.1f, value);
        RebuildRope();
    }

    public void RebuildRope()
    {
        if (anchor == null)
        {
            return;
        }

        ropeSimulation = new RopeSimulation(
            anchor.position,
            pointCount,
            ropeLength,
            constraintIterations,
            initialAngle,
            initialPhi,
            ropeDamping,
            gravity,
            bucketMass);

        lastBucketPos = ropeSimulation.GetBucketPosition();
        SyncDragLength();
    }

    private void SyncDragLength()
    {
        if (dragController != null)
        {
            dragController.SetRopeLength(ropeLength);
        }
    }

    private void FixedUpdate()
    {
        if (ropeSimulation == null || anchor == null || bucket == null)
        {
            return;
        }

        bool isDragging = dragController != null && dragController.IsDragging;
        Vector3 draggedPosition = isDragging ? dragController.DraggedPosition : ropeSimulation.GetBucketPosition();

        ropeSimulation.Simulate(
            Time.fixedDeltaTime,
            anchor.position,
            isDragging,
            draggedPosition);

        Vector3 ropeEnd = ropeSimulation.GetBucketPosition();

        bucketVelocity = (ropeEnd - lastBucketPos) / Time.fixedDeltaTime;
        bucketVelocity =Vector3.ClampMagnitude(bucketVelocity,5f);
        // Debug.Log(bucketVelocity.magnitude);
        lastBucketPos = ropeEnd;
        Vector3 ropeVector =ropeEnd -anchor.position;

        BucketVolume bucketVolume = bucket.GetComponent<BucketVolume>();
        if (bucketVolume != null)
        {
            bucketVolume.SetExternalVelocity(bucketVelocity);
        }
        
        float l =ropeVector.magnitude;
        if (l > 0.001f)
        {
            Vector3 ropeDirection = (anchor.position - ropeEnd).normalized;
            bucket.up = ropeDirection;
        }

        bucket.position = ropeEnd - bucket.TransformVector(bucketAttachmentLocalOffset);
    }

    private void LateUpdate()
    {
        if (ropeRenderer != null && ropeSimulation != null)
        {
            ropeRenderer.Render(ropeSimulation.Points);
        }
    }
}
