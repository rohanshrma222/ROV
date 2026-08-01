using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows a "research site found" -> scan progress -> species info card when the ROV
/// reaches a waypoint (ROVMissionController.OnWaypointReached), pausing ROV input
/// until the player scans and taps Continue. Builds its own Canvas at runtime (no
/// scene wiring needed, building its own Canvas the moment a mission controller exists) and
/// loads its creature-per-waypoint data from Resources/Creatures.
/// Replaces the old WaypointTriviaUI quiz interaction.
/// </summary>
public class SpeciesScanUI : MonoBehaviour
{
    const string BankResourcePath = "Creatures/WaypointCreatures";
    const float ScanDuration = 2.5f;

    static readonly Color CommonColor = new Color(0.2f, 0.55f, 0.55f, 1f);
    static readonly Color RareColor   = new Color(0.55f, 0.35f, 0.9f, 1f);

    public static int SpeciesDiscovered { get; private set; }
    public static int XPEarned { get; private set; }
    public static int GemsEarned { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureExists();
        EnsureExists();
    }

    static void EnsureExists()
    {
        if (FindFirstObjectByType<ROVMissionController>() == null) return;
        if (FindFirstObjectByType<SpeciesScanUI>() != null) return;
        new GameObject("SpeciesScanUI").AddComponent<SpeciesScanUI>();
    }

    ROVMissionController _missionController;
    ROVController        _rov;
    WaypointCreatureBank _bank;
    CreatureData         _currentCreature;
    GameObject           _spawnedModel;
    bool                 _scanning;
    float                _scanElapsed;

    // UI references (built at runtime in BuildUI)
    GameObject _root, _foundGroup, _scanningGroup, _cardGroup;
    TMP_Text   _scanPercentLabel, _cardNameLabel, _cardRarityLabel, _cardStatsLabel, _cardFactLabel;
    Image      _scanFillImage, _rarityBadgeImage;

    void Awake()
    {
        SpeciesDiscovered = 0;
        XPEarned = 0;
        GemsEarned = 0;

        _bank = Resources.Load<WaypointCreatureBank>(BankResourcePath);
        if (_bank == null)
            Debug.LogWarning($"[SpeciesScanUI] No WaypointCreatureBank found at Resources/{BankResourcePath}.");
        else
            Debug.Log($"[SpeciesScanUI] Loaded bank with {(_bank.creatures != null ? _bank.creatures.Length : 0)} creature(s).");

        _missionController = FindFirstObjectByType<ROVMissionController>();
        if (_missionController != null)
            _missionController.OnWaypointReached.AddListener(HandleWaypointReached);
        else
            Debug.LogWarning("[SpeciesScanUI] No ROVMissionController found — should never happen since Bootstrap only creates this when one exists.");

        BuildUI();
        _root.SetActive(false);
        Debug.Log("[SpeciesScanUI] Awake complete, listening for waypoints.");
    }

    void Update()
    {
        if (!_scanning) return;

        _scanElapsed += Time.deltaTime;
        float pct = Mathf.Clamp01(_scanElapsed / ScanDuration);
        if (_scanFillImage != null) _scanFillImage.fillAmount = pct;
        if (_scanPercentLabel != null) _scanPercentLabel.text = $"Analyzing species data...  {Mathf.RoundToInt(pct * 100f)}%";

        if (pct >= 1f)
        {
            _scanning = false;
            ShowCard();
        }
    }

    void HandleWaypointReached(int reached, int total)
    {
        Debug.Log($"[SpeciesScanUI] HandleWaypointReached({reached}/{total})");
        if (_bank == null || _bank.creatures == null) return;

        // Keyed by the waypoint's own fixed identity (waypointIndex), not arrival order —
        // so which creature you get depends on WHERE you are, not what order you visited
        // waypoints in. Waypoints can be reached in any order (requireSequentialOrder is
        // off for AR), so "reached - 1" would give a different creature at the same
        // physical spot depending on visit order.
        var wp = _missionController.LastReachedWaypoint;
        int index = wp != null ? wp.waypointIndex : reached - 1;
        if (index < 0 || index >= _bank.creatures.Length)
        {
            Debug.LogWarning($"[SpeciesScanUI] Index {index} out of range (bank has {_bank.creatures.Length}).");
            return;
        }

        _currentCreature = _bank.creatures[index];
        if (_currentCreature == null) return;
        Debug.Log($"[SpeciesScanUI] Showing '{_currentCreature.creatureName}' (rarity={_currentCreature.rarity}, modelPrefab={(_currentCreature.modelPrefab != null ? _currentCreature.modelPrefab.name : "NONE")}).");

        _rov = FindFirstObjectByType<ROVController>();
        if (_rov != null)
        {
            _rov.InputEnabled = false;
            _rov.StopMotion();
        }

        ShowFound();
    }

