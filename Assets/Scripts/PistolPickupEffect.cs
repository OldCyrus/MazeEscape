using UnityEngine;
using System.Collections;
using Blocks.Gameplay.Core;
using Blocks.Gameplay.Shooter;

namespace MazeEscape
{
    public class PistolPickupEffect : MonoBehaviour, IInteractionEffect
    {
        [SerializeField] private GameObject pistolWeaponPrefab;

        public int Priority => 0;

        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            if (pistolWeaponPrefab == null)
                yield break;

            var weaponController = interactor.GetComponentInChildren<WeaponController>();
            if (weaponController == null)
                yield break;

            if (weaponController.HasWeaponPrefab(pistolWeaponPrefab))
                yield break;

            weaponController.AddWeapon(pistolWeaponPrefab);
            interactable.GetComponent<ModularInteractable>()?.RequestDespawn();
            yield return null;
        }

        public void CancelEffect(GameObject interactor) { }
    }
}
