using System.Collections.Generic;
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

        /// <summary>True once all connected players have reported ready. Clears the loading screen on all clients.</summary>
        public readonly NetworkVariable<bool> MatchReady = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly HashSet<ulong> m_ReadyClients = new HashSet<ulong>();

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
        /// Called by each client once their character is fully built and ready.
        /// Server sets MatchReady = true when all connected clients have reported.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ReportReadyServerRpc(RpcParams rpcParams = default)
        {
            if (MatchReady.Value) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!m_ReadyClients.Add(senderId)) return;

            int expected = NetworkManager.Singleton.ConnectedClients.Count;
            Debug.Log($"[MatchManager] Ready: {m_ReadyClients.Count}/{expected}");

            if (m_ReadyClients.Count >= expected)
            {
                MatchReady.Value = true;
                Debug.Log("[MatchManager] All players ready. Match starting.");
            }
        }

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
