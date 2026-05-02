using UnityEditor;
using UnityEngine;

namespace MazegeneratorPro
{
    [CustomEditor(typeof(MazeRenderer))]
    public class MazeRendererEditor : Editor
    {
        private SerializedProperty rows, cols, seed, cellSize;
        private SerializedProperty wallPrefab, floorPrefab, batPrefab, generateOnStart;
        private SerializedProperty entryExitTyoe;
        private Texture2D headerTexture;

        private void OnEnable()
        {
            rows = serializedObject.FindProperty("rows");
            cols = serializedObject.FindProperty("cols");
            seed = serializedObject.FindProperty("seed");
            cellSize = serializedObject.FindProperty("cellSize");
            wallPrefab = serializedObject.FindProperty("wallPrefab");
            floorPrefab = serializedObject.FindProperty("floorPrefab");
            batPrefab = serializedObject.FindProperty("batPrefab");
            generateOnStart = serializedObject.FindProperty("generateOnStart");
            entryExitTyoe = serializedObject.FindProperty("exitPlacement");
            headerTexture = Resources.Load<Texture2D>("maze_header");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (headerTexture != null)
            {
                float aspect = (float)headerTexture.width / headerTexture.height;
                float width = EditorGUIUtility.currentViewWidth - 20;
                float height = width / aspect;

                Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, headerTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.LabelField("Procedural Maze Generator", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space();

            DrawSectionHeader("Maze Settings");
            EditorGUILayout.PropertyField(rows, new GUIContent("Rows"));
            EditorGUILayout.PropertyField(cols, new GUIContent("Cols"));
            EditorGUILayout.PropertyField(seed, new GUIContent("Seed (0 = Random)"));
            EditorGUILayout.PropertyField(cellSize, new GUIContent("Cell Size"));

            EditorGUILayout.Space();

            DrawSectionHeader("Prefabs");
            EditorGUILayout.PropertyField(wallPrefab, new GUIContent("Wall Prefab"));
            EditorGUILayout.PropertyField(floorPrefab, new GUIContent("Floor Prefab"));
            EditorGUILayout.PropertyField(batPrefab, new GUIContent("Bat Prefab"));

            EditorGUILayout.Space();

            DrawSectionHeader("Options");
            EditorGUILayout.PropertyField(generateOnStart, new GUIContent("Generate On Start"));
            EditorGUILayout.PropertyField(entryExitTyoe, new GUIContent("Extry and Exit Type"));

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Maze", GUILayout.Height(30)))
            {
                (target as MazeRenderer).GenerateMaze();
            }
            if (GUILayout.Button("Clear Maze", GUILayout.Height(30)))
            {
                (target as MazeRenderer).ClearMaze();
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSectionHeader(string title)
        {
            GUIStyle style = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField(title, style);
        }
    }
}