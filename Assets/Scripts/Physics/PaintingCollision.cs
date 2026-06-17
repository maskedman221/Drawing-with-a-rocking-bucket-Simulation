using UnityEngine;

public class PaintingCollision : MonoBehaviour
{
    [SerializeField]
    public Transform plane;
    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public bool Constrain(ref Vector3 pos, ref Vector3 vel)
    {
        if(pos.y <= plane.position.y)
        {
            pos.y = plane.position.y+0.1f;
            vel.y = 0f;
            return true;
        }
        return false;
    }
}
