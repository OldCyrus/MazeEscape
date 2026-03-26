using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Blocks.Gameplay.Core;

namespace MazeEscape
{
    /// <summary>
    /// Networked key pickup object. Placed in the bottom-right corner of the maze.
    ///
    /// Server authority:
    ///   - Owns pickup/drop decisions.
    ///   - Monitors the carrier's life state and drops on death.
    ///   - Moves the NetworkObject's transform to the drop position (synced via NetworkTransform).
    ///
    /// All clients:
    ///   - Watch CarrierClientId NetworkVariable.
    ///   - Show/hide ground key visual and waist attachment accordingly.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public class NetworkPickupKey : NetworkBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static NetworkPickupKey Instance { get; private set; }

        // ── Constants ─────────────────────────────────────────────────────────
        public const ulong NoCarrier = ulong.MaxValue;
        private static readonly Color GoldColor    = new Color(1f, 0.82f, 0f);
        private const float           EmissionMult = 3f;

        // ── Synced state ──────────────────────────────────────────────────────
        /// <summary>ulong.MaxValue means the key is on the ground.</summary>
        public readonly NetworkVariable<ulong> CarrierClientId = new NetworkVariable<ulong>(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ── Server-only ───────────────────────────────────────────────────────
        private CorePlayerState _trackedCarrierState;

        // ── Visuals (client-side, not networked) ──────────────────────────────
        private GameObject    _groundRoot;    // shown on ground, rotates
        private GameObject    _waistRoot;     // follows the carrier's waist
        private Transform     _carrierXform;  // cached each time carrier changes
        private SphereCollider _trigger;

        // ── Spawn position ────────────────────────────────────────────────────
        // Bottom-right corner: MazeGenerator at (3.7, 0, -16.3), rows=14, cols=21, cellSize=2
        // Cell centre (row=0, col=20) = (3.7 + 20*2, 0.5, -16.3) = (43.7, 0.5, -16.3)
        private static readonly Vector3 SpawnPosition = new Vector3(43.7f, 0.5f, -16.3f);

        // ── Awake ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            BuildVisuals();

            _trigger        = gameObject.AddComponent<SphereCollider>();
            _trigger.radius = 1f;
            _trigger.isTrigger = true;

            var rb          = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic  = true;
            rb.useGravity   = false;
        }

        // ── Start: deferred positioning ───────────────────────────────────────
        // Wait one frame so MazeRenderer.Start() has finished generating the maze
        // before we place the key. Unity supports IEnumerator Start() natively.

        private System.Collections.IEnumerator Start()
        {
            yield return null;
            transform.position = SpawnPosition;
        }

