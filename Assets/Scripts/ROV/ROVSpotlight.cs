using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggleable spot-light attached as a child of the ROV.
/// Press F (keyboard) or call Toggle() from UI to switch on/off.
/// </summary>
public class ROVSpotlight : MonoBehaviour
{
    [SerializeField] Light spotLight;
    [SerializeField] float defaultIntensity = 3f;
    [SerializeField] Color lightColor = new Color(0.9f, 0.95f, 1f);

    void Awake()
    {
        if (spotLight == null) spotLight = GetComponentInChildren<Light>();
        if (spotLight != null)
        {
            spotLight.type      = LightType.Spot;
            spotLight.intensity = defaultIntensity;
            spotLight.color     = lightColor;
            spotLight.range     = 20f;
            spotLight.spotAngle = 40f;
            spotLight.enabled   = true;
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            Toggle();
    }

    /// <summary>Called by UI button or keyboard shortcut.</summary>
    public void Toggle()
    {
        if (spotLight != null)
            spotLight.enabled = !spotLight.enabled;
    }

    public bool IsOn => spotLight != null && spotLight.enabled;
}
