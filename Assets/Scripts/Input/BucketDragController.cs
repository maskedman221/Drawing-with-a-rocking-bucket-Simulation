using UnityEngine;
using UnityEngine.InputSystem;
public class BucketDragController : MonoBehaviour
{
    public bool IsDragging { get; private set; }

    public Vector3 DraggedPosition { get; private set; }

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Transform anchor;

    [SerializeField]
    private float ropeLength = 5f;

    [SerializeField]
    private LayerMask bucketMask;

    private bool bucketSelected;

    private Plane dragPlane;

    private void Update()
    {
        HandleInput();
    }

     private void HandleInput()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        // Mouse down
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, bucketMask))
            {
                bucketSelected = true;
                IsDragging = true;

                dragPlane = new Plane(Vector3.forward, hit.point);
            }
        }

        // Mouse held
        if (Mouse.current.leftButton.isPressed && bucketSelected)
        {
            if (dragPlane.Raycast(ray, out float enter))
            {
                Vector3 point = ray.GetPoint(enter);

                Vector3 dir = (point - anchor.position).normalized;

                DraggedPosition = anchor.position + dir * ropeLength;
            }
        }

        // Mouse released
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            bucketSelected = false;
            IsDragging = false;
        }
    }
}