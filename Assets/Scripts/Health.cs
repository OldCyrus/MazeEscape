using UnityEngine;
using System;

namespace Blocks.Gameplay.Shooter
{
    /// <summary>
    /// Manages health for any GameObject (players, enemies, destructibles).
    /// Subscribe to OnDie to react to death. Used by LootDropper and HealthPickup.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("Health Settings")]
        public float MaxHealth = 100f;

        [Tooltip("If true, the GameObject is destroyed when health reaches zero.")]
        public bool destroyOnDeath = false;

        public float CurrentHealth { get; private set; }

        public bool IsDead { get; private set; }

        /// <summary>Fired once when health reaches zero.</summary>
        public event Action OnDie;

        /// <summary>Fired whenever health changes. Passes (currentHealth, maxHealth).</summary>
        public event Action<float, float> OnHealthChanged;

        private void Awake()
        {
            CurrentHealth = MaxHealth;
        }

        /// <summary>Deal damage to this object.</summary>
        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0f)
                Die();
        }

        /// <summary>Restore health up to MaxHealth.</summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>Kill this object immediately regardless of current health.</summary>
        public void Kill()
        {
            if (IsDead) return;
            CurrentHealth = 0f;
            Die();
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;

            OnDie?.Invoke();

            if (destroyOnDeath)
                Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Show a health bar gizmo above the object in the editor
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"HP: {CurrentHealth}/{MaxHealth}"
            );
        }
#endif
    }
}
