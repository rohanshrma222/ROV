#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ROV Model Setup — one-click tool to:
///   1. Create two URP Lit materials from the PBR textures (MainBody1 + MainBody2)
///   2. Build an ROV prefab from the imported OBJ mesh
///   3. Swap the placeholder capsule in the active scene with the real model
///
/// Menu: ROV → Setup ROV Model
/// 
/// Run this AFTER Unity finishes importing Assets/Models/ROV/ROVTex.obj
/// (wait until the spinning progress bar in Unity's bottom-right disappears).
/// </summary>
public static class ROVModelSetup
{
    // ── Asset paths ────────────────────────────────────────────────────────
    const string ModelPath   = "Assets/Models/ROV/ROVTex.obj";
    const string TexDir      = "Assets/Models/ROV/Textures";
    const string MatDir      = "Assets/Models/ROV/Materials";
    const string PrefabPath  = "Assets/Prefabs/ROV/ROV_Model.prefab";

    // ── Texture file names (relative to TexDir) ───────────────────────────
    const string B1_Base     = "ROVbot_low_MainBody1_BaseColor.jpg";
    const string B1_Normal   = "ROVbot_low_MainBody1_Normal.jpg";
    const string B1_Metallic = "ROVbot_low_MainBody1_Metallic.jpg";
    const string B1_Rough    = "ROVbot_low_MainBody1_Roughness.jpg";
    const string B1_Emit     = "ROVbot_low_MainBody1_Emissive.jpg";

    const string B2_Base     = "ROVbot_low_MainBody2_BaseColor.jpg";
    const string B2_Normal   = "ROVbot_low_MainBody2_Normal.jpg";
    const string B2_Metallic = "ROVbot_low_MainBody2_Metallic.jpg";
    const string B2_Rough    = "ROVbot_low_MainBody2_Roughness.jpg";
    const string B2_Emit     = "ROVbot_low_MainBody2_Emissive.jpg";

    [MenuItem("ROV/Setup ROV Model %#m")]
    public static void SetupModel()
    {
        // ── 0. Verify model imported ──────────────────────────────────────
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null)
        {
            EditorUtility.DisplayDialog("ROV Model Setup",
                "⚠️  Model not found at:\n" + ModelPath +
                "\n\nMake sure Unity has finished importing the .obj file " +
                "(no spinning progress bar in bottom-right corner).", "OK");
            return;
        }

        // ── 1. Create materials directory ─────────────────────────────────
        if (!System.IO.Directory.Exists(MatDir))
            System.IO.Directory.CreateDirectory(MatDir);

        // ── 2. Build materials ────────────────────────────────────────────
        var mat1 = BuildURPMaterial("ROV_MainBody1",
            B1_Base, B1_Normal, B1_Metallic, B1_Rough, B1_Emit,
            new Color(1f, 1f, 1f, 1f));

        var mat2 = BuildURPMaterial("ROV_MainBody2",
            B2_Base, B2_Normal, B2_Metallic, B2_Rough, B2_Emit,
            new Color(0.9f, 0.9f, 1f, 1f));

        AssetDatabase.SaveAssets();

        // ── 3. Instantiate OBJ into scene ─────────────────────────────────
        var rovInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        rovInstance.name = "ROV_ModelInstance";

        // Apply materials to all MeshRenderers
        var renderers = rovInstance.GetComponentsInChildren<MeshRenderer>(true);
        Debug.Log($"[ROVModelSetup] Found {renderers.Length} MeshRenderer(s) on model.");

        foreach (var mr in renderers)
        {
            string goName = mr.gameObject.name.ToLowerInvariant();
            // Assign material by submesh name heuristic
            var mats = new Material[mr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                string origName = mr.sharedMaterials[i] != null
                    ? mr.sharedMaterials[i].name.ToLowerInvariant() : "";
                bool isBody2 = origName.Contains("2") || goName.Contains("2");
                mats[i] = isBody2 ? mat2 : mat1;
            }
            mr.sharedMaterials = mats;
        }

