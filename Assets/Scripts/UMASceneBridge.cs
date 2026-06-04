using UnityEngine;
using UnityEngine.SceneManagement;
using UMA.CharacterSystem;

namespace MazeEscape
{
    public class UMASceneBridge : MonoBehaviour
    {
        private const string RecipePrefsKey = "UMA_PlayerRecipe";

        [SerializeField] private DynamicCharacterAvatar avatar;
        [SerializeField] private string gameSceneName = "[BB] Shooter MultiplayerSession";

        public void SaveAndPlay()
        {
            if (avatar == null)
            {
                Debug.LogWarning("[UMASceneBridge] No avatar assigned.");
                return;
            }
            string recipe = avatar.GetCurrentRecipe();
            PlayerPrefs.SetString(RecipePrefsKey, recipe);
            PlayerPrefs.Save();
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
