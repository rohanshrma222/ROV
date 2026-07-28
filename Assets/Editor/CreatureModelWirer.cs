#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assigns each downloaded creature model (Assets/Models/Creatures/&lt;Name&gt;.glb) as the
/// modelPrefab on the matching CreatureData asset (Assets/Resources/Creatures/&lt;Name&gt;.asset),
/// matched by identical file name. Menu: ROV → Wire Creature Models
/// </summary>
public static class CreatureModelWirer
{
    const string ModelsDir = "Assets/Models/Creatures";
    const string DataDir   = "Assets/Resources/Creatures";

    [MenuItem("ROV/Wire Creature Models")]
    public static void WireModels()
    {
        int wired = 0, missing = 0;

        foreach (var dataGuid in AssetDatabase.FindAssets("t:CreatureData", new[] { DataDir }))
        {
            string dataPath = AssetDatabase.GUIDToAssetPath(dataGuid);
            var data = AssetDatabase.LoadAssetAtPath<CreatureData>(dataPath);
            string baseName = Path.GetFileNameWithoutExtension(dataPath);

            string modelPath = $"{ModelsDir}/{baseName}.glb";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning($"[CreatureModelWirer] No model at {modelPath} for '{data.creatureName}' — skipped.");
                missing++;
                continue;
            }

            data.modelPrefab = model;
            EditorUtility.SetDirty(data);
            wired++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CreatureModelWirer] Wired {wired} creature model(s), {missing} had no matching .glb.");
        EditorUtility.DisplayDialog("Creature Models Wired",
            $"Wired {wired} model(s) to their CreatureData assets.\n{missing} had no matching .glb file (expected — Leafy Sea Dragon / Vampire Squid have none yet).",
            "OK");
    }
}
#endif
