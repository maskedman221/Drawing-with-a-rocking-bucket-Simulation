using UnityEngine;
using UnityEngine.InputSystem;

public class BucketDragController : MonoBehaviour
{
    public bool IsDragging { get; private set; }
    public Vector3 DraggedPosition { get; private set; }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform anchor;

    [Header("Rope Settings")]
    [SerializeField] private float ropeLength = 5f;
    [SerializeField] private LayerMask bucketMask;

    private bool bucketSelected;

    void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        if (anchor == null)
        {
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        // -------------------------
        // Mouse Down: select bucket
        // -------------------------
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, bucketMask))
            {
                bucketSelected = true;
                IsDragging = true;
            }
        }

        // Mouse Hold: free 3D drag

        if (Mouse.current.leftButton.isPressed && bucketSelected)
        {
            float camDistance = Vector3.Distance(anchor.position, mainCamera.transform.position);

            Vector3 worldPoint = ray.GetPoint(camDistance);

            Vector3 dir = (worldPoint - anchor.position).normalized;

            DraggedPosition = anchor.position + dir * ropeLength;
        }

        // Mouse Up
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            bucketSelected = false;
            IsDragging = false;
        }
    }
}


// using UnityEngine;
// using UnityEngine.InputSystem;
// public class BucketDragController : MonoBehaviour
// {
//     public bool IsDragging { get; private set; }

//     public Vector3 DraggedPosition { get; private set; }

//     [SerializeField]
//     private Camera mainCamera;

//     [SerializeField]
//     private Transform anchor;

//     [SerializeField]
//     private float ropeLength = 5f;

//     [SerializeField]
//     private LayerMask bucketMask;

//     private bool bucketSelected;

//     private Plane dragPlane;

//     private void Update()
//     {
//         HandleInput();
//     }

//      private void HandleInput()
//     {
//         Vector2 mousePos = Mouse.current.position.ReadValue();

//         Ray ray = mainCamera.ScreenPointToRay(mousePos);

//         // Mouse down
//         if (Mouse.current.leftButton.wasPressedThisFrame)
//         {
//             if (Physics.Raycast(ray, out RaycastHit hit, 100f, bucketMask))
//             {
//                 bucketSelected = true;
//                 IsDragging = true;

//                 dragPlane = new Plane(Vector3.forward, hit.point);
//             }
//         }

//         // Mouse held
//         if (Mouse.current.leftButton.isPressed && bucketSelected)
//         {
//             if (dragPlane.Raycast(ray, out float enter))
//             {
//                 Vector3 point = ray.GetPoint(enter);

//                 Vector3 dir = (point - anchor.position).normalized;

//                 DraggedPosition = anchor.position + dir * ropeLength;
//             }
//         }

//         // Mouse released
//         if (Mouse.current.leftButton.wasReleasedThisFrame)
//         {
//             bucketSelected = false;
//             IsDragging = false;
//         }
//     }
// }
