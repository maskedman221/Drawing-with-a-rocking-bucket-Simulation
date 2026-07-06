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
    private int constraintIterations = 20;
    [Header("SPH Settings")]
    [SerializeField]
    public float sphDt = 0.005f;
    public SPHManager sphManager;
    private RopeSimulation ropeSimulation;
    
    float sphAccumulator = 0f;
    Vector3 lastBucketPos;
    public Vector3 bucketVelocity;
    private void Start()
    {
        ropeSimulation = new RopeSimulation(
            anchor.position,
            pointCount,
            ropeLength,
            constraintIterations);
        if (dragController != null)
        {
            dragController.SetRopeLength(ropeLength);
        }
        lastBucketPos =ropeSimulation.GetBucketPosition();
        if (dragController != null)
        {
            dragController.SetCurrentRopeEnd(lastBucketPos);
        }
    }

    private void FixedUpdate()
    {
        if (dragController != null)
        {
            dragController.SetRopeLength(ropeLength);
        }

        ropeSimulation.Simulate(
            Time.fixedDeltaTime,
            anchor.position,
            dragController != null && dragController.IsDragging,
            dragController != null ? dragController.DraggedPosition : Vector3.zero);

        Vector3 ropeEnd = ropeSimulation.GetBucketPosition();
        if (dragController != null)
        {
            dragController.SetCurrentRopeEnd(ropeEnd);
        }

        bucketVelocity = (ropeEnd - lastBucketPos) / Time.fixedDeltaTime;
        bucketVelocity =Vector3.ClampMagnitude(bucketVelocity,5f);
        // Debug.Log(bucketVelocity.magnitude);
        lastBucketPos = ropeEnd;
        Vector3 ropeVector =ropeEnd -anchor.position;

        bucket.GetComponent<BucketVolume>().SetExternalVelocity(bucketVelocity);
        
        float l =ropeVector.magnitude;
        if (l > 0.001f)
        {
            Vector3 ropeDirection = (anchor.position - ropeEnd).normalized;
            bucket.up = ropeDirection;
        }

        bucket.position = ropeEnd - bucket.TransformVector(bucketAttachmentLocalOffset);
    // sphManager.Simulate(Time.fixedDeltaTime);
    // sphAccumulator += Time.fixedDeltaTime;
    // while (sphAccumulator >= sphDt)
    // {
    //     sphManager.Simulate(sphDt);
    //     sphAccumulator -= sphDt;
    // }
}

    private void LateUpdate()
    {
        ropeRenderer.Render(ropeSimulation.Points);
    }
}
