using UnityEngine;

/// <summary>
/// Attach to a creature GameObject (tagged "Actor", see World.GetCreatures) to give it
/// species card content. Assign the matching CreatureData asset in the Inspector.
/// </summary>
public class CreatureIdentity : MonoBehaviour
{
    public CreatureData data;
}
