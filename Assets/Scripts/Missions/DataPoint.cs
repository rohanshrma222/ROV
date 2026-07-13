using UnityEngine;

/// <summary>
/// Immutable snapshot of sensor data captured when the ROV arrives at a waypoint.
/// Serializable so Unity can display it in the Inspector and JsonUtility can encode it.
/// </summary>
[System.Serializable]
public struct DataPoint
{
    /// Name of the waypoint where this sample was taken.
    public string waypointName;
    /// World-space position of the ROV at capture time.
    public Vector3 position;
    /// Depth below water surface in metres.
    public float depth;
    /// Water temperature in °C (from TemperatureController or World estimate).
    public float temperature;
    /// Water pH (from TemperatureController or default 8.1).
    public float pH;
    /// Horizontal visibility in metres.
    public float visibility;
    /// Seconds since mission started at capture time.
    public float timestamp;
    /// Biome string from World.GetEnvironment().
    public string biome;
    /// Light level string (bright / dim / dark).
    public string lightLevel;
    /// Names of creatures detected within scan radius.
    public string[] creaturesNearby;
}
