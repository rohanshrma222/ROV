using UnityEngine;

/// <summary>
/// Drop this on any GameObject in ROVGame to allow direct Play-in-Editor
/// without going through the MissionSelect scene.
/// Populates MissionSelectorUI.ActiveMission with a default preset if none is set.
/// </summary>
public class MissionSceneBootstrap : MonoBehaviour
{
    [SerializeField] ROVMissionUIData defaultMission;

    void Awake()
    {
        // If launched directly (no mission selected via menu), use the default preset
        if (MissionSelectorUI.ActiveMission == null && defaultMission != null)
        {
            MissionSelectorUI.ActiveMission = defaultMission;
            Debug.Log("[MissionSceneBootstrap] No active mission — using default: " + defaultMission.missionName);
        }
    }
}
