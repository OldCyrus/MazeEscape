using UMA.CharacterSystem;

namespace UMA
{

    public interface IItemSelector
    {
        public void SetItem(UMAWardrobeRecipe item);
        public void ClearSlot(string slotName);
    }
}