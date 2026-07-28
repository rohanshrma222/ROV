using UnityEngine;

/// <summary>
/// Authored facts shown on the species card when a creature is scanned at a waypoint.
/// </summary>
[CreateAssetMenu(menuName = "ROV/Creature Data", fileName = "NewCreatureData")]
public class CreatureData : ScriptableObject
{
    public enum Rarity { Common, Rare }

    [Header("Identity")]
    public string creatureName;
    public Sprite portrait;
    public Rarity rarity = Rarity.Common;

    [Header("Field Notes")]
    public string depthRange;
    public string habitat;
    public string diet;
    public string conservationStatus;

    [TextArea(3, 6)]
    public string interestingFact;

    [Header("3D Model (AR world presence)")]
    [Tooltip("Imported model (e.g. a .glb under Assets/Models/Creatures) spawned at the waypoint when it's reached. Wire via Assets > Create > ROV > Creature Data, or the ROV > Wire Creature Models menu.")]
    public GameObject modelPrefab;
    [Tooltip("Local offset from the waypoint marker, for correcting off-centre model pivots.")]
    public Vector3 modelOffset = Vector3.zero;
    [Tooltip("Uniform scale correction — downloaded models are rarely authored at AR room-scale.")]
    public float modelScale = 1f;
}
