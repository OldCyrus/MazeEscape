using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace MazeEscape
{
    /// <summary>
    /// Attached to the ExitDoor prefab.
    ///
    /// - Sets the door's box collider to trigger so players can pass through.
    /// - Shows a golden emissive glow visible ONLY to the local client carrying the key.
    /// - When the key carrier reaches the door, the server declares them winner.
    /// </summary>
    public class ExitDoor : NetworkBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────
        private static readonly Color GoldColor    = new Color(1f, 0.82f, 0f);
        private static readonly Color GoldEmission = new Color(1f, 0.82f, 0f) * 4f;

        // ── Private refs ──────────────────────────────────────────────────────
        private MeshRenderer _meshRenderer;
        private Material     _originalMaterial;
        private Material     _glowMaterial;
        private bool         _glowActive;
        private bool         _subscribed;

        // ── Awake ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            if (col != null)
                col.isTrigger = true;
            else
                Debug.LogWarning("[ExitDoor] No BoxCollider found.");

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer != null)
            {
                _originalMaterial = _meshRenderer.sharedMaterial;
                _glowMaterial     = new Material(_originalMaterial);
                _glowMaterial.EnableKeyword("_EMISSION");
                _glowMaterial.SetColor("_EmissionColor", GoldEmission);
                _glowMaterial.color = GoldColor;
            }

            SetGlowVisible(false);
        }

        // ── Network spawn ─────────────────────────────────────────────────────
        public override void OnNetworkSpawn()
        {
            if (NetworkPickupKey.Instance != null)
                NetworkPickupKey.Instance.CarrierClientId.OnValueChanged += OnCarrierChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkPickupKey.Instance != null)
                NetworkPickupKey.Instance.CarrierClientId.OnValueChanged -= OnCarrierChanged;
        }

        // ── Update: poll in case NetworkPickupKey spawned after this object ───
        private void Update()
        {
            if (!_subscribed && NetworkPickupKey.Instance != null)
            {
                NetworkPickupKey.Instance.CarrierClientId.OnValueChanged += OnCarrierChanged;
                OnCarrierChanged(NetworkPickupKey.NoCarrier, NetworkPickupKey.Instance.CarrierClientId.Value);
                _subscribed = true;
            }
        }

        // ── Carrier changed ───────────────────────────────────────────────────
        private void OnCarrierChanged(ulong prev, ulong next)
        {
            bool iAmCarrier = next != NetworkPickupKey.NoCarrier
                              && next == NetworkManager.Singleton.LocalClientId;
            SetGlowVisible(iAmCarrier);
        }

        // ── Server: win condition trigger ─────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            var playerState = other.GetComponentInParent<CorePlayerState>();
            if (playerState == null || !playerState.IsActive) return;

            if (NetworkPickupKey.Instance == null) return;
            if (NetworkPickupKey.Instance.CarrierClientId.Value != playerState.OwnerClientId) return;

            if (MatchManager.Instance == null) return;
            if (MatchManager.Instance.MatchEnded.Value) return;

            Debug.Log($"[ExitDoor] Player {playerState.OwnerClientId} escaped with the key — they win!");
            MatchManager.Instance.EndMatch(playerState.OwnerClientId);
        }

        // ── Glow toggle ───────────────────────────────────────────────────────
        private void SetGlowVisible(bool visible)
        {
            if (_glowActive == visible) return;
            _glowActive = visible;

            if (_meshRenderer != null)
                _meshRenderer.material = visible ? _glowMaterial : _originalMaterial;
        }
    }
}
