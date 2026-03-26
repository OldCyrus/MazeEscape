using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the SpawnRoom prefab and places an instance in the active scene.
/// Run via: Tools > Build Spawn Room
/// </summary>
public class SpawnRoomBuilder
{
    // ── Geometry constants ────────────────────────────────────────────────────
    const float EAST_X       =  2.7f;   // maze entrance gap X (right edge of room)
    const float ROOM_WIDTH   = 12f;
    const float ROOM_DEPTH   = 12f;
    const float WALL_HEIGHT  =  5f;
    const float WALL_T       =  0.2f;   // wall thickness
    const float ENTRANCE_Z   = -16.3f;  // entrance gap centre Z
    const float CELL_SIZE    =  2f;     // gap width matches maze cell size
    const float FLOOR_Y      = -0.5f;

    // Derived bounds
    static float WestX    => EAST_X - ROOM_WIDTH;          // -9.3
    static float CenterX  => (EAST_X + WestX) / 2f;       // -3.3
    static float NorthZ   => ENTRANCE_Z + ROOM_DEPTH / 2f; // -10.3
    static float SouthZ   => ENTRANCE_Z - ROOM_DEPTH / 2f; // -22.3

    // ── Menu entry ────────────────────────────────────────────────────────────
    [MenuItem("Tools/Build Spawn Room")]
    static void Build()
    {
        // Load prefabs
        var wallPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MazeGenerator/Prefabs/Wall.prefab");
        var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MazeGenerator/Prefabs/Floor.prefab");

        if (wallPrefab == null || floorPrefab == null)
        {
            Debug.LogError("[SpawnRoomBuilder] Could not find Wall.prefab or Floor.prefab. Check the paths.");
            return;
        }

        // Ensure the SpawnPoint tag exists
        EnsureTagExists("SpawnPoint");

        // ── Root object ───────────────────────────────────────────────────────
        var root = new GameObject("SpawnRoom");
        root.transform.position = new Vector3(CenterX, 0f, ENTRANCE_Z);

        // ── Floor ─────────────────────────────────────────────────────────────
        // 12×12, centred on room, Y at -0.5 to match maze floor
        var floor = Spawn(floorPrefab, root, "SpawnRoom_Floor",
            new Vector3(CenterX, FLOOR_Y, ENTRANCE_Z),
            new Vector3(ROOM_WIDTH, 1f, ROOM_DEPTH));
        floor.layer = 0; // Default layer as requested

        // ── North wall (Z = -10.3, full 12 units, no opening) ─────────────────
        Spawn(wallPrefab, root, "Wall_North",
            new Vector3(CenterX, 0f, NorthZ),
            new Vector3(ROOM_WIDTH, WALL_HEIGHT, WALL_T));

        // ── South wall (Z = -22.3, full 12 units, no opening) ─────────────────
        Spawn(wallPrefab, root, "Wall_South",
            new Vector3(CenterX, 0f, SouthZ),
            new Vector3(ROOM_WIDTH, WALL_HEIGHT, WALL_T));

        // ── West wall (X = -9.3, countdown door side, full 12 units) ──────────
        Spawn(wallPrefab, root, "Wall_West_CountdownDoor",
            new Vector3(WestX, 0f, ENTRANCE_Z),
            new Vector3(WALL_T, WALL_HEIGHT, ROOM_DEPTH));

        // ── East wall with 2-unit opening (X = 2.7, connects to maze) ─────────
        // Gap spans Z: ENTRANCE_Z ± CELL_SIZE/2  →  -17.3 to -15.3
        float gapTop    = ENTRANCE_Z + CELL_SIZE / 2f;  // -15.3
        float gapBottom = ENTRANCE_Z - CELL_SIZE / 2f;  // -17.3

        // Upper segment: gapTop (-15.3) to NorthZ (-10.3) = 5 units
        float upperLen    = NorthZ - gapTop;   // 5.0
        float upperCentreZ = (NorthZ + gapTop) / 2f; // -12.8
        Spawn(wallPrefab, root, "Wall_East_Upper",
            new Vector3(EAST_X, 0f, upperCentreZ),
            new Vector3(WALL_T, WALL_HEIGHT, upperLen));

        // Lower segment: SouthZ (-22.3) to gapBottom (-17.3) = 5 units
        float lowerLen     = gapBottom - SouthZ;       // 5.0
        float lowerCentreZ = (SouthZ + gapBottom) / 2f; // -19.8
        Spawn(wallPrefab, root, "Wall_East_Lower",
            new Vector3(EAST_X, 0f, lowerCentreZ),
            new Vector3(WALL_T, WALL_HEIGHT, lowerLen));

        // ── Spawn points (8 in 4×2 grid) ──────────────────────────────────────
        // 4 columns: evenly spaced, 12/5 = 2.4 unit intervals from west wall
        // 2 rows:    evenly spaced, 12/3 = 4.0 unit intervals from south wall
        //
        //  X positions: -6.9, -4.5, -2.1,  0.3
        //  Z positions: -18.3, -14.3
        //
        float xStep = ROOM_WIDTH / 5f;  // 2.4
        float zStep = ROOM_DEPTH / 3f;  // 4.0

        float[] xs = new float[4];
        for (int i = 0; i < 4; i++)
            xs[i] = WestX + xStep * (i + 1);

        float[] zs = new float[2];
        zs[0] = SouthZ + zStep;      // -18.3
        zs[1] = SouthZ + zStep * 2f; // -14.3

        int spIdx = 0;
        foreach (float spZ in zs)
        {
            foreach (float spX in xs)
            {
                var sp = new GameObject("SpawnPoint_" + spIdx);
                sp.transform.SetParent(root.transform);
                sp.transform.position = new Vector3(spX, 0f, spZ);
                sp.tag = "SpawnPoint";
                spIdx++;
            }
        }

        // ── Save as prefab ─────────────────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        const string prefabPath = "Assets/Prefabs/SpawnRoom.prefab";
        var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(
            root, prefabPath, InteractionMode.UserAction);

        if (saved != null)
            Debug.Log($"[SpawnRoomBuilder] Prefab saved to {prefabPath}");
        else
            Debug.LogError("[SpawnRoomBuilder] Failed to save prefab.");

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        LogSummary();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject Spawn(GameObject prefab, GameObject parent, string name,
                            Vector3 worldPos, Vector3 scale)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.position  = worldPos;
        go.transform.localScale = scale;
        return go;
    }

