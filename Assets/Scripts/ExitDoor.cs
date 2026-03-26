using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace MazeEscape
{
    /// <summary>
    /// Placed at the maze exit gap (top-right corner, world (44.7, 0, 9.7)).
    /// Visually matches a normal wall for all players except the key carrier.
    ///
    /// Carrier-only behaviour:
    ///   - Wall glows bright green (local material swap, not networked).
    ///   - E key while inside the proximity trigger sends a validated ServerRpc
    ///     that ends the match via MatchManager.
    ///
    /// Non-carriers see the exit as an ordinary wall and cannot activate it.
    /// The gap is physically open (no solid collider), so all players can pass
    /// through — only the carrier can WIN by doing so.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ExitDoor : NetworkBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private float proximityRadius = 2.5f;

        // ── Exit position ─────────────────────────────────────────────────────
        // Exit gap wall: MazeGenerator at (3.7, 0, -16.3), rows=14, cols=21, cellSize=2
        // verticalWalls[rows-1, cols] → local (cols*2 - 1, 0, (rows-1)*2) = (41, 0, 26)
        // World: (3.7 + 41, 0, -16.3 + 26) = (44.7, 0, 9.7)
        private static readonly Vector3 ExitPosition = new Vector3(-1f, 0f, 9.7f);
        // Matches a vertical maze wall: thin in X, wall height in Y, cellSize in Z
        private static readonly Vector3 WallScale    = new Vector3(0.2f, 5f, 4f);

        // ── Material state ────────────────────────────────────────────────────
        private static readonly Color GreenColor   = new Color(0.1f, 1f, 0.35f);
        private const float           GreenEmission = 4f;

        private Renderer[] _renderers;
        private Material[] _normalMats;
        private Material[] _glowMats;
        private bool       _glowActive;

        // ── Trigger state (local client only) ────────────────────────────────
        private bool _playerInZone;

        // ── Start: deferred positioning + setup ───────────────────────────────
        // Wait one frame so MazeRenderer.Start() has finished generating the maze
        // before we position ourselves at the exit gap.
        // Unity supports IEnumerator Start() natively as an auto-coroutine.

        private void Awake()
        {
            // Snap to the correct exit gap position with wall-matching scale.
            // Done in Awake so the door is never visible at the wrong transform.
            transform.position   = ExitPosition;
            transform.localScale = WallScale;
        }

        private System.Collections.IEnumerator Start()
        {
            yield return null;

            // Match the layer used by all maze walls so camera culling is consistent.
            gameObject.layer = 6;

            // Build normal and green-glow material variants from whatever the
            // Wall prefab uses, so the door matches the maze visually by default.
            _renderers  = GetComponentsInChildren<Renderer>();
            _normalMats = new Material[_renderers.Length];
            _glowMats   = new Material[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                _normalMats[i] = _renderers[i].material; // already instanced by Unity

                var glow = new Material(_normalMats[i]) { color = GreenColor };
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", GreenColor * GreenEmission);
                _glowMats[i] = glow;
            }

            // Proximity zone — trigger only, no physical blocking
            var box        = gameObject.AddComponent<BoxCollider>();
            box.isTrigger  = true;
            box.size       = new Vector3(proximityRadius * 2f, 5f, proximityRadius * 2f);
        }

        // ── Update: glow + E key ──────────────────────────────────────────────

        private void Update()
        {
            if (!IsSpawned) return;

            bool amCarrier = NetworkPickupKey.Instance != null
                && NetworkPickupKey.Instance.CarrierClientId.Value
                   == NetworkManager.Singleton.LocalClientId;

            // Green glow — purely local, no network call needed
            if (amCarrier != _glowActive)
            {
                _glowActive = amCarrier;
                for (int i = 0; i < _renderers.Length; i++)
                    _renderers[i].material = amCarrier ? _glowMats[i] : _normalMats[i];
            }

            // E key: carrier in proximity activates exit
            if (amCarrier && _playerInZone
                && Keyboard.current != null
                && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ActivateExitServerRpc();
            }
        }

        // ── Trigger: proximity detection ──────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            // Only care about our own player object
            var ps = other.GetComponentInParent<Blocks.Gameplay.Core.CorePlayerState>();
            if (ps != null && ps.IsOwner)
                _playerInZone = true;
        }

        private void OnTriggerExit(Collider other)
        {
            var ps = other.GetComponentInParent<Blocks.Gameplay.Core.CorePlayerState>();
            if (ps != null && ps.IsOwner)
                _playerInZone = false;
        }

        // ── ServerRpc: validated exit activation ──────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        private void ActivateExitServerRpc(ServerRpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;

            // Double-check: sender must actually be the key carrier
            if (NetworkPickupKey.Instance == null
                || NetworkPickupKey.Instance.CarrierClientId.Value != sender)
            {
                Debug.LogWarning($"[ExitDoor] Client {sender} tried to activate exit but is not the carrier.");
                return;
            }

            if (MatchManager.Instance == null || MatchManager.Instance.MatchEnded.Value) return;

            MatchManager.Instance.EndMatch(sender);
        }
    }
}
