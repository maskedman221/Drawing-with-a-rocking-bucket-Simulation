using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SPHCubeTestDraw : MonoBehaviour
{
    public Material containerLineMaterial;
    public float containerLineWidth = 0.02f;
    public SPHManager sph;
    LineRenderer containerLines;
    
    void LateUpdate()
    {
        if (!sph.showContainer)
        {
            if (containerLines != null)
            containerLines.enabled = false;

            return;
        }
        EnsureContainerLineRenderer();
        UpdateContainerLineRenderer();
    }
    public void EnsureContainerLineRenderer()
    {
        if (containerLines != null)
            return;

        GameObject lineObject = new GameObject("Container Lines");
        lineObject.transform.SetParent(transform, false);

        containerLines = lineObject.AddComponent<LineRenderer>();
        containerLines.useWorldSpace = true;
        containerLines.loop = false;
        containerLines.positionCount = 24;
        containerLines.widthMultiplier = containerLineWidth;

        if (containerLineMaterial != null)
            containerLines.material = containerLineMaterial;
    }

    public void UpdateContainerLineRenderer()
    {
        containerLines.enabled = true;
        containerLines.widthMultiplier = containerLineWidth;

        Quaternion rotation = Quaternion.Euler(sph.boxRotation);
        Vector3 half = sph.boxSize * 0.5f;

        Vector3[] c =
        {
            new Vector3(-half.x, -half.y, -half.z),
            new Vector3( half.x, -half.y, -half.z),
            new Vector3( half.x, -half.y,  half.z),
            new Vector3(-half.x, -half.y,  half.z),
            new Vector3(-half.x,  half.y, -half.z),
            new Vector3( half.x,  half.y, -half.z),
            new Vector3( half.x,  half.y,  half.z),
            new Vector3(-half.x,  half.y,  half.z)
        };

        for (int i = 0; i < c.Length; i++)
            c[i] = sph.boxCenter + rotation * c[i];

        Vector3[] lines =
        {
            c[0], c[1], c[1], c[2], c[2], c[3], c[3], c[0],
            c[4], c[5], c[5], c[6], c[6], c[7], c[7], c[4],
            c[0], c[4], c[1], c[5], c[2], c[6], c[3], c[7]
        };

        containerLines.positionCount = lines.Length;
        containerLines.SetPositions(lines);
    }
}