using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using MazegeneratorPro;

namespace MazeEscape
{
    /// <summary>
    /// Server-only spawner. After the maze generates, places each item type
    /// across rooms using round-robin distribution — one item per room before
    /// any room receives a second of the same type.
    ///
    /// To add a new pickup: add a row to the Items list in the Inspector.
    /// The prefab must have a NetworkObject component.
    /// </summary>
    public class ItemSpawner : NetworkBehaviour
    {
        [System.Serializable]
        public class SpawnEntry
        {
            [Tooltip("The pickup prefab to spawn. Must have a NetworkObject component.")]
            public GameObject prefab;
            [Tooltip("How many of this item to place per match.")]
            [Min(0)]
            public int count = 1;
        }

        [Header("References")]
        [SerializeField] private MazeRenderer mazeRenderer;

        [Header("Loot Table")]
        [SerializeField] private List<SpawnEntry> items = new List<SpawnEntry>();

        private const float SpawnHeight = 0.5f;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            if (mazeRenderer == null)
            {
                Debug.LogError("[ItemSpawner] mazeRenderer is not assigned.", this);
                return;
            }

            StartCoroutine(SpawnAfterMazeReady());
        }

        private IEnumerator SpawnAfterMazeReady()
        {
            yield return null;

            MazeData maze = mazeRenderer.GetMazeData();
            if (maze == null)
            {
                Debug.LogError("[ItemSpawner] MazeData is null after generation. " +
                               "Ensure MazeRenderer.generateOnStart is true.", this);
                yield break;
            }

            List<List<Vector2Int>> perRoom = BuildPerRoomCandidates(maze);
            if (perRoom.Count == 0)
            {
                Debug.LogWarning("[ItemSpawner] No valid spawn cells found in room interiors.", this);
                yield break;
            }

            foreach (var entry in items)
            {
                if (entry.prefab == null || entry.count <= 0) continue;
                SpawnEntryAcrossRooms(entry, perRoom);
            }
        }

        private void SpawnEntryAcrossRooms(SpawnEntry entry, List<List<Vector2Int>> perRoom)
        {
            // Deep-copy so each item type gets its own independently shuffled room order.
            var rooms = new List<List<Vector2Int>>(perRoom.Count);
            foreach (var cells in perRoom)
                rooms.Add(new List<Vector2Int>(cells));

            // Shuffle room order.
            for (int i = rooms.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (rooms[i], rooms[j]) = (rooms[j], rooms[i]);
            }

            // Shuffle cells within each room.
            foreach (var cells in rooms)
            {
                for (int i = cells.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (cells[i], cells[j]) = (cells[j], cells[i]);
                }
            }

            // Round-robin: one item per room before any room gets a second.
            int spawned = 0;
            int pass = 0;

            while (spawned < entry.count)
            {
                bool placedAny = false;
                for (int r = 0; r < rooms.Count && spawned < entry.count; r++)
                {
                    var cells = rooms[r];
                    if (pass >= cells.Count) continue;

                    Vector3 worldPos = CellToWorld(cells[pass], mazeRenderer);
                    SpawnItem(entry.prefab, worldPos);
                    spawned++;
                    placedAny = true;
                }

                pass++;
                if (!placedAny) break;
            }

            Debug.Log($"[ItemSpawner] Spawned {spawned}x {entry.prefab.name}.");
        }

        private List<List<Vector2Int>> BuildPerRoomCandidates(MazeData maze)
        {
            var perRoom = new List<List<Vector2Int>>();
            if (maze.rooms == null) return perRoom;

            foreach (RectInt room in maze.rooms)
            {
                int minCol = room.x + 1;
                int maxCol = room.x + room.width - 2;
                int minRow = room.y + 1;
                int maxRow = room.y + room.height - 2;

                if (minCol > maxCol || minRow > maxRow) continue;

                var cells = new List<Vector2Int>();
                for (int r = minRow; r <= maxRow; r++)
                    for (int c = minCol; c <= maxCol; c++)
                        cells.Add(new Vector2Int(c, r));

                perRoom.Add(cells);
            }

            return perRoom;
        }

        private static Vector3 CellToWorld(Vector2Int cell, MazeRenderer renderer)
        {
            return renderer.transform.TransformPoint(new Vector3(
                cell.x * renderer.cellSize,
                SpawnHeight,
                cell.y * renderer.cellSize));
        }

        private void SpawnItem(GameObject prefab, Vector3 position)
        {
            GameObject go = Instantiate(prefab, position, Quaternion.identity);
            NetworkObject no = go.GetComponent<NetworkObject>();

            if (no != null)
            {
                no.Spawn();
            }
            else
            {
                Debug.LogWarning($"[ItemSpawner] {prefab.name} has no NetworkObject — spawned locally only.", this);
            }
        }
    }
}
