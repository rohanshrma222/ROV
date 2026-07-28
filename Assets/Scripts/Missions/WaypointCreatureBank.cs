using UnityEngine;

/// <summary>
/// ScriptableObject holding one authored CreatureData per waypoint, index-aligned to
/// waypoint order. Create via Assets → Create → ROV → Waypoint Creature Bank.
/// </summary>
[CreateAssetMenu(menuName = "ROV/Waypoint Creature Bank", fileName = "NewWaypointCreatureBank")]
public class WaypointCreatureBank : ScriptableObject
{
    public CreatureData[] creatures;
}
