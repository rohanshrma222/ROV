using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Core mission orchestrator. Place in scene and wire waypoints in the Inspector.
/// Flow: StartMission() → ROV flies to each waypoint → all waypoints complete → MissionComplete().
/// </summary>
public class ROVMissionController : MonoBehaviour
{
    [Header("Mission Setup")]
    [SerializeField] ROVMissionUIData missionData;
    [SerializeField] List<ROVWaypoint> waypoints = new();
    [Tooltip("If true, waypoints must be reached in list order and out-of-order triggers are ignored. Disable for layouts (e.g. AR, where waypoints are scattered with no visible ordering cue) where the player can't tell which one is \"next\".")]
    [SerializeField] bool requireSequentialOrder = true;

    [Header("References")]
    [SerializeField] MissionHUD missionHUD;

    [Header("Events")]
    public UnityEvent<int, int>   OnWaypointReached;    // (current, total)
    public UnityEvent<string>     OnMissionComplete;    // report text

    // ── State ───────────────────────────────────────────────────────────────
    int              _currentIndex;
    float            _missionStartTime;
    bool             _missionActive;

    // ── Public API ──────────────────────────────────────────────────────────

    public int   TotalWaypoints  => waypoints.Count;
    public int   ReachedCount    => _currentIndex;
    public bool  MissionActive   => _missionActive;
    public float ElapsedTime     => _missionActive ? Time.time - _missionStartTime : 0f;

    /// Distance to the next waypoint in world metres (-1 if mission done).
    public float DistanceToNext
    {
        get
        {
            if (!_missionActive || _currentIndex >= waypoints.Count || waypoints[_currentIndex] == null) return -1f;
            var rov = FindFirstObjectByType<ROVController>();
            if (rov == null) return -1f;
            return Vector3.Distance(rov.transform.position, waypoints[_currentIndex].transform.position);
        }
    }

    public ROVWaypoint CurrentWaypoint =>
        (_currentIndex < waypoints.Count) ? waypoints[_currentIndex] : null;

    /// <summary>
    /// The waypoint object actually reached most recently. Unlike CurrentWaypoint (which is
    /// just "the next un-visited slot in original list order"), this is correct even when
    /// requireSequentialOrder is false and waypoints are reached out of order — e.g. in AR,
    /// where CurrentWaypoint would otherwise point at the wrong physical location.
    /// </summary>
    public ROVWaypoint LastReachedWaypoint { get; private set; }

    /// <summary>
    /// Replaces the waypoint list at runtime, for waypoints spawned dynamically relative to an
    /// AR placement point rather than authored at fixed positions in the Inspector.
    /// </summary>
    public void ConfigureWaypoints(List<ROVWaypoint> newWaypoints)
    {
        waypoints = newWaypoints;
    }

    /// <summary>Call before StartMission() to allow waypoints to be reached in any order.</summary>
    public void SetRequireSequentialOrder(bool required)
    {
        requireSequentialOrder = required;
    }

    void Awake()
    {
        if (missionHUD == null)
            missionHUD = FindFirstObjectByType<MissionHUD>();

        // Find and temporarily activate ReportScreenUI so its Awake() runs and subscribes to OnMissionComplete
        var reportUI = FindFirstObjectByType<ReportScreenUI>(FindObjectsInactive.Include);
        if (reportUI != null)
        {
            reportUI.gameObject.SetActive(true);
        }
    }

    void Start()
    {
    }

    public void StartMission()
    {
        if (_missionActive)
        {
            Debug.LogWarning("[ROVMissionController] Mission already active.");
            return;
        }

        // Clean up any deleted/null waypoints from the Inspector list
        waypoints.RemoveAll(wp => wp == null);

        // Dynamically load active mission selection if available (populated by bootstrappers or selectors)
        if (MissionSelectorUI.ActiveMission != null)
        {
            missionData = MissionSelectorUI.ActiveMission;
        }

        // Limit physical waypoints in the scene to the count specified in the mission definition
        if (missionData != null && missionData.waypointCount > 0 && waypoints.Count > missionData.waypointCount)
        {
            waypoints = waypoints.GetRange(0, missionData.waypointCount);
        }

        // Reset and subscribe to waypoints
        foreach (var wp in waypoints)
        {
            if (wp != null && wp.OnROVEntered != null)
            {
                wp.Reset();
                wp.OnROVEntered.RemoveListener(HandleWaypointReached);
                wp.OnROVEntered.AddListener(HandleWaypointReached);
            }
        }

        _currentIndex    = 0;
        _missionStartTime = Time.time;
        _missionActive   = true;

        if (missionHUD != null)
            missionHUD.SetMissionState(0, waypoints.Count, -1f, false);

        Debug.Log($"[ROVMissionController] Mission started: {(missionData != null ? missionData.missionName : "Unnamed")}");
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_missionActive || _currentIndex >= waypoints.Count) return;
        if (missionHUD != null)
            missionHUD.SetMissionState(_currentIndex, waypoints.Count, DistanceToNext, false);
    }

    void HandleWaypointReached(ROVWaypoint wp)
    {
        if (!_missionActive) return;
        if (requireSequentialOrder && wp.waypointIndex != _currentIndex && waypoints.IndexOf(wp) != _currentIndex)
        {
            Debug.LogWarning($"[ROVMissionController] '{wp.waypointLabel}' ignored: out of order (expected index {_currentIndex}).");
            return;
        }

        LastReachedWaypoint = wp;

        OnWaypointReached?.Invoke(_currentIndex + 1, waypoints.Count);

        if (missionHUD != null)
            missionHUD.SetMissionState(_currentIndex + 1, waypoints.Count, -1f, true);

        Debug.Log($"[ROVMissionController] Waypoint {_currentIndex + 1}/{waypoints.Count} reached.");

        _currentIndex++;

        if (_currentIndex >= waypoints.Count)
            CompleteMission();
    }

    void CompleteMission()
    {
        _missionActive = false;
        OnMissionComplete?.Invoke("");
        Debug.Log("[ROVMissionController] Mission complete.");
    }
}
