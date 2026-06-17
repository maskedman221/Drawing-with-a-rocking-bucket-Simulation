using UnityEngine;

public class PaintParticle
{
    public Vector3 Position;
    public Vector3 PreviousPosition;
    public Vector3 Velocity;
    // public float Viscosity = 0.5f;
    public float Volume;
    public float Radius;
    public Color Color;
    public float Life;
    public float MaxLife;
    // public GameObject Visual;

   public PaintParticle(Vector3 pos, Vector3 vel ,float volume,Color color )
    {
        Position = pos;
        PreviousPosition = pos;
        Velocity = vel;
        Volume = volume;
        Color = color;
        Radius = getRadius();
        MaxLife = 2f;
        Life = MaxLife;
        // Visual =GameObject.Instantiate(prefab, pos, Quaternion.identity);
    }
    public float getRadius()
    {
            return Mathf.Pow(
                (3f * Volume) /
                (4f * Mathf.PI),
                1f / 3f);
    }
    // public bool Update(float dt)
    // {
    //     Velocity += Vector3.down * 9.81f * dt;
    //     Position += Velocity * dt;

    //     Life -= dt;

    //     Visual.transform.position = Position;

    //     if (Life <= 0f)
    //     {
    //         GameObject.Destroy(Visual);
    //         return false;
    //     }

    //     return true;
    // }
}