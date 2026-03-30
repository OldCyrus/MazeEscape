using UnityEngine;
using Blocks.Gameplay.Shooter;

/// <summary>
/// Weapon pickup placeholder.
///
/// NOTE: Your project uses a fully networked weapon system (WeaponController is a NetworkBehaviour,
/// weapons are spawned as network objects). Granting a new weapon to a player at runtime requires
/// a server-authoritative spawn via NetworkManager, which needs to be handled inside your existing
/// game flow (e.g. through GameManager or a dedicated server RPC).
///
/// This script detects the pickup client-side and logs it. Wire OnWeaponPickedUp into your
/// server-side weapon granting logic to complete the flow.
/// </summary>
public class WeaponPickup : LootItem
{
    [Header("Weapon Settings")]
    [Tooltip("The name of the weapon to grant. Must match WeaponData.weaponName in your project.")]
    public string weaponName = "";

    /// <summary>
    /// Subscribe to this from your server-side weapon granting code.
    /// Passes the player GameObject that touched the pickup.
    /// </summary>
    public System.Action<GameObject, string> OnWeaponPickedUp;

    protected override bool OnPickup(GameObject player)
    {
        WeaponController weaponController = player.GetComponentInChildren<WeaponController>();
        if (weaponController == null) return false;

        // Check if the player already has this weapon
        if (weaponController.HasWeaponPrefab(gameObject))
        {
            Debug.Log($"[WeaponPickup] Player already has {weaponName}.");
            return false;
        }

        OnWeaponPickedUp?.Invoke(player, weaponName);
        Debug.Log($"[WeaponPickup] Player picked up weapon: {weaponName}. Route this through your server weapon grant logic.");
        return true;
    }
}