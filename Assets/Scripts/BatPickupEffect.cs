using UnityEngine;
using System.Collections;
using Blocks.Gameplay.Core;

namespace MazeEscape
{
    /// <summary>
    /// IInteractionEffect for the baseball bat pickup.
    /// Add this alongside ModularInteractable on the BaseballBat prefab.
    /// If the touching player has no bat, grants them one via BatCarrier.
    /// </summary>
    public class BatPickupEffect : MonoBehaviour, IInteractionEffect
    {
        public int Priority => 0;

        public IEnumerator ApplyEffect(GameObject interactor, GameObject interactable)
        {
            var carrier = interactor.GetComponent<BatCarrier>();
            if (carrier == null || carrier.HasBat)
                yield break;

            carrier.GrantBat();
            interactable.GetComponent<ModularInteractable>()?.RequestDespawn();
            yield return null;
        }

        public void CancelEffect(GameObject interactor) { }
    }
}
