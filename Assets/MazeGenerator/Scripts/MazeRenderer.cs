using UnityEngine;
using Unity.AI.Navigation;

namespace MazegeneratorPro
{
    public class MazeRenderer : MonoBehaviour
    {
        [Header("Maze Settings")]
        public int rows = 10;
        public int cols = 10;
        public int seed = 0;
        public float cellSize = 2f;

        [Header("Prefabs")]
        public GameObject wallPrefab;
        public GameObject floorPrefab;

        [Header("Options")]
        public bool generateOnStart = true;
        public enum ExitPlacement { OppositeCorners, SameSide, Random }
        public ExitPlacement exitPlacement = ExitPlacement.OppositeCorners;


        private MazeData maze;
        private NavMeshSurface navMeshSurface;

        void Start()
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.Children;

            if (generateOnStart)
                GenerateMaze();
        }

        public void GenerateMaze()
        {
            ClearMaze();

            var generator = new MazeGenerator(rows, cols, seed);
            maze = generator.GenerateMaze();

            // --- Set exits ---
            switch (exitPlacement)
            {
                case ExitPlacement.OppositeCorners:
                    maze.verticalWalls[0, 0] = false;
                    maze.verticalWalls[rows - 1, cols] = false;
                    break;

                case ExitPlacement.SameSide:
                    maze.verticalWalls[0, 0] = false;
                    maze.verticalWalls[rows - 1, 0] = false;
                    break;

                case ExitPlacement.Random:
                    int randomRowStart = Random.Range(0, rows);
                    maze.horizontalWalls[0, randomRowStart] = false;
                    int randomRowEnd = Random.Range(0, rows);
                    maze.horizontalWalls[rows, randomRowEnd] = false;
                    break;
            }

            // Floor
            GameObject floor = Instantiate(floorPrefab, transform);
            floor.transform.localScale = new Vector3(cols * cellSize, 1, rows * cellSize);
            floor.transform.localPosition = new Vector3((cols * cellSize) / 2f - cellSize / 2f, -0.5f, (rows * cellSize) / 2f - cellSize / 2f);

            // Walls
            for (int r = 0; r < rows + 1; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (maze.horizontalWalls[r, c])
                    {
                        var wall = Instantiate(wallPrefab, transform);
                        wall.transform.localScale = new Vector3(cellSize, wall.transform.localScale.y, 0.2f);
                        wall.transform.localPosition = new Vector3(c * cellSize, 0, r * cellSize - cellSize / 2f);
                    }
                }
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols + 1; c++)
                {
                    if (maze.verticalWalls[r, c])
                    {
                        var wall = Instantiate(wallPrefab, transform);
                        wall.transform.localScale = new Vector3(0.2f, wall.transform.localScale.y, cellSize);
                        wall.transform.localPosition = new Vector3(c * cellSize - cellSize / 2f, 0, r * cellSize);
                    }
                }
            }

            // Bake NavMesh after maze is ready
            navMeshSurface.BuildNavMesh();
        }


        /// <summary>
        /// Returns the generated MazeData after GenerateMaze() has run.
        /// Used by BatSpawner (and any other system) to query cell layout.
        /// </summary>
        public MazeData GetMazeData() => maze;

        public void ClearMaze()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}