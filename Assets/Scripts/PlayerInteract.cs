using UnityEngine;
using Blocks.Gameplay.Core;

/// <summary>
/// IPlayerAddon that listens for the Interact GameEvent and triggers pickup on any
/// LootItem within reach. Add this component to the player prefab alongside CoreInputHandler.
///
/// Wire-up:
///   - onInteractPressed → the same "OnInteractPressed" GameEvent asset wired into CoreInputHandler.
/// </summary>
public class PlayerInteract : MonoBehaviour, IPlayerAddon
{
    [Tooltip("The GameEvent raised by CoreInputHandler when the player presses the interact key (E). " +
             "Must match the same asset assigned to CoreInputHandler's On Interact Pressed field.")]
    [SerializeField] private GameEvent onInteractPressed;

    private const float PickupRadius = 1.5f;

    // ── IPlayerAddon ──────────────────────────────────────────────────────────

    public void Initialize(CorePlayerManager playerManager) { }

    public void OnPlayerSpawn()
    {
        if (onInteractPressed != null)
            onInteractPressed.RegisterListener(HandleInteract);
    }

    public void OnPlayerDespawn()
    {
        if (onInteractPressed != null)
            onInteractPressed.UnregisterListener(HandleInteract);
    }

    public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState) { }

    // ── Private ───────────────────────────────────────────────────────────────

    private void HandleInteract()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, PickupRadius,
                                                Physics.AllLayers, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            LootItem loot = hit.GetComponent<LootItem>();
            if (loot != null)
                loot.TryInteractPickup();
        }
    }
}
