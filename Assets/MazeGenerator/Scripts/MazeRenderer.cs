using UnityEngine;
using Unity.AI.Navigation;
using Unity.Netcode;

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
        public GameObject batPrefab;
        public GameObject pistolPrefab;

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

            // Bat spawning is handled by BatSpawner (a separate NetworkBehaviour).
            // Pistol spawning remains here.
            if (pistolPrefab != null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    SpawnPistols();
                else if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.OnServerStarted += SpawnPistols;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnServerStarted -= SpawnPistols;
        }

        private void SpawnPistols()
        {
            var candidates = BuildRoomInteriorCandidates();
            ShuffleList(candidates);

            int toSpawn = Mathf.Min(3, candidates.Count);
            for (int i = 0; i < toSpawn; i++)
            {
                Vector3 worldPos = transform.TransformPoint(new Vector3(candidates[i].x * cellSize, 0.5f, candidates[i].y * cellSize));
                var go = Instantiate(pistolPrefab, worldPos, Quaternion.identity);
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null) { Destroy(go); break; }
                netObj.Spawn();
            }
        }

        // Returns all interior cells from every room, shrunk 1 cell inward on each side
        // so spawns never land on doorway edges or outer room walls.
        private System.Collections.Generic.List<Vector2Int> BuildRoomInteriorCandidates()
        {
            var candidates = new System.Collections.Generic.List<Vector2Int>();
            if (maze?.rooms == null) return candidates;

            foreach (RectInt room in maze.rooms)
            {
                int minCol = room.x + 1;
                int maxCol = room.x + room.width - 2;
                int minRow = room.y + 1;
                int maxRow = room.y + room.height - 2;

                for (int r = minRow; r <= maxRow; r++)
                    for (int c = minCol; c <= maxCol; c++)
                        candidates.Add(new Vector2Int(c, r));
            }
            return candidates;
        }

        private static void ShuffleList<T>(System.Collections.Generic.List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
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