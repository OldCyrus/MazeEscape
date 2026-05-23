using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Shooter
{
    public class MeleeShooting : NetworkBehaviour, IShootingBehavior
    {
        [Header("Melee Settings")]
        [Tooltip("Radius of the hit-detection sphere in front of the player.")]
        [SerializeField] private float attackRange = 1.5f;
        [Tooltip("Seconds after swing start before hit detection fires (matches animation windup).")]
        [SerializeField] private float hitDelay = 0.35f;

        private Animator m_Animator;
        private NetworkAnimator m_NetworkAnimator;

        private static readonly int k_MeleeAttack = Animator.StringToHash("MeleeAttack");

        public override void OnNetworkSpawn()
        {
            var ownerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);
            if (ownerObj == null) return;
            m_Animator        = ownerObj.GetComponentInChildren<Animator>();
            m_NetworkAnimator = ownerObj.GetComponentInChildren<NetworkAnimator>();

            if (m_Animator != null)
            {
                int meleeLayerIndex = m_Animator.GetLayerIndex("Melee");
                if (meleeLayerIndex >= 0)
                    m_Animator.SetLayerWeight(meleeLayerIndex, 1f);
            }
        }

        public void Shoot(ShootingContext context)
        {
            if (m_NetworkAnimator != null)
                m_NetworkAnimator.SetTrigger(k_MeleeAttack);

            StartCoroutine(MeleeRoutine(context));
            context.OnAmmoConsumed?.Invoke(0);
        }

        public bool CanShoot() => true;
        public void UpdateShooting(Vector3 updatedDirection, float deltaTime) { }
        public void StopShooting() { }

        private IEnumerator MeleeRoutine(ShootingContext context)
        {
            yield return new WaitForSeconds(hitDelay);

            // Sphere centered at chest height, half a metre in front of the player.
            Vector3 origin = context.owner.transform.position
                           + Vector3.up
                           + context.owner.transform.forward * 0.5f;

            Collider[] hits = Physics.OverlapSphere(origin, attackRange, context.hitMask);
            var alreadyHit  = new HashSet<IHittable>();

            foreach (var col in hits)
            {
                if (col.transform.IsChildOf(context.owner.transform)) continue;
                var hittable = col.GetComponentInParent<IHittable>();
                if (hittable == null || alreadyHit.Contains(hittable)) continue;

                alreadyHit.Add(hittable);
                var hitInfo = new HitInfo
                {
                    amount      = context.damage,
                    hitPoint    = col.bounds.center,
                    hitNormal   = (col.transform.position - origin).normalized,
                    attackerId  = context.ownerClientId,
                    impactForce = context.owner.transform.forward * 5f,
                    isMelee     = true
                };
                hittable.OnHit(hitInfo);
                context.OnTargetHit?.Invoke(col.gameObject, hitInfo);
            }
        }
    }
}
