using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeTwistArrowRenderer : MonoBehaviour
{
    [Header("References")]
    public RopeSimulationGPU ropeSystem;
    public Transform bucket;
    public Transform anchor;

    [Header("Layout")]
    [Range(0.5f, 0.95f)]
    public float ropeT = 0.8f;
    public float arrowLength = 0.18f;
    public float headLength = 0.06f;
    public float headAngle = 22f;
    public float lineOffset = 0.04f;

    LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 5;
        lineRenderer.loop = false;
    }

    public void RenderArrow()
    {
        if (lineRenderer == null || ropeSystem == null || !ropeSystem.IsInitialized)
            return;

        Vector3[] positions = ropeSystem.Positions;
        if (positions == null || positions.Length < 2 || bucket == null)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        Vector3 anchorPos = anchor != null ? anchor.position : positions[0];
        Vector3 ropeEnd = positions[positions.Length - 1];
        Vector3 basePoint = Vector3.Lerp(anchorPos, ropeEnd, ropeT);

        Vector3 pourDir = bucket.forward;
        if (pourDir.sqrMagnitude < 1e-6f)
            pourDir = -bucket.up;

        Vector3 ropeDir = (ropeEnd - anchorPos).normalized;
        Vector3 side = Vector3.Cross(ropeDir, pourDir);
        if (side.sqrMagnitude < 1e-6f)
            side = Vector3.Cross(ropeDir, Vector3.up);
        side.Normalize();

        Vector3 origin = basePoint + side * lineOffset;
        Vector3 tip = origin + pourDir.normalized * arrowLength;
        Vector3 headA = tip - Quaternion.AngleAxis(headAngle, side) * pourDir.normalized * headLength;
        Vector3 headB = tip - Quaternion.AngleAxis(-headAngle, side) * pourDir.normalized * headLength;

        lineRenderer.positionCount = 5;
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, tip);
        lineRenderer.SetPosition(2, headA);
        lineRenderer.SetPosition(3, tip);
        lineRenderer.SetPosition(4, headB);
    }
}
