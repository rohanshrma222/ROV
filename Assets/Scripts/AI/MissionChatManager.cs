using System;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stripped version of MarineDemoManager — Android TTS/Speech removed.
/// Provides text-based Gemini chat for the ROV mission co-pilot panel.
/// Wire UI references in Inspector, or call AskGemini(prompt) from code.
/// </summary>
public sealed class MissionChatManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] TMP_InputField promptInput;
    [SerializeField] TMP_Text       responseText;
    [SerializeField] TMP_Text       statusText;
    [SerializeField] Button         askButton;
    [SerializeField] TMP_Dropdown   languageDropdown;

    [Header("Gemini")]
    [SerializeField] bool             useGemini = true;
    [SerializeField] GeminiChatClient gemini    = new();
    [TextArea(2, 4)]
    [SerializeField] string geminiSystemPrompt =
        "You are NAVIGATOR, an expert ROV pilot AI assistant specialising in " +
        "marine biology, ocean sensor data interpretation, and underwater exploration. " +
        "Give concise, factual answers of 3-5 sentences by default. " +
        "When given mission data (depth, temperature, pH, biome, creatures), " +
        "analyse and report findings in a scientific field-notes style. " +
        "If the question is unrelated to oceanography or ROV operation, " +
        "politely redirect to those topics.";

    readonly CancellationTokenSource _destroyCts = new();
    string _currentLanguage = "en";

    void Start()
    {
        WireUi();
        if (responseText != null)
            responseText.text = "NAVIGATOR online. Ask me anything about the mission.";
        SetStatus("Ready");
    }

    void WireUi()
    {
        if (languageDropdown != null)
        {
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new System.Collections.Generic.List<string> { "English", "German" });
            languageDropdown.onValueChanged.AddListener(i => _currentLanguage = i == 1 ? "de" : "en");
        }

        if (askButton != null)
            askButton.onClick.AddListener(OnAskButtonClicked);

        if (promptInput != null)
            promptInput.onSubmit.AddListener(_ => OnAskButtonClicked());
    }

    void OnAskButtonClicked()
    {
        string prompt = promptInput != null ? promptInput.text.Trim() : "";
        if (string.IsNullOrWhiteSpace(prompt)) return;
        ProcessQueryAsync(prompt).Forget();
    }

    /// <summary>Ask Gemini with a pre-formed prompt (called by MissionReportGenerator).</summary>
    public UniTask<string> AskGemini(string prompt) => AskGeminiInternal(prompt);

    async UniTask ProcessQueryAsync(string rawPrompt)
    {
        if (string.IsNullOrWhiteSpace(rawPrompt)) return;
        string prompt = NormalizeMarineTerms(rawPrompt);
        if (promptInput != null) promptInput.text = "";

        if (useGemini && gemini.HasApiKey)
        {
            string response = await AskGeminiInternal(prompt);
            SetResponse(response);
        }
        else
        {
            SetResponse("Gemini API key not configured. Set GEMINI_API_KEY in your .env file.");
            SetStatus("No API key");
        }
    }

    async UniTask<string> AskGeminiInternal(string prompt)
    {
        SetStatus("Thinking…");
        try
        {
            string response = await gemini.GenerateResponseAsync(prompt, _currentLanguage, geminiSystemPrompt);
            SetStatus("Ready");
            return string.IsNullOrWhiteSpace(response) ? "No response from Gemini." : response.Trim();
        }
        catch (Exception ex)
        {
            SetStatus("Gemini error");
            Debug.LogError("[MissionChatManager] Gemini failed: " + ex.Message);
            return "Error: " + ex.Message;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    static string NormalizeMarineTerms(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return t;
        t = ReplacePhrase(t, "safe reports",   "cephalopods");
        t = ReplacePhrase(t, "safer pods",     "cephalopods");
        t = ReplacePhrase(t, "cephlopods",     "cephalopods");
        t = ReplacePhrase(t, "molluscs",       "mollusks");
        t = ReplacePhrase(t, "bio luminescence","bioluminescence");
        return t;
    }

    static string ReplacePhrase(string src, string from, string to) =>
        Regex.Replace(src, $@"\b{Regex.Escape(from)}\b", to,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    void SetStatus(string s)   { if (statusText   != null) statusText.text   = s; }
    void SetResponse(string s) { if (responseText != null) responseText.text = s; }

    void OnDestroy()
    {
        _destroyCts.Cancel();
        _destroyCts.Dispose();
    }
}
