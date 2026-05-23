using UnityEngine;
using System.Collections;
using Blocks.Gameplay.Core;
using Blocks.Gameplay.Shooter;

namespace MazeEscape
{
    /// <summary>
    /// IInteractionEffect for the baseball bat pickup.
    /// Grants the bat weapon prefab to the interacting player via WeaponController.AddWeapon.
    /// Wire this alongside ModularInteractable on the bat pickup prefab.
    /// </summary>
    public class BatWeaponPickupEffect : MonoBehaviour, IInteractionEffect
    {
        [Tooltip("The bat weapon prefab (NetworkObject with ModularWeapon + MeleeShooting).")]
        [SerializeField] private GameObject batWeaponPrefab;

        public int Priority => 0;

        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (batWeaponPrefab == null)
                yield break;

            var weaponController = interactor.GetComponentInChildren<WeaponController>();
            if (weaponController == null)
                yield break;

            if (weaponController.HasWeaponPrefab(batWeaponPrefab))
                yield break;

            weaponController.AddWeapon(batWeaponPrefab);
            interactable.GetComponent<ModularInteractable>()?.RequestDespawn();
            yield return null;
        }

        public void CancelEffect(GameObject interactor) { }
    }
}
