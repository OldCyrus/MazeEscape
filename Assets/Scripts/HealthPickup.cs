using UnityEngine;
using Blocks.Gameplay.Core;

/// <summary>
/// Restores health to the player on pickup.
/// Uses CoreStatsHandler.ModifyStat with StatKeys.Health.
/// </summary>
public class HealthPickup : LootItem
{
    [Header("Health Settings")]
    [Tooltip("Amount of health to restore.")]
    public float healAmount = 25f;

    protected override bool OnPickup(GameObject player)
    {
        CoreStatsHandler stats = player.GetComponentInChildren<CoreStatsHandler>();
        if (stats == null) return false;

        // Don't pick up if already at full health
        float current = stats.GetCurrentValue(StatKeys.Health);
        float max = stats.GetMaxValue(StatKeys.Health);
        if (current >= max) return false;

        stats.ModifyStat(StatKeys.Health, healAmount, 0, ModificationSource.Healing);
        return true;
    }
}