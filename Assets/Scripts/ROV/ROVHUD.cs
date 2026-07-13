using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ROV Heads-Up Display — reads World sensor data every frame and
/// updates TMP text labels for depth, temperature, pH, heading,
/// visibility, and biome.
/// Wire each label in the Inspector or let it auto-find by name.
/// </summary>
public class ROVHUD : MonoBehaviour
{
    [Header("Depth & Navigation")]
    [SerializeField] TMP_Text depthLabel;
    [SerializeField] TMP_Text headingLabel;
    [SerializeField] TMP_Text biomeLabel;

    [Header("Environmental Sensors")]
    [SerializeField] TMP_Text tempLabel;
    [SerializeField] TMP_Text phLabel;
    [SerializeField] TMP_Text visibilityLabel;
    [SerializeField] TMP_Text lightLabel;

    [Header("Status Bar")]
    [SerializeField] TMP_Text statusLabel;

    [Header("Depth Gauge Bar (optional)")]
    [SerializeField] Image depthBarFill;
    [SerializeField] float maxDisplayDepth = 60f;

    [Header("Alert Thresholds")]
    [SerializeField] float tempWarningHigh = 30f;
    [SerializeField] float tempWarningLow  = 10f;
    [SerializeField] float phWarningLow    = 7.7f;
    [SerializeField] Color normalColor  = new Color(0.78f, 1f, 0.95f);
    [SerializeField] Color warningColor = new Color(1f, 0.6f, 0.3f);
    [SerializeField] Color dangerColor  = new Color(1f, 0.2f, 0.2f);

    void Update()
    {
        RefreshHUD();
    }

    void RefreshHUD()
    {
        var player = World.GetPlayer();
        var env    = World.GetEnvironment();

        // ── Depth ──────────────────────────────────────────────────────────
        SetText(depthLabel, $"DEPTH\n<size=130%><b>{player.depthMeters:F1}</b></size> m");
        if (depthBarFill != null)
            depthBarFill.fillAmount = Mathf.Clamp01(player.depthMeters / maxDisplayDepth);

        // ── Heading ────────────────────────────────────────────────────────
        SetText(headingLabel, $"HDG  {player.headingDegrees:F0}°  {player.headingCompass}");

        // ── Biome ──────────────────────────────────────────────────────────
        SetText(biomeLabel, env.biome.ToUpper());

        // ── Temperature ────────────────────────────────────────────────────
        Color tCol = env.waterTemperatureC > tempWarningHigh ? dangerColor
                   : env.waterTemperatureC < tempWarningLow  ? warningColor
                   : normalColor;
        SetText(tempLabel, $"TEMP\n<size=130%><b>{env.waterTemperatureC:F1}</b></size> °C", tCol);

        // ── pH ─────────────────────────────────────────────────────────────
        Color pCol = env.pH < phWarningLow ? dangerColor : normalColor;
        SetText(phLabel, $"pH  <b>{env.pH:F2}</b>", pCol);

        // ── Visibility ─────────────────────────────────────────────────────
        SetText(visibilityLabel, $"VIS  <b>{env.visibilityMeters:F0}</b> m");

        // ── Light level ────────────────────────────────────────────────────
        SetText(lightLabel, $"LIGHT  {env.lightLevel.ToUpper()}");

        // ── Status ─────────────────────────────────────────────────────────
        string sf = env.seafloorDistanceMeters >= 0f
            ? $"  |  SEABED  {env.seafloorDistanceMeters:F1}m"
            : "";
        SetText(statusLabel, $"X {player.x:F1}  Y {player.y:F1}  Z {player.z:F1}{sf}");
    }

    static void SetText(TMP_Text label, string text, Color? color = null)
    {
        if (label == null) return;
        label.text = text;
        if (color.HasValue) label.color = color.Value;
    }
}
