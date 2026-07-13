#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ROV Scene Builder — one-click setup for the ROVGame scene.
/// Menu: ROV → Build ROVGame Scene
///
/// What it does:
///   1. Creates (or resets) the ROVGame scene
///   2. Places the CoralCanyons environment prefab + water plane
///   3. Builds the ROV capsule with all components wired
///   4. Adds a child Spotlight and two cameras (1st/3rd person)
///   5. Spawns 5 glowing waypoints arranged in an arc on the seabed
///   6. Creates TemperatureController, MissionController, ReportGenerator
///   7. Builds the full HUD canvas (depth, temp, pH, heading, biome, etc.)
///   8. Builds the Sonar radar panel
///   9. Builds the Mission HUD (waypoint counter + ON STATION overlay)
///  10. Builds the AI Chat panel + Report screen
///  11. Builds the on-screen joystick canvas
///  12. Wires all cross-references between components
///  13. Saves the scene and marks it dirty
/// </summary>
public static class ROVSceneBuilder
{
    // ── Paths ──────────────────────────────────────────────────────────────
    const string ScenePath       = "Assets/Scenes/ROVGame.unity";
    const string EnvPrefabPath   = "Assets/Prefabs/Environments/CoralCanyonsEnvironment.prefab";
    const string WaterPrefabPath = "Assets/Prefabs/Environments/Surface_Water_Plane.prefab";
    const string GlowMatPath     = "Assets/Materials/Underwater/GlowAnemone.mat";
    const string SkyboxMatPath   = "Assets/Materials/Underwater/UnderwaterSkybox.mat";
    const string JoystickPrefab  = "Assets/Joystick Pack/Prefabs/Fixed Joystick.prefab";

    // ── Colors ─────────────────────────────────────────────────────────────
    static readonly Color PanelBg      = new Color(0.04f, 0.12f, 0.18f, 0.85f);
    static readonly Color AccentCyan   = new Color(0.2f,  0.9f,  0.85f, 1f);
    static readonly Color TextGreen    = new Color(0.4f,  1f,    0.7f,  1f);
    static readonly Color DimText      = new Color(0.6f,  0.8f,  0.9f,  0.8f);
    static readonly Color WaypointGlow = new Color(0.1f,  0.9f,  1f,   1f);

    [MenuItem("ROV/Build ROVGame Scene %#r")]
    public static void BuildScene()
    {
        // ── 0. Open / create scene ─────────────────────────────────────────
        string dir = Path.GetDirectoryName(ScenePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 1. Rendering settings ──────────────────────────────────────────
        SetupRendering();

        // ── 2. Environment ─────────────────────────────────────────────────
        GameObject envRoot  = PlaceEnvironment();
        GameObject rovRoot  = BuildROV();
        GameObject wayRoot  = BuildWaypoints();
        GameObject msnCtrl  = BuildMissionController(wayRoot, rovRoot);
        GameObject hudCanvas = BuildHUDCanvas(rovRoot, msnCtrl);
        GameObject joyCanvas = BuildJoystickCanvas(rovRoot);
        GameObject chatCanvas = BuildChatCanvas(msnCtrl);

        // ── 3. EventSystem ─────────────────────────────────────────────────
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));

        // ── 4. Save ────────────────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        Debug.Log("[ROVSceneBuilder] ✅ ROVGame scene built and saved to " + ScenePath);
        EditorUtility.DisplayDialog("ROV Scene Builder",
            "✅ ROVGame scene built successfully!\n\n" +
            "Press PLAY in Unity to start.\n\n" +
            "Controls:\n" +
            "  W/A/S/D  — thrust\n" +
            "  Q/E      — ascend / descend\n" +
            "  RMB drag — yaw / pitch\n" +
            "  F        — spotlight toggle\n" +
            "  V        — camera switch\n" +
            "  Fly into glowing spheres to collect data!", "OK");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RENDERING
    // ═══════════════════════════════════════════════════════════════════════

    static void SetupRendering()
    {
        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
        RenderSettings.fogColor   = new Color(0.015f, 0.46f, 0.595f);
        RenderSettings.fogDensity = 0.055f;
        RenderSettings.ambientMode        = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.33f, 0.78f, 0.85f) * 0.8f;
        RenderSettings.ambientEquatorColor = new Color(0.015f, 0.46f, 0.595f) * 0.8f;
        RenderSettings.ambientGroundColor  = new Color(0.004f, 0.19f, 0.30f) * 0.8f;

