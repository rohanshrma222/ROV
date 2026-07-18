using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mission HUD panel: waypoint counter, distance to next waypoint,
/// and "ON STATION" overlay prompt.
/// Called each frame by ROVMissionController.
/// </summary>
public class MissionHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] TMP_Text waypointCounterLabel;   // e.g. "WAYPOINT  2 / 5"
    [SerializeField] TMP_Text distanceLabel;          // e.g. "NEXT  12.4 m"
    [SerializeField] TMP_Text missionNameLabel;       // top bar

    [Header("ON STATION Overlay")]
    [SerializeField] GameObject onStationOverlay;     // panel with large "ON STATION" text
    [SerializeField] float      onStationDisplayTime = 2.5f;

    [Header("Mission Start")]
    [SerializeField] ROVMissionController missionController;
    [Tooltip("Start the mission automatically on scene load. Disable when the mission should only begin after something else happens first (e.g. AR placement), and call missionController.StartMission() manually at that point instead.")]
    [SerializeField] bool autoStartOnAwake = true;

    float _onStationTimer;

    void Awake()
    {
        if (missionController == null)
            missionController = FindFirstObjectByType<ROVMissionController>();

        if (onStationOverlay != null)
            onStationOverlay.SetActive(false);

        // Show mission name from selector
        if (missionNameLabel != null && MissionSelectorUI.ActiveMission != null)
            missionNameLabel.text = MissionSelectorUI.ActiveMission.missionName.ToUpper();
    }

    void Start()
    {
        if (autoStartOnAwake && missionController != null)
            missionController.StartMission();
    }

    void Update()
    {
        if (_onStationTimer > 0f)
        {
            _onStationTimer -= Time.deltaTime;
            if (_onStationTimer <= 0f && onStationOverlay != null)
                onStationOverlay.SetActive(false);
        }
    }

    /// <summary>Called by ROVMissionController every frame + on waypoint reached.</summary>
    public void SetMissionState(int reached, int total, float distanceToNext, bool justArrived)
    {
        if (waypointCounterLabel != null)
            waypointCounterLabel.text = $"WAYPOINT  {reached} / {total}";

        if (distanceLabel != null)
        {
            if (distanceToNext < 0f || reached >= total)
                distanceLabel.text = reached >= total ? "SURVEY COMPLETE" : "";
            else
                distanceLabel.text = $"NEXT  <b>{distanceToNext:F1}</b> m";
        }

        if (justArrived && onStationOverlay != null)
        {
            onStationOverlay.SetActive(true);
            _onStationTimer = onStationDisplayTime;
        }
    }
}
