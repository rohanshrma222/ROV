using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Report screen shown after mission completion.
/// ROVMissionController.OnMissionComplete feeds the Gemini report text here.
/// Includes a scrollable text area and a clipboard copy button.
/// </summary>
public class ReportScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] TMP_Text  reportText;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] TMP_Text  missionTitleLabel;
    [SerializeField] TMP_Text  dataPointSummaryLabel;
    [SerializeField] Button    copyButton;
    [SerializeField] Button    closeButton;
    [SerializeField] GameObject reportPanel;

    [Header("Mission Controller (auto-found)")]
    [SerializeField] ROVMissionController missionController;

    string _reportContent = "";

    void Awake()
    {
        if (missionController == null)
            missionController = FindFirstObjectByType<ROVMissionController>();

        if (missionController != null)
            missionController.OnMissionComplete.AddListener(ShowReport);

        if (copyButton != null)
            copyButton.onClick.AddListener(CopyToClipboard);

        if (closeButton != null)
            closeButton.onClick.AddListener(() => reportPanel?.SetActive(false));

        if (reportPanel != null)
            reportPanel.SetActive(false);
    }

    void Start()
    {
        if (missionTitleLabel != null && MissionSelectorUI.ActiveMission != null)
            missionTitleLabel.text = MissionSelectorUI.ActiveMission.missionName.ToUpper() + " — MISSION REPORT";
    }

    void ShowReport(string reportContent)
    {
        _reportContent = reportContent;

        if (reportPanel != null)
            reportPanel.SetActive(true);

        if (reportText != null)
        {
            reportText.text = string.IsNullOrWhiteSpace(reportContent)
                ? "No report data available."
                : reportContent;
        }

        // Scroll to top
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        // Summary stats
        if (dataPointSummaryLabel != null && missionController != null)
            dataPointSummaryLabel.text =
                $"{missionController.ReachedCount} waypoints surveyed  ·  " +
                $"{missionController.ElapsedTime:F0}s elapsed";

        Debug.Log("[ReportScreenUI] Report displayed.");
    }

    void CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(_reportContent)) return;
        GUIUtility.systemCopyBuffer = _reportContent;
        if (copyButton != null)
        {
            var label = copyButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = "Copied!";
                Invoke(nameof(ResetCopyLabel), 2f);
            }
        }
    }

    void ResetCopyLabel()
    {
        if (copyButton == null) return;
        var label = copyButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = "Copy Report";
    }
}
