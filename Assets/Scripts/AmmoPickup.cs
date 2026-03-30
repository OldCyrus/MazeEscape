using UnityEngine;
using Blocks.Gameplay.Shooter;

/// <summary>
/// Refills ammo for the player's current weapon on pickup.
/// Uses WeaponController and ModularWeapon from Blocks.Gameplay.Shooter.
/// </summary>
public class AmmoPickup : LootItem
{
    [Header("Ammo Settings")]
    [Tooltip("If set, only refills ammo if the player's current weapon matches this name.")]
    public string weaponName = "";

    protected override bool OnPickup(GameObject player)
    {
        WeaponController weaponController = player.GetComponentInChildren<WeaponController>();
        if (weaponController == null) return false;

        // IWeapon is the interface — cast to ModularWeapon to access GetWeaponName and NeedsReload
        ModularWeapon current = weaponController.CurrentWeapon as ModularWeapon;
        if (current == null) return false;

        // If a specific weapon name is required, check it matches
        if (!string.IsNullOrEmpty(weaponName))
        {
            if (current.GetWeaponName() != weaponName) return false;
        }

        // Only pick up if the weapon actually needs a reload
        if (!current.NeedsReload()) return false;

        current.TryReload();
        return true;
    }
}