using UnityEngine;

public class RopePoint
{
    public Vector3 Position;
    public Vector3 PreviousPosition;
    public float InverseMass;
    public RopePoint(Vector3 position , float mass =1f)
    {
        Position = position;
        PreviousPosition = position;
        InverseMass=1f/mass;
    }
}