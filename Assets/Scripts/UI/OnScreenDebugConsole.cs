using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Temporary on-device debug aid: overlays the last N Debug.Log lines at the top of the
/// screen so behaviour can be inspected on a phone without adb/Xcode. Builds its own
/// Canvas at runtime (no scene wiring needed) and survives scene loads. Safe to delete
/// once no longer needed.
/// </summary>
public class OnScreenDebugConsole : MonoBehaviour
{
    const int MaxLines = 20;

    static OnScreenDebugConsole _instance;
    TMP_Text _text;
    readonly List<string> _lines = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("OnScreenDebugConsole");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<OnScreenDebugConsole>();
    }

    void Awake()
    {
        BuildUI();
        Application.logMessageReceived += HandleLog;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    string _lastRaw;
    int _repeatCount;

    void HandleLog(string message, string stackTrace, LogType type)
    {
        string color = type == LogType.Error || type == LogType.Exception ? "#ff6b6b"
                      : type == LogType.Warning ? "#ffd166"
                      : "#e6f7ff";

        // For exceptions/errors, append the first real stack frame (the throwing method)
        // so the on-screen line says *where* it happened, not just what.
        string suffix = "";
        if ((type == LogType.Exception || type == LogType.Error) && !string.IsNullOrEmpty(stackTrace))
        {
            string firstFrame = stackTrace.Split('\n')[0].Trim();
            if (firstFrame.Length > 0) suffix = $"  <color=#999999>@ {firstFrame}</color>";
        }

        string raw = message + suffix;

        // Collapse consecutive repeats (e.g. a per-frame exception) instead of letting one
        // message flood the whole buffer and push everything else off screen.
        if (raw == _lastRaw && _lines.Count > 0)
        {
            _repeatCount++;
            _lines[_lines.Count - 1] = $"<color={color}>{raw}</color> <color=#999999>(x{_repeatCount})</color>";
        }
        else
        {
            _lastRaw = raw;
            _repeatCount = 1;
            _lines.Add($"<color={color}>{raw}</color>");
            if (_lines.Count > MaxLines) _lines.RemoveAt(0);
        }

        if (_text != null) _text.text = string.Join("\n", _lines);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("DebugConsoleCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();

        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var img = panelGO.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);
        img.raycastTarget = false; // let touches reach the joysticks underneath
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0.65f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        var textGO = new GameObject("LogText");
        textGO.transform.SetParent(panelGO.transform, false);
        _text = textGO.AddComponent<TextMeshProUGUI>();
        _text.fontSize = 20;
        _text.color = Color.white;
        _text.raycastTarget = false;
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10, 10);
        textRT.offsetMax = new Vector2(-10, -10);
    }
}