    void SpawnModel()
    {
        if (_currentCreature.modelPrefab == null) return;
        var waypoint = _missionController != null ? _missionController.LastReachedWaypoint : null;
        if (waypoint == null)
        {
            Debug.LogWarning("[SpeciesScanUI] SpawnModel: CurrentWaypoint is null, can't place model.");
            return;
        }

        // The creature is the reveal — hide the waypoint's placeholder marker the moment
        // the real model appears.
        var markerVisual = waypoint.transform.Find("Visual");
        if (markerVisual != null) markerVisual.gameObject.SetActive(false);

        _spawnedModel = Instantiate(_currentCreature.modelPrefab, waypoint.transform);
        _spawnedModel.transform.localPosition = _currentCreature.modelOffset;
        _spawnedModel.transform.localScale = Vector3.one * _currentCreature.modelScale;

        // Rigged models (e.g. Giant Sea Spider) use SkinnedMeshRenderers, whose bounds are
        // computed from the imported bind pose and can end up wrong/stale enough that Unity's
        // frustum culling decides they're off-screen and never draws them at all — a model
        // that's fully invisible for reasons that have nothing to do with position or scale.
        // Forcing bounds to recompute every frame sidesteps that.
        foreach (var skinned in _spawnedModel.GetComponentsInChildren<SkinnedMeshRenderer>())
            skinned.updateWhenOffscreen = true;

        if (AddInspectionCollider(_spawnedModel))
            _spawnedModel.AddComponent<CreatureModelRotator>();
        Debug.Log($"[SpeciesScanUI] Spawned '{_spawnedModel.name}' at waypoint '{waypoint.waypointLabel}', world pos {_spawnedModel.transform.position}, scale {_spawnedModel.transform.localScale}.");
    }

