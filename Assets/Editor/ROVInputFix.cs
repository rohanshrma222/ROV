#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes the "switched active Input handling" error from Joystick Pack.
/// Sets Active Input Handling to "Both" (old + new Input System coexist).
/// Menu: ROV → Fix Input System
/// 
/// After running: Unity will ask to restart — click "Apply" / "Yes".
/// </summary>
public static class ROVInputFix
{
    [MenuItem("ROV/Fix Input System (Fix Joystick Errors)")]
    public static void FixInputSystem()
    {
        // activeInputHandler: 0 = Input Manager (Old)
        //                     1 = Input System Package (New)
        //                     2 = Both
        var buildTargetGroup = BuildTargetGroup.Standalone;
        PlayerSettings.SetScriptingBackend(buildTargetGroup, ScriptingImplementation.Mono2x);

        // Use reflection to set activeInputHandler = 2 (Both)
        var playerSettingsType = typeof(PlayerSettings);
        var prop = playerSettingsType.GetProperty("activeInputHandler",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (prop != null)
        {
            prop.SetValue(null, 2);
            Debug.Log("[ROVInputFix] activeInputHandler set to 2 (Both).");
        }
        else
        {
            // Fallback: write directly to ProjectSettings asset
            string settingsPath = "ProjectSettings/ProjectSettings.asset";
            string content = System.IO.File.ReadAllText(settingsPath);

            if (content.Contains("activeInputHandler:"))
            {
                content = System.Text.RegularExpressions.Regex.Replace(
                    content, @"activeInputHandler:\s*\d", "activeInputHandler: 2");
            }
            else
            {
                // Insert after a known nearby line
                content = content.Replace(
                    "  forceSingleInstance: 0",
                    "  forceSingleInstance: 0\n  activeInputHandler: 2");
            }

            System.IO.File.WriteAllText(settingsPath, content);
            Debug.Log("[ROVInputFix] Wrote activeInputHandler: 2 to ProjectSettings.asset directly.");
        }

        // Also replace StandaloneInputModule with InputSystemUIInputModule in the scene
        FixEventSystemInScene();

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Input System Fixed",
            "✅ Active Input Handling set to BOTH.\n\n" +
            "Unity may ask you to restart — click Apply/Yes.\n\n" +
            "After restart:\n" +
            "• Press Play → no more Input errors\n" +
            "• Keyboard (WASD/QE) + joystick both work",
            "OK");
    }

    static void FixEventSystemInScene()
    {
        // Find StandaloneInputModule and replace with InputSystemUIInputModule
        var standalone = Object.FindFirstObjectByType<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone == null) return;

        var go = standalone.gameObject;
        Object.DestroyImmediate(standalone);

        // Try to add InputSystemUIInputModule (requires Input System package)
        var inputSystemType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");

        if (inputSystemType == null)
            inputSystemType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

        if (inputSystemType != null)
        {
            go.AddComponent(inputSystemType);
            Debug.Log("[ROVInputFix] Replaced StandaloneInputModule with InputSystemUIInputModule.");
        }
        else
        {
            // Re-add StandaloneInputModule as fallback (will work once "Both" is set)
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[ROVInputFix] Kept StandaloneInputModule (will work with Both mode).");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
#endif
