using UnityEngine;

public class SPHParticle
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 force;
    public float mass = 1f;
    public float density;
    public float pressure;
    public Vector3 predictedPosition;
    public float nearDensity;
    public float nearPressure;
    public Color color;
    public bool IsinsidetheBucket=true;
    public bool OnPlane = false;
}