    static void EnsureTagExists(string tag)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset");
        if (asset == null) return;

        var so = new SerializedObject(asset);
        var tags = so.FindProperty("tags");

        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
        Debug.Log($"[SpawnRoomBuilder] Added tag '{tag}' to TagManager.");
    }

    static void LogSummary()
    {
        Debug.Log(
            "[SpawnRoomBuilder] === Spawn Room Summary ===\n" +
            $"  Floor:       pos ({CenterX:F1}, {FLOOR_Y:F1}, {ENTRANCE_Z:F1})  scale (12, 1, 12)\n" +
            $"  Wall North:  pos ({CenterX:F1}, 0, {NorthZ:F1})  scale (12, 5, 0.2)\n" +
            $"  Wall South:  pos ({CenterX:F1}, 0, {SouthZ:F1})  scale (12, 5, 0.2)\n" +
            $"  Wall West:   pos ({WestX:F1}, 0, {ENTRANCE_Z:F1})  scale (0.2, 5, 12)  [CountdownDoor]\n" +
            $"  Wall E-Upper: pos ({EAST_X:F1}, 0, -12.8)  scale (0.2, 5, 5)\n" +
            $"  Wall E-Lower: pos ({EAST_X:F1}, 0, -19.8)  scale (0.2, 5, 5)\n" +
            $"  Gap centre:  ({EAST_X:F1}, 0, {ENTRANCE_Z:F1})  width 2 units → aligns with maze entrance\n" +
            $"  SpawnPoints: 8 in 4×2 grid  (X: -6.9/-4.5/-2.1/0.3, Z: -18.3/-14.3)\n" +
            $"  Prefab:      Assets/Prefabs/SpawnRoom.prefab"
        );
    }
}