    /// <summary>
    /// Downloaded models don't come with a collider, so CreatureModelRotator has nothing to
    /// hit-test taps against. Builds a BoxCollider from the model's actual combined mesh
    /// bounds (in the model's own local space) so tapping anywhere on its visible surface works.
    /// Returns false (and skips adding the rotator) if the model has no renderers at all,
    /// since CreatureModelRotator requires a Collider to already be present.
    /// </summary>
    static bool AddInspectionCollider(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[SpeciesScanUI] '{model.name}' has no Renderer components — can't build an inspection collider (and likely isn't rendering anything either).");
            return false;
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        Vector3 lossyScale = model.transform.lossyScale;
        var box = model.AddComponent<BoxCollider>();
        box.center = model.transform.InverseTransformPoint(combined.center);
        box.size = new Vector3(
            combined.size.x / Mathf.Max(lossyScale.x, 0.0001f),
            combined.size.y / Mathf.Max(lossyScale.y, 0.0001f),
            combined.size.z / Mathf.Max(lossyScale.z, 0.0001f));
        Debug.Log($"[SpeciesScanUI] '{model.name}' combined world bounds: center={combined.center}, size={combined.size}.");
        return true;
    }

    void ShowFound()
    {
        _root.SetActive(true);
        _foundGroup.SetActive(true);
        _scanningGroup.SetActive(false);
        _cardGroup.SetActive(false);
    }

    void StartScan()
    {
        _scanning = true;
        _scanElapsed = 0f;
        if (_scanFillImage != null) _scanFillImage.fillAmount = 0f;
        _foundGroup.SetActive(false);
        _scanningGroup.SetActive(true);
        _cardGroup.SetActive(false);

        SpawnModel();
    }

    void ShowCard()
    {
        _foundGroup.SetActive(false);
        _scanningGroup.SetActive(false);
        _cardGroup.SetActive(true);

        bool rare = _currentCreature.rarity == CreatureData.Rarity.Rare;

        if (_cardNameLabel != null) _cardNameLabel.text = _currentCreature.creatureName;
        if (_cardRarityLabel != null) _cardRarityLabel.text = rare ? "RARE DISCOVERY" : "COMMON";
        if (_rarityBadgeImage != null) _rarityBadgeImage.color = rare ? RareColor : CommonColor;
        if (_cardStatsLabel != null)
            _cardStatsLabel.text =
                $"Depth        {_currentCreature.depthRange}\n" +
                $"Habitat      {_currentCreature.habitat}\n" +
                $"Diet         {_currentCreature.diet}\n" +
                $"Status       {_currentCreature.conservationStatus}";
        if (_cardFactLabel != null) _cardFactLabel.text = _currentCreature.interestingFact;
    }

    void Continue()
    {
        SpeciesDiscovered++;
        bool rare = _currentCreature != null && _currentCreature.rarity == CreatureData.Rarity.Rare;
        XPEarned += rare ? 250 : 100;
        if (rare) GemsEarned += 1;

        _root.SetActive(false);
        if (_rov != null) _rov.InputEnabled = true;
        // Deliberately not despawning _spawnedModel — once a species is logged, it stays
        // visible in the world as a permanent marker of the discovery.
        _spawnedModel = null;
        _currentCreature = null;
    }

    // ── UI construction ─────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO = new GameObject("SpeciesScanCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        _root = new GameObject("Root");
        _root.transform.SetParent(canvasGO.transform, false);
        var rootImg = _root.AddComponent<Image>();
        rootImg.color = new Color(0f, 0f, 0f, 0.55f);
        Stretch(_root.GetComponent<RectTransform>());

        var card = new GameObject("Card");
        card.transform.SetParent(_root.transform, false);
        card.AddComponent<Image>().color = new Color(0.03f, 0.09f, 0.14f, 0.97f);
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(700, 520);
        cardRT.anchoredPosition = Vector2.zero;

        _foundGroup = BuildFoundGroup(card.transform);
        _scanningGroup = BuildScanningGroup(card.transform);
        _cardGroup = BuildCardGroup(card.transform);
    }

    GameObject BuildFoundGroup(Transform parent)
    {
        var group = new GameObject("Found");
        group.transform.SetParent(parent, false);
        Stretch(group.AddComponent<RectTransform>());

        AddText(group.transform, "Title", "RESEARCH SITE FOUND", 32, TextAlignmentOptions.Center,
            new Vector2(0, 0.65f), new Vector2(1, 0.85f));
        AddText(group.transform, "Subtitle", "A rare marine species has been detected nearby.", 22,
            TextAlignmentOptions.Center, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f));

        var button = AddButton(group.transform, "ScanButton", "SCAN", new Vector2(0.3f, 0.12f), new Vector2(0.7f, 0.25f));
        button.onClick.AddListener(StartScan);

        return group;
    }

    GameObject BuildScanningGroup(Transform parent)
    {
        var group = new GameObject("Scanning");
        group.transform.SetParent(parent, false);
        Stretch(group.AddComponent<RectTransform>());

        var barBg = new GameObject("BarBackground");
        barBg.transform.SetParent(group.transform, false);
        barBg.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        var barBgRT = barBg.GetComponent<RectTransform>();
        barBgRT.anchorMin = new Vector2(0.1f, 0.45f);
        barBgRT.anchorMax = new Vector2(0.9f, 0.52f);
        barBgRT.offsetMin = barBgRT.offsetMax = Vector2.zero;

        var barFill = new GameObject("BarFill");
        barFill.transform.SetParent(barBg.transform, false);
        _scanFillImage = barFill.AddComponent<Image>();
        _scanFillImage.color = new Color(0.3f, 0.85f, 0.7f, 1f);
        _scanFillImage.type = Image.Type.Filled;
        _scanFillImage.fillMethod = Image.FillMethod.Horizontal;
        _scanFillImage.fillAmount = 0f;
        Stretch(barFill.GetComponent<RectTransform>());

        _scanPercentLabel = AddText(group.transform, "PercentLabel", "Analyzing species data...  0%", 20,
            TextAlignmentOptions.Center, new Vector2(0, 0.55f), new Vector2(1, 0.65f));

        return group;
    }

    GameObject BuildCardGroup(Transform parent)
    {
        var group = new GameObject("SpeciesCard");
        group.transform.SetParent(parent, false);
        Stretch(group.AddComponent<RectTransform>());

        _cardNameLabel = AddText(group.transform, "NameLabel", "", 30, TextAlignmentOptions.Center,
            new Vector2(0, 0.85f), new Vector2(1, 0.97f));

        var badge = new GameObject("RarityBadge");
        badge.transform.SetParent(group.transform, false);
        _rarityBadgeImage = badge.AddComponent<Image>();
        var badgeRT = badge.GetComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(0.35f, 0.76f);
        badgeRT.anchorMax = new Vector2(0.65f, 0.83f);
        badgeRT.offsetMin = badgeRT.offsetMax = Vector2.zero;
        _cardRarityLabel = AddText(badge.transform, "RarityLabel", "", 16, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one);

        _cardStatsLabel = AddText(group.transform, "StatsLabel", "", 20, TextAlignmentOptions.Left,
            new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.74f));
        _cardStatsLabel.enableWordWrapping = false;

        _cardFactLabel = AddText(group.transform, "FactLabel", "", 18, TextAlignmentOptions.TopLeft,
            new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.38f));
        _cardFactLabel.enableWordWrapping = true;

        var button = AddButton(group.transform, "ContinueButton", "CONTINUE", new Vector2(0.3f, 0.05f), new Vector2(0.7f, 0.15f));
        button.onClick.AddListener(Continue);

        return group;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TMP_Text AddText(Transform parent, string name, string text, float fontSize,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return tmp;
    }

    static Button AddButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.1f, 0.6f, 0.5f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var button = go.AddComponent<Button>();
        AddText(go.transform, "Label", label, 22, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        return button;
    }
}
