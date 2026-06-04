using UMA.CharacterSystem;
using UnityEngine;
using UnityEngine.UI;

namespace UMA
{

    public class ItemEffector : MonoBehaviour
    {
        public IItemSelector itemSelector;
        public UMAWardrobeRecipe recipe;
        private string clearSlotName;

        public void Setup(IItemSelector itemSelector, UMAWardrobeRecipe recipe)
        {
            this.itemSelector = itemSelector;
            this.recipe = recipe;

            Image[] img = GetComponentsInChildren<Image>();

            bool imageSet = false;
            if (recipe.wardrobeRecipeThumbs.Count > 0)
            {
                for (int i = 0; i < img.Length; i++)
                {
                    if (img[i].name == "ItemImage")
                    {
                        var thumb = recipe.wardrobeRecipeThumbs[0].thumb;
                        img[i].sprite = thumb;
                        if (thumb != null)
                            imageSet = true;
                    }
                }
            }

            Text text = GetComponentInChildren<Text>();
            if (text != null)
            {
                if (!imageSet)
                {
                    string itemName = recipe.name;
                    if (!string.IsNullOrEmpty(recipe.DisplayValue))
                    {
                        itemName = recipe.DisplayValue;
                    }
                    text.text = itemName.Substring(0, Mathf.Min(12, itemName.Length));
                }
                else
                {
                    text.text = "";
                }
            }
        }

        public void SetupClear(IItemSelector itemSelector, string slotName)
        {
            this.itemSelector = itemSelector;
            this.clearSlotName = slotName;
            Text text = GetComponentInChildren<Text>();
            if (text != null)
                text.text = slotName == "Beard" ? "Clean Shaven" : "None";
        }

        public void ImageClicked()
        {
            if (recipe == null)
                itemSelector.ClearSlot(clearSlotName);
            else
                itemSelector.SetItem(recipe);
        }
    }
}