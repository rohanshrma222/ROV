using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically configures CanvasScalers for mobile aspect ratios
/// and applies a Safe Area wrapper to prevent UI elements from overflowing
/// or clipping under device notches and rounded corners.
/// </summary>
[DefaultExecutionOrder(-100)]
public class MobileUIAdapter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // Automatically spawn the adapter in the scene if it's not already there
        var go = new GameObject("MobileUIAdapter");
        go.AddComponent<MobileUIAdapter>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyAdapterToActiveScene();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAdapterToActiveScene();
    }

    private void ApplyAdapterToActiveScene()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            // Only apply to screen space overlay/camera canvases (the UI)
            if (canvas.renderMode == RenderMode.WorldSpace) continue;

            ConfigureCanvasScaler(canvas);
            ApplySafeArea(canvas);
            StyleReportPanel(canvas);
        }
    }

    private void StyleReportPanel(Canvas canvas)
    {
        // Find ReportScreenUI even if it is inactive on startup
        var reportUI = canvas.GetComponentInChildren<ReportScreenUI>(true);
        if (reportUI == null) return;

        // 1. Style the main panel background
        var panelImage = reportUI.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0.05f, 0.10f, 0.15f, 0.90f); // Dark semi-transparent
        }

        // Shifting report panel 80 units downwards so it sits better on the screen
        var rectTrans = reportUI.GetComponent<RectTransform>();
        if (rectTrans != null)
        {
            rectTrans.anchoredPosition = new Vector2(rectTrans.anchoredPosition.x, rectTrans.anchoredPosition.y - 80f);
        }

        // 2. Clear white backgrounds from Scroll View components
        var images = reportUI.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.gameObject == reportUI.gameObject) continue;

            string nameLower = img.gameObject.name.ToLower();
            
            // Viewport, scroll view backgrounds, etc. should be transparent or dark
            if (nameLower.Contains("view") || nameLower.Contains("scroll") || nameLower.Contains("viewport") || nameLower.Contains("background"))
            {
                img.color = new Color(0.03f, 0.05f, 0.08f, 0.50f); // Sleek transparent dark inside scrollbox
            }
        }

        // 3. Make all texts high-contrast white/cyan
        var texts = reportUI.GetComponentsInChildren<TMPro.TMP_Text>(true);
        foreach (var txt in texts)
        {
            if (txt.gameObject.name.ToLower().Contains("title") || txt.gameObject.name.ToLower().Contains("header"))
            {
                txt.color = new Color(0.2f, 0.9f, 0.9f, 1f); // Vibrant Cyan for headers
            }
            else
            {
                txt.color = Color.white; // Readable white for body text
            }
        }

        // 4. Remove the Navigator Chat Panel as requested
        RemoveChatPanel(canvas.transform);
    }

    private void RemoveChatPanel(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == "ChatPanel")
            {
                Destroy(child.gameObject);
            }
            else
            {
                RemoveChatPanel(child);
            }
        }
    }

    private void ConfigureCanvasScaler(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        
        // For landscape mobile games, matching height or a 0.5 blend works best
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void ApplySafeArea(Canvas canvas)
    {
        // Don't apply multiple times to the same canvas
        if (canvas.transform.Find("SafeAreaContainer") != null) return;

        // Create the safe area container parent
        var safeAreaGO = new GameObject("SafeAreaContainer", typeof(RectTransform));
        safeAreaGO.transform.SetParent(canvas.transform, false);
        var safeRect = safeAreaGO.GetComponent<RectTransform>();

        // Set sibling index to 0 so backgrounds can still render behind it if needed,
        // but we'll move non-background children into it.
        safeAreaGO.transform.SetAsFirstSibling();

        // Calculate safe area dimensions relative to the screen size
        Rect safeArea = Screen.safeArea;
        
        // Apply anchors
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Constrain the safe area to add padding/margin to prevent screen edge overflow
        anchorMin.x = Mathf.Max(anchorMin.x, 0.04f); // 4% left margin
        anchorMax.x = Mathf.Min(anchorMax.x, 0.96f); // 4% right margin
        anchorMin.y = Mathf.Max(anchorMin.y, 0.02f); // 2% bottom margin
        anchorMax.y = Mathf.Min(anchorMax.y, 0.90f); // 10% top margin (pushed down from the top edge)

        safeRect.anchorMin = anchorMin;
        safeRect.anchorMax = anchorMax;
        safeRect.offsetMin = new Vector2(-40.00019f, -36f);
        safeRect.offsetMax = new Vector2(43.00001f, 193f);

        // Move all UI panel children into the safe area container, except full-screen backgrounds/water planes
        var children = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            children.Add(canvas.transform.GetChild(i));
        }

        foreach (var child in children)
        {
            if (child == safeAreaGO.transform) continue;

            string nameLower = child.name.ToLower();
            
            // Backgrounds, raw water planes, screen space effects should not be clipped by safe area
            if (nameLower.Contains("bg") || 
                nameLower.Contains("background") || 
                nameLower.Contains("water") || 
                nameLower.Contains("sky") ||
                nameLower.Contains("fill") ||
                nameLower.Contains("fade") ||
                nameLower.Contains("transition"))
            {
                continue;
            }

            // Move the gameplay UI panels into the safe area container
            child.SetParent(safeAreaGO.transform, false);
        }
    }
}
