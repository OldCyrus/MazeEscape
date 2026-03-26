using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using MazeEscape;

/// <summary>
/// Creates the CountdownSystem NetworkObject and the CountdownCanvas UI in the
/// active scene, then wires all cross-references.
///
/// Run via: Tools > Install Countdown System
///
/// Prerequisites:
///   - SpawnRoom must already be in the scene (run "Tools > Build Spawn Room" first).
///   - TextMeshPro must be imported.
///   - Netcode for GameObjects must be in the project.
/// </summary>
public class CountdownSystemInstaller
{
    // ── Geometry — must match SpawnRoomBuilder constants ──────────────────────
    private static readonly Vector3 DoorWorldPos = new Vector3(2.7f, 0f, -16.3f);
    private const float GapWidth = 2f;

    // ── Menu entry ─────────────────────────────────────────────────────────────
    [MenuItem("Tools/Install Countdown System")]
    static void Install()
    {
        // Guard: destroy any existing CountdownSystem to avoid duplicates
        var existing = GameObject.Find("CountdownSystem");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "CountdownSystem exists",
                    "A CountdownSystem already exists in the scene. Replace it?",
                    "Replace", "Cancel"))
                return;
            Object.DestroyImmediate(existing);
        }

        var existingCanvas = GameObject.Find("CountdownCanvas");
        if (existingCanvas != null)
            Object.DestroyImmediate(existingCanvas);

        // ── 1. CountdownSystem NetworkObject ──────────────────────────────────
        var sysGo = new GameObject("CountdownSystem");
        sysGo.AddComponent<NetworkObject>();

        // PrisonBarDoor — configure via SerializedObject so serialized fields persist
        var door = sysGo.AddComponent<PrisonBarDoor>();
        SetSerializedField(door, "doorWorldPosition", DoorWorldPos);
        SetSerializedField(door, "gapWidth",          GapWidth);

        // CountdownController — wire door reference
        var controller = sysGo.AddComponent<CountdownController>();
        SetSerializedRef(controller, "prisonDoor", door);

        // ── 2. CountdownCanvas (screen-space overlay) ─────────────────────────
        var canvasGo = new GameObject("CountdownCanvas");

        var canvas            = canvasGo.AddComponent<Canvas>();
        canvas.renderMode     = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder   = 10;

        var scaler                 = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel — anchored to centre of screen
        var panelGo   = new GameObject("CountdownPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin       = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax       = new Vector2(0.5f, 0.5f);
        panelRect.pivot           = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta       = new Vector2(300f, 220f);
        panelRect.anchoredPosition = Vector2.zero;
        panelGo.SetActive(false); // hidden until countdown starts

        // TextMeshProUGUI
        var textGo   = new GameObject("CountdownText");
        textGo.transform.SetParent(panelGo.transform, false);
        var textRect  = textGo.AddComponent<RectTransform>();
        textRect.anchorMin  = Vector2.zero;
        textRect.anchorMax  = Vector2.one;
        textRect.offsetMin  = Vector2.zero;
        textRect.offsetMax  = Vector2.zero;

        var tmp            = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text           = "10";
        tmp.fontSize       = 120f;
        tmp.fontStyle      = FontStyles.Bold;
        tmp.alignment      = TextAlignmentOptions.Center;
        tmp.color          = Color.white;
        tmp.enableWordWrapping = false;

        // CountdownUI — attach to canvas root and wire references
        var ui = canvasGo.AddComponent<CountdownUI>();
        SetSerializedRef(ui, "controller",     controller);
        SetSerializedRef(ui, "countdownText",  tmp);
        SetSerializedRef(ui, "panel",          panelGo);

        // ── 3. Finish ─────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = sysGo;

        Debug.Log(
            "[CountdownSystemInstaller] Done!\n" +
            "  CountdownSystem (NetworkObject + PrisonBarDoor + CountdownController)\n" +
            $"    Door bars at world X={DoorWorldPos.x}, Z={DoorWorldPos.z}, gap={GapWidth}u\n" +
            "  CountdownCanvas  (Canvas + CanvasScaler + GraphicRaycaster + CountdownUI)\n" +
            "    └── CountdownPanel (hidden until H pressed)\n" +
            "         └── CountdownText (TextMeshProUGUI, 120pt bold)\n\n" +
            "NEXT STEPS:\n" +
            "  1. Add a NetworkManager to the scene if not present.\n" +
            "  2. Press Play → start as Host → press H to test countdown.\n" +
            "  3. The prison bars appear at the spawn room entrance and slide up when timer hits 0."
        );
    }

    // ── SerializedObject helpers ───────────────────────────────────────────────

    static void SetSerializedField(Object target, string propName, Vector3 value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(propName);
        if (prop == null) { Debug.LogError($"[Installer] Could not find property '{propName}' on {target.GetType().Name}"); return; }
        prop.vector3Value = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedField(Object target, string propName, float value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(propName);
        if (prop == null) { Debug.LogError($"[Installer] Could not find property '{propName}' on {target.GetType().Name}"); return; }
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedRef(Object target, string propName, Object value)
    {
        var so   = new SerializedObject(target);
        var prop = so.FindProperty(propName);
        if (prop == null) { Debug.LogError($"[Installer] Could not find property '{propName}' on {target.GetType().Name}"); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
