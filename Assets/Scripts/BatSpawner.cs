using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using MazegeneratorPro;

namespace MazeEscape
{
    /// <summary>
    /// Server-only spawner. Waits one frame for MazeRenderer.Start() to finish
    /// generating the maze, then places exactly 4 baseball bat pickups in random
    /// walkable cells within the "back half" of the maze.
    ///
    /// Back half definition:
    ///   Columns >= (cols / 2) — the right side of the maze, away from the
    ///   player spawn entrance (col 0, left edge). This keeps bats away from
    ///   the spawn corridor and puts them near the key and exit area.
    ///
    /// Additionally excludes:
    ///   - The 2 outermost rows/cols (outer wall ring)
    ///   - Any cell adjacent to the key spawn position
    ///
    /// Setup in the scene:
    ///   1. Add this component to any persistent server GameObject (e.g. the
    ///      same object that holds MatchManager, or a new "BatSpawner" object).
    ///   2. Assign mazeRenderer and batPickupPrefab in the Inspector.
    ///   3. The batPickupPrefab must have a NetworkObject component.
    /// </summary>
    public class BatSpawner : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("The MazeRenderer in the scene. Drag it here in the Inspector.")]
        [SerializeField] private MazeRenderer mazeRenderer;

        [Tooltip("The BaseballBat pickup prefab (must have NetworkObject).")]
        [SerializeField] private GameObject batPickupPrefab;

        [Tooltip("Optional prefab for the carried bat visual. Forwarded to each spawned bat.")]
        public GameObject batVisualPrefab;

        [Header("Settings")]
        [Tooltip("How many bats to spawn per match.")]
        [SerializeField] private int batCount = 4;

        [Tooltip("Minimum column index for bat spawning (0 = left edge). " +
                 "Default 0 lets the code calculate half automatically. " +
                 "Override here to fine-tune the spawn zone.")]
        [SerializeField] private int minColOverride = 0;

        [Tooltip("Minimum distance in cells from the key spawn cell to exclude " +
                 "bats spawning right on top of the key.")]
        [SerializeField] private int keyExclusionRadius = 2;

        // Key spawn: col=20, row=0 (matches NetworkPickupKey.SpawnPosition logic)
        // These must match MazeRenderer's rows/cols/cellSize and the key position
        // in NetworkPickupKey. If those change, update here.
        private const int KeySpawnCol = 20;
        private const int KeySpawnRow = 0;

        // How high above the floor to place the bat (matches NetworkPickupKey).
        private const float SpawnHeight = 0.5f;

        // Outer ring exclusion — don't spawn in the outermost cells (walls live there).
        private const int BorderMargin = 1;

        // ── Network spawn ─────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            // Only the server spawns bats.
            if (!IsServer) return;

            if (mazeRenderer == null)
            {
                Debug.LogError("[BatSpawner] mazeRenderer is not assigned.", this);
                return;
            }

            if (batPickupPrefab == null)
            {
                Debug.LogError("[BatSpawner] batPickupPrefab is not assigned.", this);
                return;
            }

            // MazeRenderer.Start() runs in the same frame as our OnNetworkSpawn.
            // Wait one frame to guarantee GenerateMaze() has completed.
            StartCoroutine(SpawnAfterMazeReady());
        }

        // ── Coroutine ─────────────────────────────────────────────────────────

        private IEnumerator SpawnAfterMazeReady()
        {
            // One frame delay — MazeRenderer.Start() has now finished.
            yield return null;

            MazeData maze = mazeRenderer.GetMazeData();

            if (maze == null)
            {
                Debug.LogError("[BatSpawner] MazeData is null after generation. " +
                               "Ensure MazeRenderer.generateOnStart is true and " +
                               "GetMazeData() is exposed.", this);
                yield break;
            }

            List<Vector2Int> candidates = BuildCandidateList(maze);

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[BatSpawner] No valid spawn cells found in back half of maze.", this);
                yield break;
            }

            // Shuffle the candidate list.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int toSpawn = Mathf.Min(batCount, candidates.Count);
            for (int i = 0; i < toSpawn; i++)
            {
                Vector2Int cell = candidates[i];
                Vector3 worldPos = CellToWorld(cell, mazeRenderer);
                SpawnBat(worldPos);
            }

            Debug.Log($"[BatSpawner] Spawned {toSpawn} baseball bats.");
        }

        // ── Candidate cell logic ──────────────────────────────────────────────

        /// <summary>
        /// Returns all walkable cells in the back (right) half of the maze,
        /// excluding the border ring and the area around the key spawn.
        /// A cell is considered walkable if it was carved by the DFS — i.e. at
        /// least one of its four walls is open (not solid). This is always true
        /// for every interior cell after generation.
        /// </summary>
        private List<Vector2Int> BuildCandidateList(MazeData maze)
        {
            int cols    = maze.cols;
            int rows    = maze.rows;

            // Back half: right portion of the maze, away from the spawn entrance.
            // minCol is at least half the maze width.
            int minCol = Mathf.Max(minColOverride > 0 ? minColOverride : cols / 2,
                                   BorderMargin);
            int maxCol = cols - 1 - BorderMargin;
            int minRow = BorderMargin;
            int maxRow = rows - 1 - BorderMargin;

            var candidates = new List<Vector2Int>();

            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    // Exclude cells too close to the key spawn.
                    int dRow = Mathf.Abs(r - KeySpawnRow);
                    int dCol = Mathf.Abs(c - KeySpawnCol);
                    if (dRow <= keyExclusionRadius && dCol <= keyExclusionRadius)
                        continue;

                    // All interior cells after DFS are reachable — no further
                    // walkability check needed. The maze guarantees connectivity.
                    candidates.Add(new Vector2Int(c, r));
                }
            }

            return candidates;
        }

        // ── Coordinate conversion ─────────────────────────────────────────────

        /// <summary>
        /// Converts a maze cell (col, row) to a world position, accounting for
        /// MazeRenderer's transform origin and cellSize.
        /// Matches the formula used by MazeRenderer when placing walls/floor.
        /// </summary>
        private static Vector3 CellToWorld(Vector2Int cell, MazeRenderer renderer)
        {
            float cs   = renderer.cellSize;
            Vector3 origin = renderer.transform.position;

            // MazeRenderer places objects at: localPos = (c * cs, 0, r * cs)
            // relative to the renderer's transform.
            float worldX = origin.x + cell.x * cs;
            float worldZ = origin.z + cell.y * cs;

            return new Vector3(worldX, origin.y + SpawnHeight, worldZ);
        }

        // ── Spawn ─────────────────────────────────────────────────────────────

        private void SpawnBat(Vector3 position)
        {
            GameObject bat = Instantiate(batPickupPrefab, position, Quaternion.identity);
            NetworkObject no = bat.GetComponent<NetworkObject>();

            if (no != null)
            {
                no.Spawn();
                BaseballBat bb = bat.GetComponent<BaseballBat>();
                if (bb != null)
                    bb.SetVisualPrefab(batVisualPrefab);
            }
            else
            {
                Debug.LogError("[BatSpawner] batPickupPrefab is missing a NetworkObject component.", this);
                Destroy(bat);
            }
        }
    }
}
