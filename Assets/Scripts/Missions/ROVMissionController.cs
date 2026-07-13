using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Core mission orchestrator. Place in scene and wire waypoints in the Inspector.
/// Flow: StartMission() → ROV flies to each waypoint → CaptureDataPoint() auto-fires
/// → all waypoints complete → MissionComplete() → GenerateReport().
/// </summary>
public class ROVMissionController : MonoBehaviour
{
    [Header("Mission Setup")]
    [SerializeField] ROVMissionUIData missionData;
    [SerializeField] List<ROVWaypoint> waypoints = new();

    [Header("Scan Settings")]
    [Tooltip("Creature scan radius at each waypoint")]
    [SerializeField] float creatureScanRadius = 15f;

    [Header("References")]
    [SerializeField] MissionReportGenerator reportGenerator;
    [SerializeField] MissionHUD             missionHUD;

    [Header("Events")]
    public UnityEvent<int, int>   OnWaypointReached;    // (current, total)
    public UnityEvent<DataPoint>  OnDataPointCaptured;
    public UnityEvent<string>     OnMissionComplete;    // report text

    // ── State ───────────────────────────────────────────────────────────────
    int              _currentIndex;
    float            _missionStartTime;
    bool             _missionActive;
    List<DataPoint>  _dataLog = new();

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

    void Awake()
    {
        if (reportGenerator == null)
            reportGenerator = FindFirstObjectByType<MissionReportGenerator>();
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
            if (wp != null)
            {
                wp.Reset();
                wp.OnROVEntered.RemoveListener(HandleWaypointReached);
                wp.OnROVEntered.AddListener(HandleWaypointReached);
            }
        }

        _currentIndex    = 0;
        _missionStartTime = Time.time;
        _missionActive   = true;
        _dataLog.Clear();

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
        if (wp.waypointIndex != _currentIndex && waypoints.IndexOf(wp) != _currentIndex)
            return;   // out-of-order trigger, ignore

        var dp = CaptureDataPoint(wp.waypointLabel);
        _dataLog.Add(dp);
        OnDataPointCaptured?.Invoke(dp);
        OnWaypointReached?.Invoke(_currentIndex + 1, waypoints.Count);

        if (missionHUD != null)
            missionHUD.SetMissionState(_currentIndex + 1, waypoints.Count, -1f, true);

        Debug.Log($"[ROVMissionController] Waypoint {_currentIndex + 1}/{waypoints.Count} reached.");

        _currentIndex++;

        if (_currentIndex >= waypoints.Count)
            CompleteMission().Forget();
    }

    DataPoint CaptureDataPoint(string label)
    {
        var player  = World.GetPlayer();
        var env     = World.GetEnvironment();
        var creatures = World.GetCreatures(creatureScanRadius);

        string[] names = new string[creatures.Count];
        for (int i = 0; i < creatures.Count; i++)
            names[i] = creatures[i].name;

        return new DataPoint
        {
            waypointName     = label,
            position         = new Vector3(player.x, player.y, player.z),
            depth            = player.depthMeters,
            temperature      = env.waterTemperatureC,
            pH               = env.pH,
            visibility       = env.visibilityMeters,
            biome            = env.biome,
            lightLevel       = env.lightLevel,
            creaturesNearby  = names,
            timestamp        = ElapsedTime,
        };
    }

    async UniTaskVoid CompleteMission()
    {
        _missionActive = false;
        Debug.Log("[ROVMissionController] All waypoints complete — generating report…");

        string report = "Report generation unavailable.";
        if (reportGenerator != null)
            report = await reportGenerator.GenerateReport(_dataLog);

        OnMissionComplete?.Invoke(report);
        Debug.Log("[ROVMissionController] Mission complete.");
    }
}
