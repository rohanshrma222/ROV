using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Allows the user to click/tap and drag on the ROV model
/// to rotate and inspect it from all directions.
/// </summary>
public class ROVModelRotator : MonoBehaviour
{
    [SerializeField] float rotateSpeed = 0.4f;
    [SerializeField] float returnSpeed = 4f;

    private ROVController _controller;
    private Rigidbody _rb;
    private Camera _mainCam;
    private Transform _visualTransform;
    private Quaternion _initialLocalRotation = Quaternion.identity;
    private bool _isDragging;
    private Vector2 _lastPointerPos;

    void Awake()
    {
        _controller = GetComponent<ROVController>();
        _rb = GetComponent<Rigidbody>();
        _mainCam = Camera.main;

        // Find the visual model transform
        _visualTransform = transform.Find("ROV_Visual");
        if (_visualTransform == null && transform.childCount > 0)
        {
            _visualTransform = transform.GetChild(0); // Fallback to first child
        }

        if (_visualTransform != null)
        {
            _initialLocalRotation = _visualTransform.localRotation;
        }
    }

    void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;

        bool pressActive = pointer.press.isPressed;
        Vector2 pointerPos = pointer.position.ReadValue();

        if (pressActive)
        {
            if (!_isDragging)
            {
                if (IsPointerOverROV(pointerPos))
                {
                    StartDrag(pointerPos);
                }
            }
            else
            {
                ContinueDrag(pointerPos);
            }
        }
        else
        {
            if (_isDragging)
            {
                EndDrag();
            }
            else if (_visualTransform != null)
            {
                // Smoothly slerp visual back to its initial forward alignment when not dragging
                _visualTransform.localRotation = Quaternion.Slerp(
                    _visualTransform.localRotation, 
                    _initialLocalRotation, 
                    Time.deltaTime * returnSpeed
                );
            }
        }
    }

    bool IsPointerOverROV(Vector2 position)
    {
        if (_mainCam == null) return false;

        Ray ray = _mainCam.ScreenPointToRay(position);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return hit.transform.IsChildOf(transform) || hit.transform == transform;
        }
        return false;
    }

    void StartDrag(Vector2 startPos)
    {
        _isDragging = true;
        _lastPointerPos = startPos;

        if (_controller != null)
        {
            _controller.IsDraggingModel = true;
        }
    }

    void ContinueDrag(Vector2 currentPos)
    {
        if (_visualTransform == null) return;

        Vector2 delta = currentPos - _lastPointerPos;
        _lastPointerPos = currentPos;

        if (delta.magnitude > 0.05f)
        {
            // Rotate the visual mesh only (Yaw around world-up, Pitch around local-right)
            _visualTransform.Rotate(Vector3.up, -delta.x * rotateSpeed, Space.World);
            _visualTransform.Rotate(Vector3.right, delta.y * rotateSpeed, Space.Self);
        }
    }

    void EndDrag()
    {
        _isDragging = false;

        if (_controller != null)
        {
            _controller.IsDraggingModel = false;
        }
    }
}
