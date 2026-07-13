#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the 3 default mission ScriptableObject presets in Assets/Resources/Missions/
/// Menu: ROV → Create Mission Presets
/// </summary>
public static class ROVMissionAssetCreator
{
    [MenuItem("ROV/Create Mission Presets")]
    public static void CreatePresets()
    {
        string dir = "Assets/Resources/Missions";
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        CreateMission(dir, "CoralHealthMission",
            "Coral Health Survey",
            "Assess coral bleaching and water chemistry across 3 reef stations. " +
            "Monitor temperature and pH for signs of ocean acidification.",
            ROVMissionUIData.MissionType.CoralHealth, 3, 15f);

        CreateMission(dir, "SpeciesTransectMission",
            "Species Transect",
            "Conduct a marine species count along a 100m transect. " +
            "Record all fauna observed within 5m of the survey line.",
            ROVMissionUIData.MissionType.SpeciesTransect, 3, 20f);

        CreateMission(dir, "WaterColumnMission",
            "Water Column Survey",
            "Measure temperature, visibility and salinity proxy data at " +
            "3 depth intervals from surface to seabed.",
            ROVMissionUIData.MissionType.WaterColumn, 3, 10f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ROVMissionAssetCreator] 3 mission presets created in " + dir);
        EditorUtility.DisplayDialog("Mission Presets Created",
            "3 mission presets created in Assets/Resources/Missions/\n\n" +
            "Assign them to MissionSelectorUI or MissionSceneBootstrap in the Inspector.", "OK");
    }

    static void CreateMission(string dir, string assetName, string missionName,
        string desc, ROVMissionUIData.MissionType type, int wpCount, float scanRadius)
    {
        string path = $"{dir}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ROVMissionUIData>(path);
        if (existing != null)
        {
            Debug.Log($"[ROVMissionAssetCreator] Already exists, skipping: {path}");
            return;
        }

        var data = ScriptableObject.CreateInstance<ROVMissionUIData>();
        data.missionName        = missionName;
        data.missionDescription = desc;
        data.missionType        = type;
        data.waypointCount      = wpCount;
        data.scanRadius         = scanRadius;

        AssetDatabase.CreateAsset(data, path);
    }
}
#endif
