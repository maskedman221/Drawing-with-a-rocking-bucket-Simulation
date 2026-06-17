using UnityEngine;

public class SimulationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform anchor;

    [SerializeField]
    private Transform bucket;

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
    }

    private void FixedUpdate()
    {
        ropeSimulation.Simulate(
            Time.fixedDeltaTime,
            anchor.position,
            dragController.IsDragging,
            dragController.DraggedPosition);

        Vector3 ropeEnd = ropeSimulation.GetBucketPosition();

        bucketVelocity = (ropeEnd - lastBucketPos) / Time.fixedDeltaTime;

        lastBucketPos = ropeEnd;
        Vector3 bucketOffset = Vector3.down * 0.5f;

        bucket.position = ropeEnd + bucketOffset;
        Vector3 ropeVector =ropeEnd -anchor.position;

        bucket.GetComponent<BucketVolume>().SetExternalVelocity(bucketVelocity);
        
        float l =ropeVector.magnitude;
    if (l > 0.001f)
    {
        float theta =Mathf.Acos(-ropeVector.y / l);

        float phi =Mathf.Atan2(ropeVector.z,ropeVector.x);

        Vector3 ropeDirection =(anchor.position - bucket.position).normalized;

        bucket.up =ropeDirection;
        
    }
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