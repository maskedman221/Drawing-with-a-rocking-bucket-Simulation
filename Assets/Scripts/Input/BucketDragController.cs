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
    private Plane dragPlane;
    private Vector3 dragOffset;
    private Vector3 currentRopeEnd;
    private bool hasCurrentRopeEnd;

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

    void OnValidate()
    {
        ropeLength = Mathf.Max(0.001f, ropeLength);
    }

    public void SetRopeLength(float value)
    {
        ropeLength = Mathf.Max(0.001f, value);
    }

    public void SetCurrentRopeEnd(Vector3 ropeEnd)
    {
        currentRopeEnd = ropeEnd;
        hasCurrentRopeEnd = true;

        if (!IsDragging)
        {
            DraggedPosition = ropeEnd;
        }
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
                dragPlane = new Plane(-mainCamera.transform.forward, hit.point);
                DraggedPosition = hasCurrentRopeEnd
                    ? ProjectToRopeLength(currentRopeEnd)
                    : ProjectToRopeLength(hit.point);
                dragOffset = DraggedPosition - hit.point;
            }
        }

        // Mouse Hold: drag on a stable click plane, then project back to rope length.
        if (Mouse.current.leftButton.isPressed && bucketSelected)
        {
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 planePoint = ray.GetPoint(enter);
                DraggedPosition = ProjectToRopeLength(planePoint + dragOffset);
            }
        }

        // Mouse Up
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            bucketSelected = false;
            IsDragging = false;
        }
    }

    Vector3 ProjectToRopeLength(Vector3 point)
    {
        Vector3 offset = point - anchor.position;

        if (offset.sqrMagnitude < 0.000001f)
        {
            offset = DraggedPosition - anchor.position;
        }

        if (offset.sqrMagnitude < 0.000001f)
        {
            offset = Vector3.down;
        }

        return anchor.position + offset.normalized * ropeLength;
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