        // ── Network spawn ─────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            Instance = this;
            CarrierClientId.OnValueChanged += OnCarrierChanged;
            // Apply current state for late-joining clients
            OnCarrierChanged(NoCarrier, CarrierClientId.Value);
        }

        public override void OnNetworkDespawn()
        {
            CarrierClientId.OnValueChanged -= OnCarrierChanged;
            UnsubscribeCarrierDeath();
            if (Instance == this) Instance = null;
        }

        // ── Update: visuals ───────────────────────────────────────────────────

        private void Update()
        {
            // Spin the ground key so it catches the player's eye
            if (_groundRoot != null && _groundRoot.activeSelf)
                _groundRoot.transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

            // Waist key: follow the carrier's transform (client-side, not networked)
            if (_waistRoot != null && _waistRoot.activeSelf)
            {
                // Lazy-resolve carrier transform in case it wasn't ready when carrier changed
                if (_carrierXform == null && CarrierClientId.Value != NoCarrier)
                    _carrierXform = FindPlayerTransform(CarrierClientId.Value);

                if (_carrierXform != null)
                {
                    _waistRoot.transform.position =
                        _carrierXform.position
                        + _carrierXform.right * 0.35f
                        + Vector3.up        * 0.85f;
                    _waistRoot.transform.rotation = _carrierXform.rotation;
                }
            }
        }

        // ── Server: trigger pickup ────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (CarrierClientId.Value != NoCarrier) return; // already carried

            var playerState = other.GetComponentInParent<CorePlayerState>();
            if (playerState == null || !playerState.IsActive) return;

            PickUpBy(playerState.OwnerClientId);
        }

        // ── Server: internal pickup ───────────────────────────────────────────

        private void PickUpBy(ulong clientId)
        {
            _trigger.enabled = false; // prevent double-pickup

            UnsubscribeCarrierDeath();
            CarrierClientId.Value = clientId;

            // Watch for the carrier dying
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                _trackedCarrierState = client.PlayerObject?.GetComponent<CorePlayerState>();
                if (_trackedCarrierState != null)
                    _trackedCarrierState.OnLifeStateChanged += OnCarrierLifeStateChanged;
            }
        }

        // ── Server: drop ──────────────────────────────────────────────────────

        /// <summary>Called by the server to drop the key at a given world position.</summary>
        public void DropKey(Vector3 worldPosition)
        {
            if (!IsServer) return;

            UnsubscribeCarrierDeath();
            transform.position    = worldPosition; // NetworkTransform syncs this
            CarrierClientId.Value = NoCarrier;
            _trigger.enabled      = true;
        }

        // ── Server: carrier death ─────────────────────────────────────────────

        private void OnCarrierLifeStateChanged(PlayerLifeState newState)
        {
            if (newState != PlayerLifeState.Eliminated) return;

            // Find where the carrier died and drop the key there
            Vector3 dropPos = transform.position; // fallback
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(CarrierClientId.Value, out var client)
                && client.PlayerObject != null)
            {
                dropPos = client.PlayerObject.transform.position;
            }

            DropKey(dropPos);
        }

        private void UnsubscribeCarrierDeath()
        {
            if (_trackedCarrierState != null)
            {
                _trackedCarrierState.OnLifeStateChanged -= OnCarrierLifeStateChanged;
                _trackedCarrierState = null;
            }
        }

        // ── All clients: carrier changed ──────────────────────────────────────

        private void OnCarrierChanged(ulong prev, ulong next)
        {
            bool isCarried = next != NoCarrier;

            _groundRoot?.SetActive(!isCarried);
            _waistRoot?.SetActive(isCarried);

            _carrierXform = isCarried ? FindPlayerTransform(next) : null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Transform FindPlayerTransform(ulong clientId)
        {
            foreach (var no in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                if (no.OwnerClientId == clientId && no.GetComponent<CorePlayerState>() != null)
                    return no.transform;
            }
            return null;
        }

        // ── Visual construction ───────────────────────────────────────────────

        private void BuildVisuals()
        {
            _groundRoot = new GameObject("GroundKey");
            _groundRoot.transform.SetParent(transform);
            _groundRoot.transform.localPosition = Vector3.zero;
            BuildKeyShape(_groundRoot.transform, 1f);

            _waistRoot = new GameObject("WaistKey");
            _waistRoot.transform.SetParent(transform);
            _waistRoot.transform.localPosition = Vector3.zero;
            BuildKeyShape(_waistRoot.transform, 0.4f);
            _waistRoot.SetActive(false);
        }

        private void BuildKeyShape(Transform root, float scale)
        {
            // Flat disc = key head/ring
            AddPart(root, "Ring",
                Vector3.up * (0.9f * scale),
                new Vector3(0.25f * scale, 0.04f * scale, 0.25f * scale));

            // Shaft / blade
            AddPart(root, "Shaft",
                Vector3.up * (0.5f * scale),
                new Vector3(0.07f * scale, 0.25f * scale, 0.07f * scale));

            // Two teeth
            AddPart(root, "Tooth1",
                new Vector3(0.13f * scale, 0.34f * scale, 0f),
                new Vector3(0.055f * scale, 0.09f * scale, 0.055f * scale));

            AddPart(root, "Tooth2",
                new Vector3(0.13f * scale, 0.19f * scale, 0f),
                new Vector3(0.055f * scale, 0.07f * scale, 0.055f * scale));
        }

        private void AddPart(Transform parent, string partName, Vector3 localPos, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = partName;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;

            // Remove the auto-added capsule collider immediately — visual only
            Object.DestroyImmediate(go.GetComponent<Collider>());

            ApplyGoldMaterial(go.GetComponent<Renderer>());
        }

        private static void ApplyGoldMaterial(Renderer rend)
        {
            if (rend == null) return;
            var mat = new Material(rend.sharedMaterial) { color = GoldColor };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", GoldColor * EmissionMult);
            rend.material = mat;
        }
    }
}
