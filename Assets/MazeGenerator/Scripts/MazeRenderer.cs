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

            // Bat spawning is server-authoritative — only the server creates NetworkObjects.
            // If the server hasn't started yet (maze generated before host session begins),
            // subscribe to OnServerStarted so bats spawn the moment the session is ready.
            if (batPrefab != null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    Debug.Log("[MazeRenderer] Server already running — calling SpawnBats directly.");
                    SpawnBats();
                }
                else if (NetworkManager.Singleton != null)
                {
                    Debug.Log("[MazeRenderer] Server not yet started — subscribing to OnServerStarted.");
                    NetworkManager.Singleton.OnServerStarted += SpawnBats;
                }
                else
                {
                    Debug.LogWarning("[MazeRenderer] NetworkManager.Singleton is null — bats cannot be spawned.");
                }
            }
            else
            {
                Debug.LogWarning("[MazeRenderer] batPrefab is null — bats cannot be spawned.");
            }

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
            {
                NetworkManager.Singleton.OnServerStarted -= SpawnBats;
                NetworkManager.Singleton.OnServerStarted -= SpawnPistols;
            }
        }

        private void SpawnBats()
        {
            Debug.Log($"[MazeRenderer] SpawnBats called. IsServer={NetworkManager.Singleton?.IsServer}, IsHost={NetworkManager.Singleton?.IsHost}");

            var candidates = new System.Collections.Generic.List<Vector2Int>();

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (c >= cols / 2)
                        candidates.Add(new Vector2Int(c, r));
                }
            }

            // Shuffle candidates
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }

            // Pick 4 cells that are at least 3 cells apart from each other
            var chosen = new System.Collections.Generic.List<Vector2Int>();

            foreach (var cell in candidates)
            {
                bool tooClose = false;
                foreach (var picked in chosen)
                {
                    if (Mathf.Abs(cell.x - picked.x) + Mathf.Abs(cell.y - picked.y) < 3)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                {
                    chosen.Add(cell);
                    if (chosen.Count == 4) break;
                }
            }

            Debug.Log($"[MazeRenderer] SpawnBats: {chosen.Count} spawn locations chosen from {candidates.Count} candidates.");

            // Spawn bats as NetworkObjects so they can be server-despawned on pickup
            foreach (var cell in chosen)
            {
                Vector3 worldPos = transform.TransformPoint(new Vector3(cell.x * cellSize, 0f, cell.y * cellSize));
                var go = Instantiate(batPrefab, worldPos, Quaternion.identity);
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    Debug.LogError($"[MazeRenderer] Bat prefab '{batPrefab.name}' is missing a NetworkObject component. Bats cannot be spawned.", batPrefab);
                    Destroy(go);
                    break;
                }
                Debug.Log($"[MazeRenderer] Spawning bat at {worldPos}, NetworkObject found: {netObj != null}, IsSpawned before Spawn(): {netObj.IsSpawned}");
                netObj.Spawn();
                Debug.Log($"[MazeRenderer] Bat Spawn() called. IsSpawned after: {netObj.IsSpawned}");
            }
        }

        private void SpawnPistols()
        {
            var candidates = new System.Collections.Generic.List<Vector2Int>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (c >= cols / 2)
                        candidates.Add(new Vector2Int(c, r));

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }

            var chosen = new System.Collections.Generic.List<Vector2Int>();
            foreach (var cell in candidates)
            {
                bool tooClose = false;
                foreach (var picked in chosen)
                    if (Mathf.Abs(cell.x - picked.x) + Mathf.Abs(cell.y - picked.y) < 3)
                    { tooClose = true; break; }
                if (!tooClose)
                {
                    chosen.Add(cell);
                    if (chosen.Count == 3) break;
                }
            }

            foreach (var cell in chosen)
            {
                Vector3 worldPos = transform.TransformPoint(new Vector3(cell.x * cellSize, 0f, cell.y * cellSize));
                var go = Instantiate(pistolPrefab, worldPos, Quaternion.identity);
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null) { Destroy(go); break; }
                netObj.Spawn();
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