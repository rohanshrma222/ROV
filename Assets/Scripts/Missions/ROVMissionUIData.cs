using UnityEngine;

/// <summary>
/// ScriptableObject that defines a mission preset.
/// Create via Assets → Create → ROV → Mission Data.
/// </summary>
[CreateAssetMenu(menuName = "ROV/Mission Data", fileName = "NewMission")]
public class ROVMissionUIData : ScriptableObject
{
    [Header("Display Info")]
    public string missionName        = "Coral Health Survey";
    [TextArea(2, 4)]
    public string missionDescription = "Assess coral bleaching and water chemistry across 5 reef stations.";
    public Sprite missionIcon;

    [Header("Mission Parameters")]
    public int   waypointCount = 5;
    public float scanRadius    = 15f;    // metres — radius for creature detection at each waypoint

    public MissionType missionType = MissionType.CoralHealth;

    public enum MissionType
    {
        CoralHealth,
        SpeciesTransect,
        WaterColumn
    }
}
