using UnityEngine;

public class PaintParticle
{
    public Vector3 Position;
    public Vector3 Velocity;

    public float Life;
    public float MaxLife;
    public GameObject Visual;

    public PaintParticle(Vector3 pos, Vector3 vel, GameObject prefab)
    {
        Position = pos;
        Velocity = vel;

        MaxLife = 2f;
        Life = MaxLife;
        Visual =GameObject.Instantiate(prefab, pos, Quaternion.identity);
    }

    public bool Update(float dt)
    {
        Velocity += Vector3.down * 9.81f * dt;
        Position += Velocity * dt;

        Life -= dt;

        Visual.transform.position = Position;

        if (Life <= 0f)
        {
            GameObject.Destroy(Visual);
            return false;
        }

        return true;
    }
}