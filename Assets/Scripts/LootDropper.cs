using UnityEngine;
using System.Collections.Generic;
using Blocks.Gameplay.Core;

/// <summary>
/// Attach to any enemy that has a CorePlayerState component.
/// Drops loot prefabs when the enemy's life state becomes Eliminated.
/// </summary>
public class LootDropper : MonoBehaviour
{
    [System.Serializable]
    public class LootEntry
    {
        public GameObject lootPrefab;
        [Range(0f, 100f)]
        public float dropChance = 50f;
        [Min(0f)]
        public float spawnHeightOffset = 0.5f;
    }

    [Header("Loot Table")]
    public List<LootEntry> lootTable = new List<LootEntry>();

    [Tooltip("Max number of items that can drop at once.")]
    [Min(1)]
    public int maxDrops = 2;

    [Header("Scatter")]
    [Tooltip("Dropped items scatter within this radius.")]
    public float scatterRadius = 0.8f;

    private CorePlayerState _playerState;

    private void Awake()
    {
        _playerState = GetComponentInChildren<CorePlayerState>();
        if (_playerState == null)
            Debug.LogWarning($"[LootDropper] No CorePlayerState found on {gameObject.name}.");
    }

    private void OnEnable()
    {
        if (_playerState != null)
            _playerState.OnLifeStateChanged += HandleLifeStateChanged;
    }

    private void OnDisable()
    {
        if (_playerState != null)
            _playerState.OnLifeStateChanged -= HandleLifeStateChanged;
    }

    private void HandleLifeStateChanged(PlayerLifeState newState)
    {
        if (newState == PlayerLifeState.Eliminated)
            DropLoot();
    }

    private void DropLoot()
    {
        int dropped = 0;

        List<LootEntry> shuffled = new List<LootEntry>(lootTable);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        foreach (LootEntry entry in shuffled)
        {
            if (dropped >= maxDrops) break;
            if (entry.lootPrefab == null) continue;

            if (Random.Range(0f, 100f) <= entry.dropChance)
            {
                Vector2 scatter = Random.insideUnitCircle * scatterRadius;
                Vector3 spawnPos = transform.position + new Vector3(scatter.x, entry.spawnHeightOffset, scatter.y);
                Instantiate(entry.lootPrefab, spawnPos, Quaternion.identity);
                dropped++;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, scatterRadius);
    }
#endif
}