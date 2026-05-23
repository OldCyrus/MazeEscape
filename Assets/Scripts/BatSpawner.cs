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

        [Header("Settings")]
        [Tooltip("How many bats to spawn per match.")]
        [SerializeField] private int batCount = 4;

        // How high above the floor to place the bat (matches NetworkPickupKey).
        private const float SpawnHeight = 0.5f;

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
                Debug.LogWarning("[BatSpawner] No valid spawn cells found in room interiors.", this);
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

        // Returns all interior cells from every room, shrunk 1 cell inward on each side
        // so spawns never land on doorway edges or outer room walls.
        private List<Vector2Int> BuildCandidateList(MazeData maze)
        {
            var candidates = new List<Vector2Int>();
            if (maze.rooms == null) return candidates;

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

        // ── Coordinate conversion ─────────────────────────────────────────────

        private static Vector3 CellToWorld(Vector2Int cell, MazeRenderer renderer)
        {
            return renderer.transform.TransformPoint(new Vector3(
                cell.x * renderer.cellSize,
                SpawnHeight,
                cell.y * renderer.cellSize));
        }

        // ── Spawn ─────────────────────────────────────────────────────────────

        private void SpawnBat(Vector3 position)
        {
            GameObject bat = Instantiate(batPickupPrefab, position, Quaternion.identity);
            NetworkObject no = bat.GetComponent<NetworkObject>();

            if (no != null)
            {
                no.Spawn();
            }
            else
            {
                Debug.LogError("[BatSpawner] batPickupPrefab is missing a NetworkObject component.", this);
                Destroy(bat);
            }
        }
    }
}
