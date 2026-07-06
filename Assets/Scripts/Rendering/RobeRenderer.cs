using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeRenderer : MonoBehaviour
{
    private LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    public void Render(RopePoint[] points)
    {
        lineRenderer.positionCount = points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            lineRenderer.SetPosition(i, points[i].Position);
        }
    }

    public void Render(Vector3[] positions)
    {
        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);
    }
}
