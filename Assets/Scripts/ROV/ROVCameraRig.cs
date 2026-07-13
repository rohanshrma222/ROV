using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Switches the active camera between a first-person forward-facing camera
/// and a third-person chase camera orbiting behind the ROV.
/// Press V to toggle. Assign the two camera GameObjects in the Inspector.
/// </summary>
public class ROVCameraRig : MonoBehaviour
{
    [Header("Camera References")]
    [Tooltip("Camera mounted at the front of the ROV (first person).")]
    [SerializeField] Camera forwardCam;
    [Tooltip("Camera positioned behind/above the ROV (third person chase).")]
    [SerializeField] Camera chaseCam;

    [Header("Chase Cam Settings")]
    [SerializeField] Vector3 chaseOffset  = new Vector3(0f, 3f, -8f);
    [SerializeField] float   chaseDamping = 5f;

    bool _firstPerson = false;   // start in 3rd-person so model is visible

    void Awake()
    {
        if (forwardCam == null || chaseCam == null)
        {
            Debug.LogWarning("[ROVCameraRig] One or both cameras are not assigned. Auto-searching children.");
            var cams = GetComponentsInChildren<Camera>(true);
            if (cams.Length >= 2)
            {
                forwardCam = cams[0];
                chaseCam   = cams[1];
            }
        }
        ApplyCameraState();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            SwitchCamera();
    }

    void LateUpdate()
    {
        // Smooth chase camera position
        if (!_firstPerson && chaseCam != null)
        {
            Vector3 target = transform.TransformPoint(chaseOffset);
            chaseCam.transform.position = Vector3.Lerp(
                chaseCam.transform.position, target, Time.deltaTime * chaseDamping);
            chaseCam.transform.LookAt(transform.position + Vector3.up * 0.5f);
        }
    }

    /// <summary>Called by UI button.</summary>
    public void SwitchCamera()
    {
        _firstPerson = !_firstPerson;
        ApplyCameraState();
    }

    void ApplyCameraState()
    {
        if (forwardCam != null) forwardCam.gameObject.SetActive(_firstPerson);
        if (chaseCam   != null) chaseCam.gameObject.SetActive(!_firstPerson);

        // Tag the active camera as MainCamera so World.cs uses it
        if (_firstPerson && forwardCam != null)
            forwardCam.tag = "MainCamera";
        else if (!_firstPerson && chaseCam != null)
            chaseCam.tag = "MainCamera";
    }

    public bool IsFirstPerson => _firstPerson;
}
