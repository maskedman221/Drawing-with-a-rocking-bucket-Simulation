// using UnityEngine;

// public class PaintingCollision : MonoBehaviour
// {
//     [SerializeField]
//     private Transform plane;
    
//     [SerializeField]
//     private SurfaceMaterial defaultMaterial;
    
//     [Header("Friction Settings")]
//     [Range(0f, 1f)]
//     public float staticFriction = 0.9f;    // Friction when nearly stopped
//     [Range(0f, 1f)]
//     public float dynamicFriction = 0.5f;   // Friction when moving
//     public float stopThreshold = 0.01f;    // Speed below which particle stops
    
//     private SurfaceMaterial planeMaterial;
//     private float planeY;
    
//     void Start()
//     {
//         if (plane == null)
//         {
//             Debug.LogError("Plane is not assigned in PaintingCollision!");
//             return;
//         }
        
//         planeMaterial = defaultMaterial;
//         if (planeMaterial == null)
//         {
//             Debug.LogError("No SurfaceMaterial assigned! Creating default.");
//             planeMaterial = ScriptableObject.CreateInstance<SurfaceMaterial>();
//             planeMaterial.restitution = 0.0f;
//             planeMaterial.friction = 0.9f;
//             planeMaterial.roughness = 0.0f;
//         }
        
//         planeY = plane.position.y;
//     }
    
//     public bool Constrain(ref Vector3 pos, ref Vector3 vel, bool onPlane)
//     {
//         if (plane == null || planeMaterial == null)
//             return false;
        
//         if (pos.y <= planeY + 0.01f)
//         {
//             // Position correction
//             pos.y = planeY + 0.01f;
            
//             float restitution = planeMaterial.restitution;
//             float materialFriction = planeMaterial.friction;
//             float roughness = planeMaterial.roughness;
            
//             // VERTICAL (BOUNCE)
//             if (vel.y < 0)
//             {
//                 vel.y = -vel.y * restitution;
//                 if (restitution < 0.01f) vel.y = 0;
//             }
            
//             // HORIZONTAL (ADVANCED FRICTION)
//             Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);
//             float horizontalSpeed = horizontalVel.magnitude;
            
//             if (horizontalSpeed > 0.001f)
//             {
//                 // Calculate friction based on speed
//                 // Static friction is higher when nearly stopped
//                 float speedRatio = Mathf.Clamp01(horizontalSpeed / 0.1f);
//                 float effectiveFriction = Mathf.Lerp(staticFriction, dynamicFriction, speedRatio);
                
//                 // Apply friction
//                 float frictionFactor = Mathf.Max(0f, 1f - effectiveFriction * 0.5f);
//                 vel.x *= frictionFactor;
//                 vel.z *= frictionFactor;
                
//                 // Additional damping to ensure it eventually stops
//                 float damping = 1f - (1f - materialFriction) * 0.1f;
//                 vel.x *= damping;
//                 vel.z *= damping;
                
//                 // STOP THRESHOLD: If speed is below threshold, stop completely
//                 if (horizontalSpeed < stopThreshold)
//                 {
//                     vel.x = 0;
//                     vel.z = 0;
//                 }
//             }
//             else
//             {
//                 // Already stopped - keep it stopped
//                 vel.x = 0;
//                 vel.z = 0;
//             }
            
//             // ROUGHNESS
//             if (roughness > 0.001f && horizontalSpeed > 0.01f)
//             {
//                 float angle = Random.Range(0f, 2f * Mathf.PI);
//                 float magnitude = roughness * 0.2f;
//                 vel.x += Mathf.Cos(angle) * magnitude;
//                 vel.z += Mathf.Sin(angle) * magnitude;
//             }
            
//             return true;
//         }
        
//         return false;
//     }
// }

using UnityEngine;

public class PaintingCollision : MonoBehaviour
{
    [SerializeField]
    public Transform plane;
    [SerializeField]
    public SurfaceMaterial surfaceMaterial;
    [SerializeField]
    public float damping = 0.4f;
    Vector3 normal;
    void Start()
    {
       if (plane == null)
       {
           plane = transform;
       }

       if (surfaceMaterial == null)
       {
           surfaceMaterial = ScriptableObject.CreateInstance<SurfaceMaterial>();
           surfaceMaterial.restitution = 0.1f;
           surfaceMaterial.friction = 0.55f;
           surfaceMaterial.roughness = 0.2f;
       }

       normal = plane.up;

    }

    // void Update()
    // {
        
    // }

    public bool Constrain(ref Vector3 pos, ref Vector3 vel, float viscosity , bool onPlane)
    {
        if (plane == null)
        {
            plane = transform;
        }

        if (surfaceMaterial == null)
        {
            surfaceMaterial = ScriptableObject.CreateInstance<SurfaceMaterial>();
            surfaceMaterial.restitution = 0.1f;
            surfaceMaterial.friction = 0.55f;
            surfaceMaterial.roughness = 0.2f;
        }

        normal = plane.up;

        if (onPlane && pos.y <= plane.position.y)
        {
            pos.y = plane.position.y; 
            vel.y = 0;
        }
        if(pos.y <= plane.position.y)
        {
            pos.y = plane.position.y;
            Vector3 normalVelocity = Vector3.Project(vel, normal);
            Vector3 tangentialVelocity = vel - normalVelocity;
            normalVelocity *= -surfaceMaterial.restitution;
            tangentialVelocity *= (1f - surfaceMaterial.friction);
            tangentialVelocity *= Mathf.Exp(-viscosity * Time.fixedDeltaTime);
            vel  = normalVelocity + tangentialVelocity;
            // Debug.Log(vel);
            return true;
        }

        return false;
    }
}
