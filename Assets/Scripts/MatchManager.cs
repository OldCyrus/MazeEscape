using UnityEngine;
using Unity.Netcode;

namespace MazeEscape
{
    /// <summary>
    /// Singleton NetworkObject that tracks match end state.
    /// Server writes; all clients read.
    ///
    /// When the key carrier activates the exit, ExitDoor calls EndMatch()
    /// (server-side). The NetworkVariables replicate to all clients and
    /// MatchResultUI shows the appropriate win/lose screen locally.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class MatchManager : NetworkBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static MatchManager Instance { get; private set; }

        // ── Synced state ──────────────────────────────────────────────────────
        public readonly NetworkVariable<bool> MatchEnded = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>ulong.MaxValue = no winner yet.</summary>
        public readonly NetworkVariable<ulong> WinnerId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake() => Instance = this;

        public override void OnNetworkSpawn()
        {
            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API (server only) ──────────────────────────────────────────

        /// <summary>
        /// Ends the match and declares a winner. Must be called on the server.
        /// </summary>
        public void EndMatch(ulong winnerId)
        {
            if (!IsServer) return;
            if (MatchEnded.Value) return; // guard against double-call

            WinnerId.Value   = winnerId;
            MatchEnded.Value = true;

            Debug.Log($"[MatchManager] Match ended — winner: client {winnerId}");
        }
    }
}
