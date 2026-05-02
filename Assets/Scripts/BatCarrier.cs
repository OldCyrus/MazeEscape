using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Shooter;

namespace MazeEscape
{
    public enum BatSlot : byte { None, InHand, OnBack }

    /// <summary>
    /// Tracks whether this player is carrying a bat and where it is attached.
    /// Add to the player prefab alongside WeaponController.
    ///
    /// Inspector setup required:
    ///   - handAttachPoint  : the right-hand bone Transform in the character rig
    ///   - spineAttachPoint : the spine/back bone Transform in the character rig
    ///   - batVisual        : a bat mesh child of the player prefab (inactive by default)
    ///   - onWeaponChanged  : the same WeaponSwapEvent SO wired into WeaponController
    /// </summary>
    public class BatCarrier : NetworkBehaviour
    {
        [Header("Attach Points")]
        [Tooltip("Right hand bone — bat rests here when carried.")]
        [SerializeField] private Transform handAttachPoint;
        [Tooltip("Spine/back bone — bat rests here when a gun is in hand.")]
        [SerializeField] private Transform spineAttachPoint;

        [Header("Bat Visual")]
        [Tooltip("Bat mesh child on the player prefab. Inactive until a bat is picked up.")]
        [SerializeField] private GameObject batVisual;

        [Header("Events")]
        [Tooltip("The same WeaponSwapEvent ScriptableObject wired into WeaponController.")]
        [SerializeField] private WeaponSwapEvent onWeaponChanged;

        private readonly NetworkVariable<BatSlot> m_Slot = new NetworkVariable<BatSlot>(
            BatSlot.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool HasBat => m_Slot.Value != BatSlot.None;
        public BatSlot Slot  => m_Slot.Value;

        // ── Network lifecycle ─────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            m_Slot.OnValueChanged += OnSlotChanged;
            // Apply current state immediately for late-joining clients
            OnSlotChanged(BatSlot.None, m_Slot.Value);

            // Only the local owner receives weapon-swap events
            if (IsOwner)
                onWeaponChanged?.RegisterListener(OnWeaponSwapped);
        }

        public override void OnNetworkDespawn()
        {
            m_Slot.OnValueChanged -= OnSlotChanged;

            if (IsOwner)
                onWeaponChanged?.UnregisterListener(OnWeaponSwapped);
        }

        // ── Public API (called by BatPickupEffect on the owner client) ────────

        public void GrantBat()
        {
            if (HasBat) return;
            GrantBatRpc();
        }

        // ── RPCs ──────────────────────────────────────────────────────────────

        [Rpc(SendTo.Server)]
        private void GrantBatRpc()
        {
            if (HasBat) return;
            m_Slot.Value = BatSlot.InHand;
        }

        [Rpc(SendTo.Server)]
        private void MoveBatRpc(BatSlot targetSlot)
        {
            if (!HasBat) return;
            m_Slot.Value = targetSlot;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnWeaponSwapped(WeaponSwapPayload payload)
        {
            if (!HasBat) return;
            // Bat goes to back when a gun is active, returns to hand when no gun
            MoveBatRpc(payload.NewWeapon != null ? BatSlot.OnBack : BatSlot.InHand);
        }

        private void OnSlotChanged(BatSlot previous, BatSlot current)
        {
            if (batVisual == null) return;

            switch (current)
            {
                case BatSlot.None:
                    batVisual.SetActive(false);
                    break;

                case BatSlot.InHand:
                    if (handAttachPoint != null)
                        batVisual.transform.SetParent(handAttachPoint, false);
                    batVisual.SetActive(true);
                    break;

                case BatSlot.OnBack:
                    if (spineAttachPoint != null)
                        batVisual.transform.SetParent(spineAttachPoint, false);
                    batVisual.SetActive(true);
                    break;
            }
        }
    }
}
