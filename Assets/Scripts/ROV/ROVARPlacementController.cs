using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Controls AR placement of the ROV.
/// Every frame, silently raycasts the centre of the screen against tracked planes
/// (no visible plane scan, no tap required) and places the ROV the instant a floor
/// is found underneath it.
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class ROVARPlacementController : MonoBehaviour
{
    [Header("AR References")]
    [Tooltip("Reference to the ARPlaneManager, used to query plane alignment and disable it after placement")]
    [SerializeField] ARPlaneManager planeManager;
    [Tooltip("The camera used for AR tracking (AR Camera)")]
    [SerializeField] Camera arCamera;
    [Tooltip("Used to anchor the placed ROV to the detected floor plane so it doesn't drift as tracking refines")]
    [SerializeField] ARAnchorManager anchorManager;
    [Tooltip("How far below the camera a feature point must be to be trusted as \"the floor\" before a proper plane has been scanned (metres)")]
    [SerializeField] float featurePointMinDropBelowCamera = 0.15f;

    [Header("ROV Placement Settings")]
    [Tooltip("The ROV visual-only Prefab to spawn as soon as a floor plane is found underneath the screen centre")]
    [SerializeField] GameObject rovPrefab;
    [Tooltip("Optional: Existing ROV in the scene to position instead of instantiating a new one")]
    [SerializeField] GameObject existingROV;
    [Tooltip("Corrective local rotation applied to the visual model so its authored forward axis matches the physics root's forward (+Z)")]
    [SerializeField] Vector3 modelForwardOffsetEuler = new Vector3(0f, -90f, 0f);

    [Header("Physics Rig (AR scale)")]
    [Tooltip("Rigidbody mass for the AR-scaled ROV")]
    [SerializeField] float rovMass = 1f;
    [Tooltip("CapsuleCollider radius/height sized for the AR-scaled ROV model")]
    [SerializeField] float colliderRadius = 0.07f;
    [SerializeField] float colliderHeight = 0.23f;

    [Header("On-Screen Joysticks")]
    [Tooltip("Left joystick: strafe/forward. Assign the scene's LeftJoystick (FixedJoystick).")]
    [SerializeField] Joystick moveJoystick;
    [Tooltip("Right joystick: yaw/ascend. Assign the scene's RightJoystick (FixedJoystick).")]
    [SerializeField] Joystick lookJoystick;

    [Header("Mission (optional)")]
    [Tooltip("Assign the scene's MissionController. Waypoints are scattered around the placement point and the mission starts once the ROV is placed, since ROVGame's world-scale waypoints don't apply in a room-scale AR space.")]
    [SerializeField] ROVMissionController missionController;
    [Tooltip("Local offsets (relative to the placement point and facing direction) for each spawned waypoint marker, in metres.")]
    [SerializeField]
    Vector3[] waypointOffsets =
    {
        new Vector3(0.3f, 0.2f, 0.5f),
        new Vector3(-0.4f, 0.25f, 0.7f),
        new Vector3(0.15f, 0.3f, 0.9f),
    };
    [SerializeField] string[] waypointLabels = { "Station Alpha", "Station Bravo", "Station Charlie" };
    [SerializeField] float waypointTriggerRadius = 0.15f;
    [SerializeField] float waypointMarkerSize = 0.08f;

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
        if (anchorManager == null)
        {
            anchorManager = GetComponent<ARAnchorManager>();
        }
    }

    void Update()
    {
        // If already placed, do nothing
        if (_isPlaced) return;

        // Keep trying every frame until a floor is found underneath the screen centre —
        // no tap and no visible plane-scan UI, it just appears as soon as tracking finds one.
        TryPlaceObject(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    }

    private void TryPlaceObject(Vector2 screenPos)
    {
        // Raycast against scanned plane polygons *and* raw feature points — planes give the
        // best-quality fix but can take a while to converge; feature points appear almost
        // immediately and let us place well before a full plane polygon has been built.
        if (!_raycastManager.Raycast(screenPos, _hits, TrackableType.PlaneWithinPolygon | TrackableType.FeaturePoint))
            return;

        // Prefer a real floor (upward-facing horizontal plane); a tap/hit landing on a
        // scanned wall/ceiling plane is ignored outright.
        ARRaycastHit? floorHit = null;
        ARPlane floorPlane = null;
        foreach (var hit in _hits)
        {
            var plane = planeManager != null ? planeManager.GetPlane(hit.trackableId) : null;
            if (plane != null && plane.alignment == PlaneAlignment.HorizontalUp)
            {
                floorHit = hit;
                floorPlane = plane;
                break;
            }
        }

        // No plane classified yet — fall back to the first feature point that's plausibly
        // on the floor (well below the camera, not a stray wall/ceiling point).
        if (floorHit == null)
        {
            foreach (var hit in _hits)
            {
                if (planeManager != null && planeManager.GetPlane(hit.trackableId) != null)
                    continue; // already rejected above as a non-floor plane

                if (arCamera.transform.position.y - hit.pose.position.y >= featurePointMinDropBelowCamera)
                {
                    floorHit = hit;
                    break;
                }
            }
        }
        if (floorHit == null) return;

        Pose hitPose = floorHit.Value.pose;
        Quaternion facingRotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(hitPose.position - arCamera.transform.position, Vector3.up)
        );

        // Anchor to the detected floor plane so the placement stays locked to the
        // real world as tracking refines, instead of drifting as a free-floating object.
        Transform anchorTransform = null;
        if (anchorManager != null && floorPlane != null)
        {
            ARAnchor anchor = anchorManager.AttachAnchor(floorPlane, hitPose);
            if (anchor != null) anchorTransform = anchor.transform;
        }

        if (existingROV != null)
        {
            // Position existing ROV in scene
            _spawnedROV = existingROV;
            _spawnedROV.transform.SetPositionAndRotation(hitPose.position, facingRotation);
            if (anchorTransform != null) _spawnedROV.transform.SetParent(anchorTransform, true);
            _spawnedROV.SetActive(true);
        }
        else if (rovPrefab != null)
        {
            _spawnedROV = SpawnControllableROV(hitPose.position, facingRotation, anchorTransform);
        }

        if (_spawnedROV != null)
        {
            _isPlaced = true;
            OnObjectPlaced();
            StartMissionAt(hitPose.position, facingRotation, anchorTransform);
        }
    }

    /// <summary>
    /// Builds a physics root (Rigidbody + CapsuleCollider + ROVController) with the visual-only
    /// rovPrefab parented under it as "ROV_Visual", mirroring the ROV rig used in ROVGame so the
    /// same joystick/keyboard controls work in AR. Keeping physics and visuals separate lets the
    /// visual model be drag-rotated for inspection without disturbing the Rigidbody.
    /// </summary>
    private GameObject SpawnControllableROV(Vector3 position, Quaternion rotation, Transform anchorParent)
    {
        var root = new GameObject("ROV");
        root.transform.SetPositionAndRotation(position, rotation);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = rovMass;
        rb.useGravity = false;

        var collider = root.AddComponent<CapsuleCollider>();
        collider.direction = 2; // Z-axis, matching the ROV's forward
        collider.radius = colliderRadius;
        collider.height = colliderHeight;

        var visual = Instantiate(rovPrefab, root.transform);
        visual.name = "ROV_Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(modelForwardOffsetEuler);
        RecenterVisualOnRoot(root.transform, visual.transform);

        var controller = root.AddComponent<ROVController>();
        controller.ConfigureJoysticks(moveJoystick, lookJoystick);

        if (anchorParent != null) root.transform.SetParent(anchorParent, true);

        return root;
    }

    /// <summary>
    /// The ROV model's authored pivot isn't necessarily at its visual centre, so after rotating
    /// it into place its mesh can end up noticeably off to one side of the placement point.
    /// Shifts the visual (horizontally only, so it still sits at its authored height) so its
    /// combined renderer bounds are centred on the physics root regardless of source pivot.
    /// </summary>
    private static void RecenterVisualOnRoot(Transform root, Transform visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        Vector3 worldOffset = combined.center - root.position;
        Vector3 localOffset = root.InverseTransformVector(worldOffset);
        visual.localPosition -= new Vector3(localOffset.x, 0f, localOffset.z);
    }

    /// <summary>
    /// Scatters waypoint markers around the placement point (ROVGame's own waypoints are
    /// hardcoded to its tens-of-metres open-world terrain and don't translate to a room-scale
    /// AR space), wires them into the mission controller, and starts the mission.
    /// </summary>
    private void StartMissionAt(Vector3 origin, Quaternion facing, Transform anchorParent)
    {
        if (missionController == null) return;

        var waypoints = new List<ROVWaypoint>();
        for (int i = 0; i < waypointOffsets.Length; i++)
        {
            Vector3 worldPos = origin + facing * waypointOffsets[i];

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"Waypoint_{i}";
            marker.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
            marker.transform.localScale = Vector3.one * waypointMarkerSize;
            if (anchorParent != null) marker.transform.SetParent(anchorParent, true);

            var markerRenderer = marker.GetComponent<MeshRenderer>();
            if (markerRenderer != null)
                markerRenderer.material.color = Color.cyan;

            string label = i < waypointLabels.Length ? waypointLabels[i] : $"Waypoint {i + 1}";
            var wp = marker.AddComponent<ROVWaypoint>();
            wp.Configure(label, i, waypointTriggerRadius);
            waypoints.Add(wp);
        }

        missionController.ConfigureWaypoints(waypoints);
        missionController.StartMission();
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
    }
}