        // Skybox
        var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatPath);
        if (skyboxMat != null) RenderSettings.skybox = skyboxMat;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENVIRONMENT
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject PlaceEnvironment()
    {
        var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPrefabPath);
        GameObject env = envPrefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(envPrefab)
            : new GameObject("CoralCanyonsEnvironment");
        env.name = "CoralCanyonsEnvironment";

        // Water surface
        var waterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPrefabPath);
        if (waterPrefab != null)
        {
            var water = (GameObject)PrefabUtility.InstantiatePrefab(waterPrefab);
            water.name = "Surface_Water_Plane";
            water.transform.position = new Vector3(0f, 8f, 0f);
        }

        // Directional light
        var lightGO = new GameObject("Directional Light");
        var light   = lightGO.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.color     = new Color(0.7f, 0.85f, 1f);
        light.intensity = 0.7f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Temperature controller
        var tcGO = new GameObject("EnvironmentController");
        var tc   = tcGO.AddComponent<TemperatureController>();
        tc.temperature = 26f;
        tc.pH          = 8.1f;

        return env;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROV
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject BuildROV()
    {
        // Root capsule
        var rov = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rov.name = "ROV";
        rov.tag  = EnsureTag("ROV");
        rov.transform.position = new Vector3(0f, 5f, 0f);
        rov.transform.localScale = new Vector3(0.6f, 0.4f, 1.2f);

        // Tint the capsule a dark-metal color
        var mr = rov.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.15f, 0.18f, 0.22f);
            mr.material = mat;
        }

        // Rigidbody
        var rb = rov.AddComponent<Rigidbody>();
        rb.useGravity             = false;
        rb.mass                   = 5f;
        rb.linearDamping          = 4f;
        rb.angularDamping         = 5f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // ROV scripts (no Joystick refs yet — wired after canvas built)
        rov.AddComponent<ROVController>();
        rov.AddComponent<ROVSpotlight>();
        var camRig = rov.AddComponent<ROVCameraRig>();

        // ── Spotlight child ──────────────────────────────────────────────
        var spotGO   = new GameObject("ROV_Spotlight");
        spotGO.transform.SetParent(rov.transform, false);
        spotGO.transform.localPosition = new Vector3(0f, 0f, 0.7f);
        var spotLight = spotGO.AddComponent<Light>();
        spotLight.type      = LightType.Spot;
        spotLight.intensity = 3f;
        spotLight.range     = 20f;
        spotLight.spotAngle = 40f;
        spotLight.color     = new Color(0.9f, 0.95f, 1f);

        // Wire ROVSpotlight → light
        var rovSpot = rov.GetComponent<ROVSpotlight>();
        SetPrivateField(rovSpot, "spotLight", spotLight);

        // ── Forward camera (1st person) ─────────────────────────────────
        var fwdCamGO = new GameObject("ForwardCam");
        fwdCamGO.transform.SetParent(rov.transform, false);
        fwdCamGO.transform.localPosition = new Vector3(0f, 0.15f, 0.55f);
        var fwdCam = fwdCamGO.AddComponent<Camera>();
        fwdCam.fieldOfView = 75f;
        fwdCam.nearClipPlane = 0.1f;
        fwdCam.farClipPlane  = 30f;
        fwdCamGO.tag = "MainCamera";

        // ── Chase camera (3rd person) ───────────────────────────────────
        var chaseCamGO = new GameObject("ChaseCam");
        chaseCamGO.transform.SetParent(rov.transform, false);
        chaseCamGO.transform.localPosition = new Vector3(0f, 3f, -8f);
        var chaseCam = chaseCamGO.AddComponent<Camera>();
        chaseCam.fieldOfView = 60f;
        chaseCam.nearClipPlane = 0.1f;
        chaseCam.farClipPlane  = 35f;
        chaseCamGO.SetActive(false);  // 1st person default

        // Wire ROVCameraRig
        SetPrivateField(camRig, "forwardCam", fwdCam);
        SetPrivateField(camRig, "chaseCam",   chaseCam);

        return rov;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WAYPOINTS
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject BuildWaypoints()
    {
        var glowMat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
        var root    = new GameObject("Waypoints");

        // 5 waypoints arranged in a rough arc around the ROV start pos
        var positions = new Vector3[]
        {
            new Vector3( 15f, 3f,  10f),
            new Vector3(-10f, 2f,  20f),
            new Vector3(  5f, 4f, -15f),
            new Vector3( 20f, 1f, -5f),
            new Vector3(-18f, 3f, -8f),
        };
        string[] labels = { "Station Alpha", "Station Bravo", "Station Charlie", "Station Delta", "Station Echo" };

        for (int i = 0; i < 5; i++)
        {
            var wp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wp.name = $"Waypoint_{i}";
            wp.transform.SetParent(root.transform);
            wp.transform.position   = positions[i];
            wp.transform.localScale = Vector3.one * 1.5f;

            // Remove default collider — ROVWaypoint adds its own trigger SphereCollider
            Object.DestroyImmediate(wp.GetComponent<SphereCollider>());

            // Glow material
            var mr = wp.GetComponent<MeshRenderer>();
            if (mr != null && glowMat != null)
            {
                mr.material = glowMat;
            }
            else if (mr != null)
            {
                // Fallback: emission
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_EmissionColor", WaypointGlow * 2f);
                mat.EnableKeyword("_EMISSION");
                mat.color = WaypointGlow;
                mr.material = mat;
            }

            // Point light for ambient glow
            var glowLight = new GameObject("GlowLight");
            glowLight.transform.SetParent(wp.transform, false);
            var pl = glowLight.AddComponent<Light>();
            pl.type      = LightType.Point;
            pl.color     = WaypointGlow;
            pl.intensity = 1.5f;
            pl.range     = 6f;

            // ROVWaypoint component
            var rwp = wp.AddComponent<ROVWaypoint>();
            rwp.waypointLabel = labels[i];
            rwp.waypointIndex = i;
            SetPrivateField(rwp, "glowRenderer", mr);
            SetPrivateField(rwp, "triggerRadius", 3f);
        }

        return root;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MISSION CONTROLLER
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject BuildMissionController(GameObject waypointRoot, GameObject rov)
    {
        var go  = new GameObject("MissionController");
        var mc  = go.AddComponent<ROVMissionController>();
        var rg  = go.AddComponent<MissionReportGenerator>();

        // Wire waypoints list
        var waypoints = new System.Collections.Generic.List<ROVWaypoint>();
        foreach (Transform child in waypointRoot.transform)
        {
            var rwp = child.GetComponent<ROVWaypoint>();
            if (rwp != null) waypoints.Add(rwp);
        }
        SetPrivateField(mc, "waypoints", waypoints);

        // Wire report generator
        SetPrivateField(mc, "reportGenerator", rg);

        return go;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HUD CANVAS
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject BuildHUDCanvas(GameObject rov, GameObject missionCtrl)
    {
        var canvas = MakeCanvas("HUDCanvas");

        // ── ROV HUD panel (top-left) ──────────────────────────────────────
        var hudPanel = MakePanel(canvas.transform, "ROVHUD_Panel",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20),
            new Vector2(260, 320));
        ApplyPanelStyle(hudPanel);

        var hud = hudPanel.AddComponent<ROVHUD>();

        // Create all HUD labels
        float y = -20f;
        var depthLbl   = MakeLabel(hudPanel.transform, "DepthLabel",   "DEPTH\n<size=130%><b>0.0</b></size> m",   new Vector2(0,y), 18, AccentCyan);   y -= 55f;
        var headingLbl = MakeLabel(hudPanel.transform, "HeadingLabel", "HDG  0°  N",                              new Vector2(0,y), 14, DimText);       y -= 30f;
        var biomeLbl   = MakeLabel(hudPanel.transform, "BiomeLabel",   "SUNLIT SHALLOWS",                        new Vector2(0,y), 12, AccentCyan);     y -= 30f;

        AddSeparator(hudPanel.transform, y); y -= 15f;

        var tempLbl    = MakeLabel(hudPanel.transform, "TempLabel",    "TEMP\n<size=130%><b>26.0</b></size> °C",  new Vector2(0,y), 18, TextGreen);     y -= 55f;
        var phLbl      = MakeLabel(hudPanel.transform, "pHLabel",      "pH  <b>8.10</b>",                        new Vector2(0,y), 14, TextGreen);      y -= 30f;
        var visLbl     = MakeLabel(hudPanel.transform, "VisLabel",     "VIS  22 m",                              new Vector2(0,y), 14, DimText);        y -= 30f;
        var lightLbl   = MakeLabel(hudPanel.transform, "LightLabel",   "LIGHT  BRIGHT",                          new Vector2(0,y), 14, DimText);        y -= 30f;

        AddSeparator(hudPanel.transform, y); y -= 15f;

        var statusLbl  = MakeLabel(hudPanel.transform, "StatusLabel",  "X 0  Y 0  Z 0",                          new Vector2(0,y), 10, DimText);

        // Wire ROVHUD fields
        SetPrivateField(hud, "depthLabel",      depthLbl);
        SetPrivateField(hud, "headingLabel",    headingLbl);
        SetPrivateField(hud, "biomeLabel",      biomeLbl);
        SetPrivateField(hud, "tempLabel",       tempLbl);
        SetPrivateField(hud, "phLabel",         phLbl);
        SetPrivateField(hud, "visibilityLabel", visLbl);
        SetPrivateField(hud, "lightLabel",      lightLbl);
        SetPrivateField(hud, "statusLabel",     statusLbl);

        // ── Sonar Panel (bottom-right) ────────────────────────────────────
        BuildSonarPanel(canvas.transform);

        // ── Mission HUD Panel (top-right) ─────────────────────────────────
        BuildMissionHUDPanel(canvas.transform, missionCtrl);

        return canvas;
    }

    static void BuildSonarPanel(Transform canvasT)
    {
        var panel = MakePanel(canvasT, "SonarPanel",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-100, 100),
            new Vector2(180, 180));
        ApplyPanelStyle(panel);

        // Circular background tint
        var img = panel.GetComponent<Image>();
        if (img != null) img.color = new Color(0.04f, 0.1f, 0.15f, 0.9f);

        // Sweep line (thin white rect, rotates from center)
        var sweepGO = new GameObject("SweepLine", typeof(RectTransform), typeof(Image));
        sweepGO.transform.SetParent(panel.transform, false);
        var sweepRT = sweepGO.GetComponent<RectTransform>();
        sweepRT.pivot       = new Vector2(0.5f, 0f);
        sweepRT.anchorMin   = new Vector2(0.5f, 0.5f);
        sweepRT.anchorMax   = new Vector2(0.5f, 0.5f);
        sweepRT.sizeDelta   = new Vector2(2f, 85f);
        sweepRT.anchoredPosition = Vector2.zero;
        sweepGO.GetComponent<Image>().color = new Color(0.3f, 1f, 0.7f, 0.6f);

        // Sonar label
        var sonarTitle = MakeLabel(panel.transform, "SonarTitle", "SONAR",
            new Vector2(0, 78f), 10, AccentCyan);

        // Creature count label
        var countLbl = MakeLabel(panel.transform, "CreatureCount", "clear",
            new Vector2(0, -78f), 10, TextGreen);

        // Add ROVSonar script to panel
        var sonar = panel.AddComponent<ROVSonar>();
        SetPrivateField(sonar, "radarPanel",         panel.GetComponent<RectTransform>());
        SetPrivateField(sonar, "sweepLine",          sweepRT);
        SetPrivateField(sonar, "radarUIRadius",      80f);
        SetPrivateField(sonar, "creatureCountLabel", countLbl);
    }

    static void BuildMissionHUDPanel(Transform canvasT, GameObject missionCtrl)
    {
        var panel = MakePanel(canvasT, "MissionHUDPanel",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20),
            new Vector2(280, 130));
        ApplyPanelStyle(panel);

        var missionName  = MakeLabel(panel.transform, "MissionNameLabel", "ROV SURVEY MISSION",
            new Vector2(0, -18), 13, AccentCyan);
        var wpCounter    = MakeLabel(panel.transform, "WaypointCounter", "WAYPOINT  0 / 5",
            new Vector2(0, -48), 16, TextGreen);
        var distLbl      = MakeLabel(panel.transform, "DistanceLabel", "NEXT  -- m",
            new Vector2(0, -80), 13, DimText);

        // ON STATION overlay
        var onStation = MakePanel(canvasT, "OnStationOverlay",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 80), new Vector2(360, 80));
        ApplyPanelStyle(onStation);
        onStation.GetComponent<Image>().color = new Color(0.1f, 0.6f, 0.4f, 0.9f);
        MakeLabel(onStation.transform, "OnStationText", "◉  ON STATION  —  COLLECTING DATA",
            Vector2.zero, 22, Color.white);
        onStation.SetActive(false);

        // MissionHUD component
        var mc = missionCtrl.GetComponent<ROVMissionController>();
        var mh = panel.AddComponent<MissionHUD>();
        SetPrivateField(mh, "missionNameLabel",    missionName);
        SetPrivateField(mh, "waypointCounterLabel", wpCounter);
        SetPrivateField(mh, "distanceLabel",       distLbl);
        SetPrivateField(mh, "onStationOverlay",    onStation);
        SetPrivateField(mh, "missionController",   mc);

        // Wire back to mission controller
        SetPrivateField(mc, "missionHUD", mh);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // JOYSTICK CANVAS
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject BuildJoystickCanvas(GameObject rov)
    {
        var canvas = MakeCanvas("JoystickCanvas");

        var joyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(JoystickPrefab);

        Joystick leftJoy  = null;
        Joystick rightJoy = null;

        if (joyPrefab != null)
        {
            // Left joystick (move)
            var leftGO = (GameObject)PrefabUtility.InstantiatePrefab(joyPrefab, canvas.transform);
            leftGO.name = "LeftJoystick";
            var leftRT = leftGO.GetComponent<RectTransform>();
            leftRT.anchorMin = leftRT.anchorMax = new Vector2(0, 0);
            leftRT.anchoredPosition = new Vector2(130, 130);
            leftRT.sizeDelta = new Vector2(200, 200);
            leftJoy = leftGO.GetComponent<Joystick>();

            // Right joystick (look)
            var rightGO = (GameObject)PrefabUtility.InstantiatePrefab(joyPrefab, canvas.transform);
            rightGO.name = "RightJoystick";
            var rightRT = rightGO.GetComponent<RectTransform>();
            rightRT.anchorMin = rightRT.anchorMax = new Vector2(1, 0);
            rightRT.anchoredPosition = new Vector2(-130, 130);
            rightRT.sizeDelta = new Vector2(200, 200);
            rightJoy = rightGO.GetComponent<Joystick>();
        }
        else
        {
            Debug.LogWarning("[ROVSceneBuilder] Joystick Pack prefab not found at: " + JoystickPrefab +
                             " — joysticks skipped. Keyboard still works.");
        }

        // Wire joysticks to ROVController
        var controller = rov.GetComponent<ROVController>();
        if (controller != null)
        {
            SetPrivateField(controller, "moveJoystick", leftJoy);
            SetPrivateField(controller, "lookJoystick", rightJoy);
        }

        return canvas;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHAT + REPORT CANVAS
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject BuildChatCanvas(GameObject missionCtrl)
    {
        var canvas = MakeCanvas("MissionChatCanvas");

        // ── Chat Panel (bottom center — collapsed by default) ─────────────
        var chatPanel = MakePanel(canvas.transform, "ChatPanel",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 160),
            new Vector2(600, 280));
        ApplyPanelStyle(chatPanel);

        var statusTmp = MakeLabel(chatPanel.transform, "StatusText", "NAVIGATOR  online",
            new Vector2(0, -18), 11, AccentCyan);

        var responseTmp = MakeLabel(chatPanel.transform, "ResponseText",
            "NAVIGATOR online. Ask me anything about the mission.",
            new Vector2(0, -80), 13, TextGreen);
        responseTmp.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 120);

        // Input field
        var inputGO = new GameObject("PromptInput", typeof(RectTransform));
        inputGO.transform.SetParent(chatPanel.transform, false);
        var inputRT = inputGO.GetComponent<RectTransform>();
        inputRT.anchorMin = inputRT.anchorMax = new Vector2(0.5f, 0f);
        inputRT.anchoredPosition = new Vector2(-60, -120);
        inputRT.sizeDelta = new Vector2(430, 36);
        var inputField = inputGO.AddComponent<TMP_InputField>();
        // Background for input
        var inputBg = inputGO.AddComponent<Image>();
        inputBg.color = new Color(0.06f, 0.16f, 0.22f, 1f);
        inputField.targetGraphic = inputBg;

        // Ask button
        var askBtn = MakeButton(chatPanel.transform, "AskButton", "ASK",
            new Vector2(220, -120), new Vector2(90, 36), AccentCyan);

        // MissionChatManager
        var chatMgr = chatPanel.AddComponent<MissionChatManager>();
        SetPrivateField(chatMgr, "statusText",   statusTmp);
        SetPrivateField(chatMgr, "responseText", responseTmp);
        SetPrivateField(chatMgr, "promptInput",  inputField);
        SetPrivateField(chatMgr, "askButton",    askBtn);

        // ── Report Panel (hidden, activates on mission complete) ──────────
        var reportPanel = MakePanel(canvas.transform, "ReportPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(700, 500));
        ApplyPanelStyle(reportPanel);
        reportPanel.GetComponent<Image>().color = new Color(0.03f, 0.09f, 0.14f, 0.97f);

        var titleLbl    = MakeLabel(reportPanel.transform, "ReportTitle", "ROV SURVEY — MISSION REPORT",
            new Vector2(0, 220), 18, AccentCyan);
        var summaryLbl  = MakeLabel(reportPanel.transform, "DataSummary", "5 waypoints surveyed",
            new Vector2(0, 188), 12, DimText);

        // Scrollable report text
        var scrollGO = new GameObject("ReportScrollView", typeof(RectTransform));
        scrollGO.transform.SetParent(reportPanel.transform, false);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = scrollRT.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRT.anchoredPosition = new Vector2(0, -40);
        scrollRT.sizeDelta = new Vector2(650, 320);
        var scroll = scrollGO.AddComponent<ScrollRect>();
        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0.04f, 0.1f, 0.15f, 0.6f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGO.transform, false);
        var vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        scroll.viewport = vpRT;

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewport.transform, false);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;
        var reportTmp = contentGO.AddComponent<TextMeshProUGUI>();
        reportTmp.fontSize = 13;
        reportTmp.color    = TextGreen;
        reportTmp.enableWordWrapping = true;
        reportTmp.text = "Awaiting mission completion…";
        scroll.content = contentRT;

        var copyBtn  = MakeButton(reportPanel.transform, "CopyButton",  "Copy Report",  new Vector2(-80, -225), new Vector2(160, 36), AccentCyan);
        var closeBtn = MakeButton(reportPanel.transform, "CloseButton", "Close",         new Vector2( 90, -225), new Vector2(100, 36), new Color(0.8f, 0.3f, 0.3f));

        reportPanel.SetActive(false);

        // ReportScreenUI
        var mc  = missionCtrl.GetComponent<ROVMissionController>();
        var rg  = missionCtrl.GetComponent<MissionReportGenerator>();
        var rui = reportPanel.AddComponent<ReportScreenUI>();
        SetPrivateField(rui, "reportText",            reportTmp);
        SetPrivateField(rui, "scrollRect",            scroll);
        SetPrivateField(rui, "missionTitleLabel",     titleLbl);
        SetPrivateField(rui, "dataPointSummaryLabel", summaryLbl);
        SetPrivateField(rui, "copyButton",            copyBtn);
        SetPrivateField(rui, "closeButton",           closeBtn);
        SetPrivateField(rui, "reportPanel",           reportPanel);
        SetPrivateField(rui, "missionController",     mc);

        // Wire report generator output label
        SetPrivateField(rg, "reportOutputLabel", reportTmp);
        SetPrivateField(rg, "chatManager", chatPanel.GetComponent<MissionChatManager>());

        return canvas;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UI HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject MakeCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 10;
        var cs = go.GetComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        return go;
    }

    static GameObject MakePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        go.GetComponent<Image>().color = PanelBg;
        return go;
    }

    static void ApplyPanelStyle(GameObject panel)
    {
        var img = panel.GetComponent<Image>();
        if (img != null) img.color = PanelBg;
    }

    static TextMeshProUGUI MakeLabel(Transform parent, string name, string text,
        Vector2 anchoredPos, float fontSize = 14, Color? color = null)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(240, 50);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text     = text;
        tmp.fontSize = fontSize;
        tmp.color    = color ?? TextGreen;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    static Button MakeButton(Transform parent, string name, string label,
        Vector2 pos, Vector2 size, Color tint)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = tint * 0.6f;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var textGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(go.transform, false);
        var tRT = textGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 13;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    static void AddSeparator(Transform parent, float y)
    {
        var go  = new GameObject("Separator", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 1f);
        rt.anchorMax = new Vector2(0.9f, 1f);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, 1);
        go.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.7f, 0.4f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REFLECTION HELPER
    // ═══════════════════════════════════════════════════════════════════════

    static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        var type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        Debug.LogWarning($"[ROVSceneBuilder] Field '{fieldName}' not found on {target.GetType().Name}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAG HELPER
    // ═══════════════════════════════════════════════════════════════════════

    static string EnsureTag(string tag)
    {
        // Attempt to add the tag if it doesn't exist
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                return tag;

        // Not found — add it
        int idx = tagsProp.arraySize;
        tagsProp.InsertArrayElementAtIndex(idx);
        tagsProp.GetArrayElementAtIndex(idx).stringValue = tag;
        tagManager.ApplyModifiedProperties();
        return tag;
    }
}
#endif