        // ── 4. Normalise scale (OBJ importers vary wildly) ────────────────
        // Sample the bounding box and scale to ~1.5m long
        var bounds = CalculateBounds(rovInstance);
        float longestAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longestAxis > 0.001f)
        {
            float targetSize = 1.5f;
            float scale = targetSize / longestAxis;
            rovInstance.transform.localScale = Vector3.one * scale;
            Debug.Log($"[ROVModelSetup] Auto-scaled ROV by {scale:F4} (original longest axis = {longestAxis:F3}m)");
        }

        // ── 5. Save as prefab ─────────────────────────────────────────────
        string prefabDir = System.IO.Path.GetDirectoryName(PrefabPath);
        if (!System.IO.Directory.Exists(prefabDir))
            System.IO.Directory.CreateDirectory(prefabDir);

        var prefab = PrefabUtility.SaveAsPrefabAsset(rovInstance, PrefabPath);
        Object.DestroyImmediate(rovInstance);

        // ── 6. Swap capsule placeholder in active scene ────────────────────
        bool swapped = SwapPlaceholderInScene(prefab);

        AssetDatabase.Refresh();

        string msg = swapped
            ? "✅ ROV model set up and swapped into scene!\n\nThe capsule placeholder has been replaced with the real ROV model."
            : "✅ ROV prefab created at:\n" + PrefabPath +
              "\n\nNo placeholder capsule named 'ROV' found in the active scene.\n" +
              "Drag the prefab from Assets/Prefabs/ROV/ into your scene manually.";

        Debug.Log("[ROVModelSetup] " + msg);
        EditorUtility.DisplayDialog("ROV Model Setup", msg, "OK");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SWAP PLACEHOLDER
    // ═══════════════════════════════════════════════════════════════════════

    static bool SwapPlaceholderInScene(GameObject modelPrefab)
    {
        // Find the existing ROV capsule placeholder
        var placeholder = GameObject.Find("ROV");
        if (placeholder == null) return false;

        // Record position/rotation/scale and all components
        Vector3    pos   = placeholder.transform.position;
        Quaternion rot   = placeholder.transform.rotation;

        // The ROVSceneBuilder parents cameras/spotlight under the capsule.
        // We'll re-parent them under the new model root.
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in placeholder.transform)
            children.Add(child);

        // Grab component references we need to preserve
        var rb          = placeholder.GetComponent<Rigidbody>();
        var rovCtrl     = placeholder.GetComponent<ROVController>();
        var rovSpot     = placeholder.GetComponent<ROVSpotlight>();
        var rovCamRig   = placeholder.GetComponent<ROVCameraRig>();

        // ── Create new ROV root ───────────────────────────────────────────
        var newRoot = new GameObject("ROV");
        newRoot.tag = "ROV";
        newRoot.transform.position = pos;
        newRoot.transform.rotation = rot;

        // ── Instantiate visual model as child ─────────────────────────────
        var modelGO = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, newRoot.transform);
        modelGO.name = "ROV_Visual";
        modelGO.transform.localPosition = Vector3.zero;
        modelGO.transform.localRotation = Quaternion.identity;

        // ── Copy Rigidbody ────────────────────────────────────────────────
        var newRb = newRoot.AddComponent<Rigidbody>();
        if (rb != null)
        {
            newRb.mass                   = rb.mass;
            newRb.useGravity             = rb.useGravity;
            newRb.linearDamping          = rb.linearDamping;
            newRb.angularDamping         = rb.angularDamping;
            newRb.interpolation          = rb.interpolation;
            newRb.collisionDetectionMode = rb.collisionDetectionMode;
        }

        // ── Add a CapsuleCollider on root for physics ─────────────────────
        var col = newRoot.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0, 0);
        col.radius = 0.35f;
        col.height = 1.2f;
        col.direction = 2; // Z-axis (forward)

        // ── Copy ROV scripts ──────────────────────────────────────────────
        CopyComponentTo<ROVController>(placeholder, newRoot);
        CopyComponentTo<ROVSpotlight>(placeholder, newRoot);
        CopyComponentTo<ROVCameraRig>(placeholder, newRoot);

        // ── Re-parent children (cameras, spotlight) ───────────────────────
        foreach (var child in children)
            child.SetParent(newRoot.transform, true);

        // ── Destroy old capsule ───────────────────────────────────────────
        Object.DestroyImmediate(placeholder);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MATERIAL BUILDER
    // ═══════════════════════════════════════════════════════════════════════

    static Material BuildURPMaterial(string matName,
        string baseFile, string normalFile, string metallicFile,
        string roughFile, string emitFile, Color baseColor)
    {
        string matPath = $"{MatDir}/{matName}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null)
        {
            Debug.Log($"[ROVModelSetup] Material already exists, refreshing: {matPath}");
        }

        var mat = existing ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = matName;

        // Base color map
        var baseMap = LoadTex($"{TexDir}/{baseFile}");
        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
        mat.SetColor("_BaseColor", baseColor);

        // Normal map
        var normalMap = LoadTex($"{TexDir}/{normalFile}", isNormal: true);
        if (normalMap != null)
        {
            mat.SetTexture("_BumpMap", normalMap);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        // Metallic (R channel) + Smoothness from inverted Roughness
        var metallicMap = LoadTex($"{TexDir}/{metallicFile}");
        if (metallicMap != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallicMap);
            mat.SetFloat("_Metallic", 1f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        // Roughness → invert for smoothness
        var roughMap = LoadTex($"{TexDir}/{roughFile}");
        // URP uses smoothness = 1 - roughness; set smoothness slider low for rough metal
        mat.SetFloat("_Smoothness", 0.3f);

        // Emission
        var emitMap = LoadTex($"{TexDir}/{emitFile}");
        if (emitMap != null)
        {
            mat.SetTexture("_EmissionMap", emitMap);
            mat.SetColor("_EmissionColor", Color.white * 0.6f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (existing == null)
            AssetDatabase.CreateAsset(mat, matPath);
        else
            EditorUtility.SetDirty(mat);

        return mat;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    static Texture2D LoadTex(string path, bool isNormal = false)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            Debug.LogWarning($"[ROVModelSetup] Texture not found: {path}");
            return null;
        }
        if (isNormal)
        {
            // Ensure normal map import setting
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }
        return tex;
    }

    static Bounds CalculateBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    static T CopyComponentTo<T>(GameObject src, GameObject dst) where T : Component
    {
        var srcComp = src.GetComponent<T>();
        if (srcComp == null) return null;

        var dstComp = dst.GetComponent<T>() ?? dst.AddComponent<T>();
        var json = JsonUtility.ToJson(srcComp);
        JsonUtility.FromJsonOverwrite(json, dstComp);
        return dstComp;
    }
}
#endif
