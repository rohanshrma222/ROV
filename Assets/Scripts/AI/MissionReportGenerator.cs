using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// Takes the list of DataPoints collected during a mission, builds a
/// structured scientific prompt, and calls Gemini via MissionChatManager.
/// Call GenerateReport() after mission completion.
/// </summary>
public class MissionReportGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] MissionChatManager chatManager;
    [SerializeField] TMP_Text           reportOutputLabel;   // on the ReportScreen

    [Header("Mission Context")]
    [SerializeField] string missionName = "ROV Survey";

    void Awake()
    {
        if (chatManager == null)
            chatManager = FindFirstObjectByType<MissionChatManager>();
    }

    /// <summary>
    /// Generates an AI mission report from collected data points.
    /// Populates reportOutputLabel and returns the text.
    /// </summary>
    public async UniTask<string> GenerateReport(List<DataPoint> dataPoints)
    {
        if (chatManager == null)
        {
            Debug.LogError("[MissionReportGenerator] MissionChatManager not found.");
            return "Report unavailable — MissionChatManager missing.";
        }

        string prompt = BuildPrompt(dataPoints);
        Debug.Log("[MissionReportGenerator] Sending report prompt to Gemini…");

        string report = await chatManager.AskGemini(prompt);

        if (reportOutputLabel != null)
            reportOutputLabel.text = report;

        return report;
    }

    string BuildPrompt(List<DataPoint> pts)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## ROV Mission Report Request: {missionName}");
        sb.AppendLine();
        sb.AppendLine("You are NAVIGATOR, an expert marine biology AI. The ROV has completed its survey.");
        sb.AppendLine("Below is the sensor log collected at each waypoint. Provide a concise scientific");
        sb.AppendLine("field report including: environmental summary, species observations, water quality");
        sb.AppendLine("assessment (temperature trend, pH health), notable findings, and one recommendation.");
        sb.AppendLine();
        sb.AppendLine("### Sensor Log");

        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            sb.AppendLine($"\n**Waypoint {i + 1} — {p.waypointName}**");
            sb.AppendLine($"- Depth: {p.depth:F1} m | Biome: {p.biome}");
            sb.AppendLine($"- Temperature: {p.temperature:F1}°C | pH: {p.pH:F2}");
            sb.AppendLine($"- Visibility: {p.visibility:F0} m | Light: {p.lightLevel}");
            if (p.creaturesNearby != null && p.creaturesNearby.Length > 0)
                sb.AppendLine($"- Species observed: {string.Join(", ", p.creaturesNearby)}");
            else
                sb.AppendLine("- Species observed: none detected");
            sb.AppendLine($"- Timestamp: T+{p.timestamp:F0}s");
        }

        sb.AppendLine();
        sb.AppendLine("Generate the field report now.");
        return sb.ToString();
    }
}
