using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mission selection screen. Assign 3 ROVMissionUIData assets and 3 buttons.
/// Selecting a mission stores it in the static ActiveMission slot, then loads
/// the ROVGame scene.
/// </summary>
public class MissionSelectorUI : MonoBehaviour
{
    [Header("Mission Definitions")]
    [SerializeField] ROVMissionUIData coralHealthMission;
    [SerializeField] ROVMissionUIData speciesTransectMission;
    [SerializeField] ROVMissionUIData waterColumnMission;

    [Header("Mission Card UI (3 buttons)")]
    [SerializeField] Button coralHealthButton;
    [SerializeField] Button speciesTransectButton;
    [SerializeField] Button waterColumnButton;

    [Header("Card Labels (optional — auto-fills from ScriptableObject)")]
    [SerializeField] TMP_Text coralNameLabel;
    [SerializeField] TMP_Text coralDescLabel;
    [SerializeField] TMP_Text speciesNameLabel;
    [SerializeField] TMP_Text speciesDescLabel;
    [SerializeField] TMP_Text waterNameLabel;
    [SerializeField] TMP_Text waterDescLabel;

    [Header("Scene to load")]
    [SerializeField] string rovGameScene = "ROVGame";

    // ── Static mission handoff ───────────────────────────────────────────────
    public static ROVMissionUIData ActiveMission;

    void Start()
    {
        PopulateCard(coralHealthMission,       coralNameLabel,   coralDescLabel);
        PopulateCard(speciesTransectMission,   speciesNameLabel, speciesDescLabel);
        PopulateCard(waterColumnMission,       waterNameLabel,   waterDescLabel);

        if (coralHealthButton != null)
            coralHealthButton.onClick.AddListener(() => SelectMission(coralHealthMission));
        if (speciesTransectButton != null)
            speciesTransectButton.onClick.AddListener(() => SelectMission(speciesTransectMission));
        if (waterColumnButton != null)
            waterColumnButton.onClick.AddListener(() => SelectMission(waterColumnMission));
    }

    void SelectMission(ROVMissionUIData mission)
    {
        ActiveMission = mission;
        SceneManager.LoadScene(rovGameScene);
    }

    static void PopulateCard(ROVMissionUIData data, TMP_Text nameLabel, TMP_Text descLabel)
    {
        if (data == null) return;
        if (nameLabel != null) nameLabel.text = data.missionName;
        if (descLabel != null) descLabel.text = data.missionDescription;
    }
}
