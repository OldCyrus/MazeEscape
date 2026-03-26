using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace MazeEscape
{
    /// <summary>
    /// Manages the pre-game countdown timer. Runs server-side; state is synced to
    /// all clients via NetworkVariables so every player sees the same numbers.
    ///
    /// Controls:
    ///   H key  — host only, starts the 10-second countdown (testing shortcut;
    ///             later replaced with auto-trigger when 8 players connect).
    /// </summary>
    public class CountdownController : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float countdownDuration = 10f;

        [Header("References")]
        [SerializeField] private PrisonBarDoor prisonDoor;

        // ── Synced state (server writes, everyone reads) ───────────────────────
        public NetworkVariable<float> TimeRemaining = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsCountingDown = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> CountdownComplete = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ── Update ─────────────────────────────────────────────────────────────

        private void Update()
        {
            // H key — host only, guard against double-trigger
            if (IsHost && !IsCountingDown.Value && !CountdownComplete.Value)
            {
                if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                    StartCountdownServerRpc();
            }

            // Tick down — server authority
            if (IsServer && IsCountingDown.Value)
            {
                TimeRemaining.Value -= Time.deltaTime;

                if (TimeRemaining.Value <= 0f)
                {
                    TimeRemaining.Value    = 0f;
                    IsCountingDown.Value   = false;
                    CountdownComplete.Value = true;

                    if (prisonDoor != null)
                        prisonDoor.OpenDoor();
                }
            }
        }

        // ── ServerRpc ──────────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        private void StartCountdownServerRpc()
        {
            if (IsCountingDown.Value || CountdownComplete.Value) return;

            TimeRemaining.Value  = countdownDuration;
            IsCountingDown.Value = true;

            Debug.Log("[CountdownController] Countdown started.");
        }
    }
}
