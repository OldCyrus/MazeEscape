using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using MazeEscape;

/// <summary>
/// Installs the Key Pickup and Exit Door system into the active scene.
/// Run via: Tools > Install Key & Exit System
///
/// Creates:
///   NetworkKey     — in-scene NetworkObject, bottom-right maze corner (43.7, 0.5, -16.3)
///   ExitDoor       — in-scene NetworkObject at exit gap (44.7, 0, 9.7), built from Wall prefab
///   MatchManager   — in-scene NetworkObject
///   MatchResultCanvas — Canvas + MatchResultUI
///
/// Geometry maths (MazeGenerator at world (3.7, 0, -16.3), rows=14, cols=21, cellSize=2):
///   Cell centre (row, col) = (3.7 + col*2, 0, -16.3 + row*2)
///   Bottom-right (row=0, col=20): (3.7 + 40, 0, -16.3)      = (43.7, 0, -16.3)  ← KEY
///   Exit gap wall [r=13, c=21]:   localPos (41, 0, 26) → world (44.7, 0, 9.7)    ← EXIT DOOR
///
/// NOTE: Both NetworkPickupKey and ExitDoor also self-correct their positions via a
/// one-frame Start() coroutine, so the scene-placement values here are a safe fallback.
/// </summary>
public class KeyExitInstaller
{
    private static readonly Vector3 KeyPos      = new Vector3(43.7f, 0.5f, -16.3f);
    private static readonly Vector3 ExitDoorPos = new Vector3(44.7f, 0f,   9.7f);
    // Exit door matches a vertical maze wall (thin in X, wall height in Y, cellSize in Z)
    private static readonly Vector3 ExitDoorScale = new Vector3(0.2f, 5f, 2f);

    [MenuItem("Tools/Install Key & Exit System")]
    static void Install()
    {
        // ── Guard: remove stale objects ───────────────────────────────────────
        // Includes name variants that may have been placed by earlier installer
        // runs with wrong positions, including any blue-tinted wall objects.
        foreach (var name in new[] {
            "NetworkKey", "ExitDoor", "MatchManager", "MatchResultCanvas",
            "Exit", "ExitWall", "ExitGate", "ExitMarker" })
        {
            var old = GameObject.Find(name);
            if (old != null)
            {
                if (!EditorUtility.DisplayDialog("Replace existing?",
                    $"'{name}' already exists in the scene. Replace it?", "Yes", "Cancel"))
                    return;
                Object.DestroyImmediate(old);
            }
        }

        // ── 1. NetworkPickupKey ───────────────────────────────────────────────
        var keyGo = new GameObject("NetworkKey");
        keyGo.transform.position = KeyPos;
        keyGo.AddComponent<NetworkObject>();

        // NetworkTransform is required by NetworkPickupKey (drops sync position)
        keyGo.AddComponent<Unity.Netcode.Components.NetworkTransform>();

        keyGo.AddComponent<NetworkPickupKey>();

        // ── 2. ExitDoor ───────────────────────────────────────────────────────
        var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MazeGenerator/Prefabs/Wall.prefab");

        GameObject exitGo;
        if (wallPrefab != null)
        {
            exitGo = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
            exitGo.name = "ExitDoor";
            // Remove solid colliders — the gap is physically open; we only want a trigger
            foreach (var col in exitGo.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);
        }
        else
        {
            // Fallback: plain cube
            exitGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            exitGo.name = "ExitDoor";
            Object.DestroyImmediate(exitGo.GetComponent<Collider>());
        }

        exitGo.transform.position   = ExitDoorPos;
        exitGo.transform.localScale = ExitDoorScale;
        exitGo.layer                = 0; // Default

        exitGo.AddComponent<NetworkObject>();
        exitGo.AddComponent<ExitDoor>();

        // ── 3. MatchManager ───────────────────────────────────────────────────
        var mmGo = new GameObject("MatchManager");
        mmGo.AddComponent<NetworkObject>();
        var mm = mmGo.AddComponent<MatchManager>();

        // ── 4. MatchResultCanvas ──────────────────────────────────────────────
        var canvasGo = new GameObject("MatchResultCanvas");

        var canvas          = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler                 = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Semi-transparent dark background panel (full-screen)
        var panelGo      = new GameObject("ResultPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect    = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        var bg           = panelGo.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.75f);
        panelGo.SetActive(false); // hidden until match ends

        // Result text — centred
        var textGo      = new GameObject("ResultText");
        textGo.transform.SetParent(panelGo.transform, false);
        var textRect    = textGo.AddComponent<RectTransform>();
        textRect.anchorMin       = new Vector2(0.5f, 0.5f);
        textRect.anchorMax       = new Vector2(0.5f, 0.5f);
        textRect.pivot           = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta       = new Vector2(700f, 220f);
        textRect.anchoredPosition = Vector2.zero;

        var tmp             = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text            = "Result";
        tmp.fontSize        = 100f;
        tmp.fontStyle       = FontStyles.Bold;
        tmp.alignment       = TextAlignmentOptions.Center;
        tmp.color           = Color.white;
        tmp.enableWordWrapping = false;

        // Wire MatchResultUI references via SerializedObject
        var ui   = canvasGo.AddComponent<MatchResultUI>();
        var uiSO = new SerializedObject(ui);
        SetProp(uiSO, "matchManager", mm);
        SetProp(uiSO, "panel",        panelGo);
        SetProp(uiSO, "resultText",   tmp);
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        // ── 5. Finish ─────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = keyGo;

        Debug.Log(
            "[KeyExitInstaller] Done!\n\n" +
            $"  NetworkKey     scene pos {KeyPos}  → self-corrects to (43.7, 0.5, -16.3) on first frame\n" +
            $"  ExitDoor       scene pos {ExitDoorPos}, scale {ExitDoorScale}  → self-corrects to (44.7, 0, 9.7)\n" +
            "  MatchManager   in scene\n" +
            "  MatchResultCanvas in scene (hidden until match ends)\n\n" +
            "POSITIONS (rows=14, cols=21, cellSize=2, maze at (3.7,0,-16.3)):\n" +
            "  Entrance gap:      (2.7,  0, -16.3)  bottom-left\n" +
            "  Key spawn:         (43.7, 0.5, -16.3) bottom-right corner\n" +
            "  Exit gap:          (44.7, 0,   9.7)   top-right corner\n\n" +
            "FLOW:\n" +
            "  Host presses H → countdown → prison bars open → players enter maze\n" +
            "  Key is at the bottom-right corner — walk over it to pick it up\n" +
            "  Carry key to the top-right exit — wall glows green for the carrier\n" +
            "  Carrier presses E near the exit → YOU WIN / GAME OVER\n\n" +
            "NOTE: Save the scene. NetworkKey, ExitDoor, and MatchManager are in-scene\n" +
            "  NetworkObjects — no NetworkManager prefab list entry required.\n" +
            "  If a blue wall from a previous install remains, re-run this installer\n" +
            "  and choose 'Yes' when prompted to replace 'ExitDoor'."
        );
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    static void SetProp(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop == null)
        {
            Debug.LogError($"[KeyExitInstaller] Property '{propName}' not found on {so.targetObject.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
    }
}
