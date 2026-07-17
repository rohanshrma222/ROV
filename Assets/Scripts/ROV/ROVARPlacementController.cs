using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Controls AR placement of the ROV.
/// Detects scanned planes using ARRaycastManager and spawns/places the ROV on tap.
/// Once placed, it hides AR plane visualization and activates model interaction.
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class ROVARPlacementController : MonoBehaviour
{
    [Header("AR References")]
    [Tooltip("Reference to the ARPlaneManager to disable plane rendering after placement")]
    [SerializeField] ARPlaneManager planeManager;
    [Tooltip("The camera used for AR tracking (AR Camera)")]
    [SerializeField] Camera arCamera;

    [Header("ROV Placement Settings")]
    [Tooltip("The ROV Prefab to spawn when tapping on a scanned floor plane")]
    [SerializeField] GameObject rovPrefab;
    [Tooltip("Optional: Existing ROV in the scene to position instead of instantiating a new one")]
    [SerializeField] GameObject existingROV;

    private ARRaycastManager _raycastManager;
    private List<ARRaycastHit> _hits = new List<ARRaycastHit>();
    private GameObject _spawnedROV;
    private bool _isPlaced;

    void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
        if (planeManager == null)
        {
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        }
        if (arCamera == null)
        {
            arCamera = Camera.main;
        }
    }

    void Update()
    {
        // If already placed, do nothing
        if (_isPlaced) return;

        // Check for user touch or click
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceObject(Input.mousePosition);
        }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            TryPlaceObject(Input.GetTouch(0).position);
        }
#endif
    }

    private void TryPlaceObject(Vector2 screenPos)
    {
        // Raycast against trackable flat surfaces (horizontal planes)
        if (_raycastManager.Raycast(screenPos, _hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = _hits[0].pose;

            if (existingROV != null)
            {
                // Position existing ROV in scene
                _spawnedROV = existingROV;
                _spawnedROV.transform.position = hitPose.position;
                _spawnedROV.transform.rotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(hitPose.position - arCamera.transform.position, Vector3.up)
                );
                _spawnedROV.SetActive(true);
            }
            else if (rovPrefab != null)
            {
                // Instantiate a new ROV prefab at the hit location
                _spawnedROV = Instantiate(rovPrefab, hitPose.position, Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(hitPose.position - arCamera.transform.position, Vector3.up)
                ));
            }

            if (_spawnedROV != null)
            {
                _isPlaced = true;
                OnObjectPlaced();
            }
        }
    }

    private void OnObjectPlaced()
    {
        Debug.Log("[ROVARPlacement] ROV placed successfully in room.");

        // Disable plane detection/manager to stop rendering visual scanning meshes
        if (planeManager != null)
        {
            planeManager.enabled = false;
            foreach (var plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(false);
            }
        }

        // Add ROVModelRotator to the placed ROV dynamically if it's not already on it
        if (_spawnedROV != null && _spawnedROV.GetComponent<ROVModelRotator>() == null)
        {
            _spawnedROV.AddComponent<ROVModelRotator>();
        }
    }
}
