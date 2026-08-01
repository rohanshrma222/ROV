using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lets the player tap/click-drag on a scanned creature model to rotate and inspect it
/// from all directions — the same interaction as ROVModelRotator, but for a standalone
/// spawned model instead of the ROV (no ROVController/Rigidbody coupling needed).
/// Requires a Collider on this GameObject for hit-testing (added by whoever spawns the
/// model, since imported models don't come with one).
/// </summary>
[RequireComponent(typeof(Collider))]
public class CreatureModelRotator : MonoBehaviour
{
    [SerializeField] float rotateSpeed = 0.4f;
    [SerializeField] float returnSpeed = 4f;

    Camera _mainCam;
    Quaternion _initialLocalRotation;
    bool _isDragging;
    Vector2 _lastPointerPos;

    void Awake()
    {
        _mainCam = Camera.main;
        _initialLocalRotation = transform.localRotation;
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
                if (IsPointerOverModel(pointerPos))
                    StartDrag(pointerPos);
            }
            else
            {
                ContinueDrag(pointerPos);
            }
        }
        else if (_isDragging)
        {
            _isDragging = false;
        }
        else
        {
            // Smoothly return to the original facing when not being dragged.
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, _initialLocalRotation, Time.deltaTime * returnSpeed);
        }
    }

    bool IsPointerOverModel(Vector2 position)
    {
        if (_mainCam == null) return false;

        Ray ray = _mainCam.ScreenPointToRay(position);
        if (Physics.Raycast(ray, out RaycastHit hit))
            return hit.transform.IsChildOf(transform) || hit.transform == transform;
        return false;
    }

    void StartDrag(Vector2 startPos)
    {
        _isDragging = true;
        _lastPointerPos = startPos;
    }

    void ContinueDrag(Vector2 currentPos)
    {
        Vector2 delta = currentPos - _lastPointerPos;
        _lastPointerPos = currentPos;

        if (delta.magnitude > 0.05f)
        {
            transform.Rotate(Vector3.up, -delta.x * rotateSpeed, Space.World);
            transform.Rotate(Vector3.right, delta.y * rotateSpeed, Space.Self);
        }
    }
}
