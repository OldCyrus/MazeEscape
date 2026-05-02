using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace MazeEscape
{
    /// <summary>
    /// IPlayerAddon that adds baseball bat melee attack on left mouse (PrimaryAction).
    /// Only acts when BatCarrier.Slot == InHand so it never conflicts with the gun.
    /// Animation is triggered via NetworkAnimator (synced to all clients).
    /// Hit detection runs on the owner client after a short windup delay and forwards
    /// damage server-authoritatively through the existing IHittable / HitProcessor chain.
    ///
    /// Inspector setup required:
    ///   - onPrimaryActionPressed : same GameEvent SO used by CoreInputHandler
    ///
    /// Animator setup required (CoreAnimator.controller):
    ///   - Trigger parameter named "MeleeAttack"
    ///   - Animation state using HumanM@Attack2H01 clip
    ///   - Transition from Any State with MeleeAttack trigger, Has Exit Time = false
    ///   - Transition out on Exit Time so locomotion resumes after the swing
    /// </summary>
    public class BatMeleeAddon : NetworkBehaviour, IPlayerAddon
    {
        [Header("Attack Settings")]
        [Tooltip("Damage dealt per hit.")]
        [SerializeField] private float damage = 15f;
        [Tooltip("Radius of the hit detection sphere at the moment of impact.")]
        [SerializeField] private float attackRange = 1.5f;
        [Tooltip("Seconds after the swing starts before hit detection fires (matches animation windup).")]
        [SerializeField] private float hitDelay = 0.35f;
        [Tooltip("Minimum seconds between attacks.")]
        [SerializeField] private float attackCooldown = 0.8f;

        [Header("Events")]
        [Tooltip("Same GameEvent SO wired to CoreInputHandler PrimaryAction.")]
        [SerializeField] private GameEvent onPrimaryActionPressed;

        private CorePlayerManager m_PlayerManager;
        private BatCarrier m_BatCarrier;
        private Animator m_Animator;
        private float m_NextAttackTime;

        private static readonly int k_AnimIDMeleeAttack = Animator.StringToHash("MeleeAttack");

        // ── IPlayerAddon ──────────────────────────────────────────────────────

        public void Initialize(CorePlayerManager playerManager)
        {
            m_PlayerManager = playerManager;
            m_BatCarrier    = playerManager.GetComponent<BatCarrier>();
            m_Animator      = playerManager.GetComponentInChildren<Animator>();
        }

        public void OnPlayerSpawn()
        {
            if (!m_PlayerManager.IsOwner) return;
            onPrimaryActionPressed?.RegisterListener(HandleAttackInput);
        }

        public void OnPlayerDespawn()
        {
            if (m_PlayerManager == null || !m_PlayerManager.IsOwner) return;
            onPrimaryActionPressed?.UnregisterListener(HandleAttackInput);
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState) { }

        // ── Private ───────────────────────────────────────────────────────────

        private void HandleAttackInput()
        {
            if (m_BatCarrier == null || m_BatCarrier.Slot != BatSlot.InHand) return;
            if (Time.time < m_NextAttackTime) return;

            m_NextAttackTime = Time.time + attackCooldown;

            if (m_Animator != null)
                m_Animator.SetTrigger(k_AnimIDMeleeAttack);

            StartCoroutine(DetectHitAfterDelay());
        }

        private IEnumerator DetectHitAfterDelay()
        {
            yield return new WaitForSeconds(hitDelay);

            // Sphere centered at chest height, half a unit in front of the player
            Vector3 origin = transform.position + Vector3.up * 1f + transform.forward * 0.5f;
            Collider[] hits = Physics.OverlapSphere(origin, attackRange);

            // Use a set to ensure each target is only hit once even if it has multiple colliders
            var alreadyHit = new HashSet<IHittable>();

            foreach (var col in hits)
            {
                // Never hit self
                if (col.transform.IsChildOf(transform) || col.gameObject == gameObject) continue;

                var hittable = col.GetComponentInParent<IHittable>();
                if (hittable == null || alreadyHit.Contains(hittable)) continue;

                alreadyHit.Add(hittable);
                hittable.OnHit(new HitInfo
                {
                    amount      = damage,
                    hitPoint    = col.bounds.center,
                    hitNormal   = (col.transform.position - origin).normalized,
                    attackerId  = OwnerClientId,
                    impactForce = transform.forward * 5f
                });
            }
        }
    }
}
