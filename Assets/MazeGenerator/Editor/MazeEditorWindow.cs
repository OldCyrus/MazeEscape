using UnityEditor;
using UnityEngine;

namespace MazegeneratorPro
{
    public class MazeEditorWindow : EditorWindow
    {
        private MazeRenderer mazeRenderer;

        [MenuItem("Tools/Procedural Maze Generator")]
        public static void OpenWindow()
        {
            GetWindow<MazeEditorWindow>("Maze Generator");
        }

        void OnGUI()
        {
            mazeRenderer = (MazeRenderer)EditorGUILayout.ObjectField("Maze Renderer", mazeRenderer, typeof(MazeRenderer), true);

            if (mazeRenderer == null)
            {
                EditorGUILayout.HelpBox("Assign a MazeRenderer in the scene.", MessageType.Info);
                return;
            }

            mazeRenderer.rows = EditorGUILayout.IntField("Rows", mazeRenderer.rows);
            mazeRenderer.cols = EditorGUILayout.IntField("Cols", mazeRenderer.cols);
            mazeRenderer.seed = EditorGUILayout.IntField("Seed (0 = Random)", mazeRenderer.seed);
            mazeRenderer.cellSize = EditorGUILayout.FloatField("Cell Size", mazeRenderer.cellSize);

            mazeRenderer.wallPrefab = (GameObject)EditorGUILayout.ObjectField("Wall Prefab", mazeRenderer.wallPrefab, typeof(GameObject), false);
            mazeRenderer.floorPrefab = (GameObject)EditorGUILayout.ObjectField("Floor Prefab", mazeRenderer.floorPrefab, typeof(GameObject), false);

            GUILayout.Space(10);
            if (GUILayout.Button("Generate Maze"))
            {
                mazeRenderer.GenerateMaze();
            }

            if (GUILayout.Button("Clear Maze"))
            {
                mazeRenderer.ClearMaze();
            }
        }
    }
}
