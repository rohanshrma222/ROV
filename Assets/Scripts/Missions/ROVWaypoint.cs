using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Component placed on each waypoint marker in the scene.
/// - Pulses its MeshRenderer material using the UnderwaterGlow shader's _GlowIntensity property.
/// - Fires OnROVEntered when the ROV's Rigidbody enters the trigger sphere.
/// Tag the ROV root GameObject with the tag "ROV" (or change rovTag below).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class ROVWaypoint : MonoBehaviour
{
    [Header("Identity")]
    public string waypointLabel = "Waypoint";
    public int    waypointIndex = 0;

    [Header("Trigger Settings")]
    [SerializeField] float triggerRadius = 3f;
    [SerializeField] string rovTag = "ROV";

    [Header("Glow Pulse")]
    [SerializeField] MeshRenderer glowRenderer;
    [SerializeField] float pulseSpeed = 2f;
    [SerializeField] float pulseMin   = 0.4f;
    [SerializeField] float pulseMax   = 1.8f;

    [Header("Events")]
    public UnityEvent<ROVWaypoint> OnROVEntered;

    SphereCollider _col;
    bool           _triggered;
    static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        _col          = GetComponent<SphereCollider>();
        _col.isTrigger = true;
        _col.radius    = triggerRadius;

        if (glowRenderer == null)
            glowRenderer = GetComponentInChildren<MeshRenderer>();
    }

    void Update()
    {
        if (_triggered || glowRenderer == null) return;
        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        float intensity = Mathf.Lerp(pulseMin, pulseMax, t);

        // Support both property-driven shaders
        var mat = glowRenderer.material;
        if (mat.HasProperty(GlowIntensityId))
            mat.SetFloat(GlowIntensityId, intensity);
        if (mat.HasProperty(EmissionColorId))
            mat.SetColor(EmissionColorId, Color.cyan * intensity);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag(rovTag) && other.GetComponentInParent<ROVController>() == null)
            return;

        _triggered = true;
        StopGlow();
        OnROVEntered?.Invoke(this);
        Debug.Log($"[ROVWaypoint] ROV reached '{waypointLabel}'");
    }

    void StopGlow()
    {
        if (glowRenderer == null) return;
        var mat = glowRenderer.material;
        if (mat.HasProperty(GlowIntensityId)) mat.SetFloat(GlowIntensityId, 0f);
        if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, Color.black);
    }

    /// <summary>Reset for mission restart.</summary>
    public void Reset()
    {
        _triggered = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
