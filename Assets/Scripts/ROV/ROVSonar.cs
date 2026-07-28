using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Radar-sweep mini-map sonar. 
/// Queries World.GetCreatures() and places coloured blip icons on a circular UI panel.
/// The sweep line rotates continuously. Blips fade out after a sweep passes them.
/// </summary>
public class ROVSonar : MonoBehaviour
{
    [Header("Sonar UI References")]
    [SerializeField] RectTransform radarPanel;       // circular background
    [SerializeField] RectTransform sweepLine;        // rotates to show sweep
    [SerializeField] GameObject    blipPrefab;       // small Image circle prefab

    [Header("Sonar Settings")]
    [SerializeField] float sweepSpeed     = 90f;     // degrees per second
    [SerializeField] float scanRadius     = 20f;     // world metres = edge of radar
    [SerializeField] float radarUIRadius  = 80f;     // pixels from center = edge of UI
    [SerializeField] float blipFadeTime   = 3f;      // seconds blip stays visible after sweep

    [Header("Blip Colors")]
    [SerializeField] Color fishColor    = new Color(0.3f, 1f, 0.6f, 1f);
    [SerializeField] Color sharkColor   = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] Color defaultColor = new Color(0.6f, 0.9f, 1f, 1f);

    [Header("Waypoint Blip")]
    [Tooltip("Distinct, non-fading marker showing the direction/distance to the mission's current waypoint, so the player navigates via the sonar instead of on-screen next-waypoint text.")]
    [SerializeField] Color waypointColor = new Color(1f, 0.65f, 0.15f, 1f);
    [SerializeField] Vector2 waypointBlipSize = new Vector2(14f, 14f);

    [Header("Creature Count Label")]
    [SerializeField] TMP_Text creatureCountLabel;

    float _sweepAngle;
    readonly List<BlipEntry> _blips = new();
    readonly Queue<GameObject> _pool = new();

    ROVMissionController _missionController;
    GameObject            _waypointBlip;

    struct BlipEntry
    {
        public string name;
        public Vector2 uiPos;      // position in radar panel
        public float   hitTime;    // Time.time when the sweep last hit this creature
        public GameObject go;
    }

    void Update()
    {
        UpdateSweep();
        UpdateBlips();
        UpdateWaypointBlip();
    }

    void UpdateSweep()
    {
        _sweepAngle = (_sweepAngle + sweepSpeed * Time.deltaTime) % 360f;
        if (sweepLine != null)
            sweepLine.localEulerAngles = new Vector3(0f, 0f, -_sweepAngle);

        // Sample creatures once per full sweep (every ~4 s at 90°/s)
        if (Time.frameCount % 15 == 0)
            SampleCreatures();
    }

    void SampleCreatures()
    {
        var creatures = World.GetCreatures(scanRadius);
        if (creatureCountLabel != null)
            creatureCountLabel.text = creatures.Count > 0 ? $"{creatures.Count} nearby" : "clear";

        var cam = Camera.main;
        if (cam == null) return;

        foreach (var c in creatures)
        {
            // Find if already tracked
            bool found = false;
            for (int i = 0; i < _blips.Count; i++)
            {
                if (_blips[i].name == c.name)
                {
                    var b = _blips[i];
                    b.uiPos   = WorldDirToRadarPos(c.direction, c.distanceMeters);
                    b.hitTime = Time.time;
                    _blips[i] = b;
                    found = true;
                    break;
                }
            }
            if (!found)
                AddBlip(c);
        }
    }

    void AddBlip(World.CreatureInfo c)
    {
        GameObject go = GetPooled();
        var entry = new BlipEntry
        {
            name    = c.name,
            uiPos   = WorldDirToRadarPos(c.direction, c.distanceMeters),
            hitTime = Time.time,
            go      = go,
        };
        // Color by creature type
        var img = go.GetComponent<Image>();
        if (img != null)
            img.color = c.name.ToLower().Contains("shark") ? sharkColor
                      : c.name.ToLower().Contains("fish")  ? fishColor
                      : defaultColor;
        _blips.Add(entry);
    }

    void UpdateBlips()
    {
        for (int i = _blips.Count - 1; i >= 0; i--)
        {
            var b = _blips[i];
            float age = Time.time - b.hitTime;
            if (age > blipFadeTime)
            {
                ReturnToPool(b.go);
                _blips.RemoveAt(i);
                continue;
            }
            // Position + fade
            if (b.go != null)
            {
                b.go.GetComponent<RectTransform>().anchoredPosition = b.uiPos;
                float alpha = Mathf.Clamp01(1f - age / blipFadeTime);
                var img = b.go.GetComponent<Image>();
                if (img != null)
                {
                    var col = img.color;
                    col.a = alpha;
                    img.color = col;
                }
            }
        }
    }

    // ── Waypoint blip ───────────────────────────────────────────────────────

    void UpdateWaypointBlip()
    {
        if (_missionController == null)
            _missionController = FindFirstObjectByType<ROVMissionController>();

        var wp = _missionController != null ? _missionController.CurrentWaypoint : null;
        var cam = Camera.main;
        if (wp == null || cam == null)
        {
            if (_waypointBlip != null) _waypointBlip.SetActive(false);
            return;
        }

        Vector3 toWaypoint = wp.transform.position - cam.transform.position;
        float distance = toWaypoint.magnitude;

        Vector3 flatForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 flatTarget  = Vector3.ProjectOnPlane(toWaypoint, Vector3.up).normalized;
        float angle = Vector3.SignedAngle(flatForward, flatTarget, Vector3.up);

        float rad     = angle * Mathf.Deg2Rad;
        float normDst = Mathf.Clamp01(distance / scanRadius);
        float r       = normDst * radarUIRadius;

        if (_waypointBlip == null) _waypointBlip = CreateWaypointBlip();
        _waypointBlip.SetActive(true);
        _waypointBlip.GetComponent<RectTransform>().anchoredPosition =
            new Vector2(Mathf.Sin(rad) * r, Mathf.Cos(rad) * r);
    }

    GameObject CreateWaypointBlip()
    {
        var g = new GameObject("WaypointBlip", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(radarPanel, false);
        g.GetComponent<RectTransform>().sizeDelta = waypointBlipSize;
        g.GetComponent<Image>().color = waypointColor;
        return g;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    Vector2 WorldDirToRadarPos(string direction, float distanceMeters)
    {
        // Convert direction string to approximate angle
        float angle = direction switch
        {
            "ahead"        =>   0f,
            "ahead-right"  =>  45f,
            "right"        =>  90f,
            "behind-right" => 135f,
            "behind"       => 180f,
            "behind-left"  => 225f,
            "left"         => 270f,
            "ahead-left"   => 315f,
            _              =>   0f,
        };
        float rad     = angle * Mathf.Deg2Rad;
        float normDst = Mathf.Clamp01(distanceMeters / scanRadius);
        float r       = normDst * radarUIRadius;
        return new Vector2(Mathf.Sin(rad) * r, Mathf.Cos(rad) * r);
    }

    GameObject GetPooled()
    {
        if (_pool.Count > 0)
        {
            var g = _pool.Dequeue();
            g.SetActive(true);
            return g;
        }
        if (blipPrefab == null)
        {
            // Create a minimal blip if no prefab assigned
            var g = new GameObject("Blip", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(radarPanel, false);
            g.GetComponent<RectTransform>().sizeDelta = new Vector2(8f, 8f);
            return g;
        }
        return Instantiate(blipPrefab, radarPanel);
    }

    void ReturnToPool(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        _pool.Enqueue(go);
    }
}
