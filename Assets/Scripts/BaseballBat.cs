using UnityEngine;
using Unity.Netcode;

/// <summary>
/// The baseball bat as a world pickup. Extends LootItem so it gets the
/// spinning/bobbing visual, trigger-only collider pickup, lifetime, and
/// sound/VFX behaviour for free.
///
/// When a player walks into it the server grants them a BaseballBatCarrier
/// component and destroys this pickup.
///
/// Place on a prefab that has:
///   - A child GameObject named "Visual" (any mesh — bat shape recommended)
///   - A Trigger Collider on the root (no physics collider, so players walk through)
///   - A NetworkObject component on the root
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class BaseballBat : LootItem
{
    [Tooltip("Optional prefab for the carried bat visual. Passed to the carrier on pickup.")]
    public GameObject batVisualPrefab;

    /// <summary>Called by BatSpawner after network-spawning to wire up the visual prefab.</summary>
    public void SetVisualPrefab(GameObject prefab)
    {
        batVisualPrefab = prefab;
    }
    // ── Ground-state drop ─────────────────────────────────────────────────────
    // Called by BaseballBatCarrier when the carrier dies, to re-enable the
    // pickup at the drop position before re-spawning this NetworkObject.
    // We re-use the existing LootItem lifetime so the bat eventually despawns
    // if nobody picks it up.

    protected override bool OnPickup(GameObject player)
    {
        // Only the server should grant the bat to avoid race conditions.
        // LootItem.OnTriggerEnter runs on all clients; NetworkObject authority
        // is on the server, so guard here.
        if (!NetworkManager.Singleton.IsServer) return false;

        // Don't let a player pick up a second bat if they already have one.
        if (player.GetComponent<BaseballBatCarrier>() != null) return false;

        // Grant the bat carrier to the player.
        var carrier = player.AddComponent<BaseballBatCarrier>();
        carrier.batVisualPrefab = batVisualPrefab;
        carrier.Initialize(gameObject);   // pass our prefab name for drop-respawn

        return true;  // LootItem will call PlayFXAndDestroy → Destroy(gameObject)
    }
}
