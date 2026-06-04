using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UMA;
using UMA.CharacterSystem;

namespace MazeEscape
{
    /// <summary>
    /// Drives the character creation screen.
    ///
    /// Flow:
    ///   1. Player opens this scene from the main menu / lobby.
    ///   2. A DynamicCharacterAvatar previews the character in real time.
    ///   3. Player picks race, wardrobe slots, and skin colour.
    ///   4. "Play" saves the recipe to PlayerPrefs and loads the game scene.
    ///
    /// Wiring checklist (Inspector):
    ///   - avatar            : the DynamicCharacterAvatar in the scene
    ///   - wardrobeContainer : a ScrollView Content transform — wardrobe buttons go here
    ///   - wardrobeButtonPrefab : a prefab with a Button + Text child
    ///   - slotButtonsParent : parent holding per-slot tab buttons (auto-populated)
    ///   - maleButton / femaleButton
    ///   - skinColorContainer : parent for skin-colour buttons (auto-populated)
    ///   - colorButtonPrefab  : prefab with a Button; button image colour = swatch
    ///   - gameSceneName     : exact scene name to load on "Play"
    ///   - playButton
    ///   - statusText        : optional Text for "Building…" feedback
    /// </summary>
    public class CharacterCreationManager : MonoBehaviour
    {
        private const string RecipePrefsKey = "UMA_PlayerRecipe";

        [Header("UMA")]
        [SerializeField] private DynamicCharacterAvatar avatar;

        [Header("Wardrobe UI")]
        [SerializeField] private Transform wardrobeContainer;
        [SerializeField] private GameObject wardrobeButtonPrefab;
        [SerializeField] private Transform slotTabsParent;
        [SerializeField] private GameObject slotTabPrefab;

        [Header("Skin Colour UI")]
        [SerializeField] private Transform skinColorContainer;
        [SerializeField] private GameObject colorButtonPrefab;

        [Header("Race Buttons")]
        [SerializeField] private Button maleButton;
        [SerializeField] private Button femaleButton;

        [Header("Navigation")]
        [SerializeField] private Button playButton;
        [SerializeField] private Text statusText;
        [SerializeField] private string gameSceneName = "[BB] Shooter MultiplayerSession";

        // Skin colour swatches — edit these in the Inspector to match your content
        [Header("Skin Colours")]
        [SerializeField] private List<Color> skinColours = new List<Color>
        {
            new Color(1.00f, 0.87f, 0.76f),
            new Color(0.94f, 0.76f, 0.62f),
            new Color(0.85f, 0.65f, 0.46f),
            new Color(0.70f, 0.50f, 0.33f),
            new Color(0.50f, 0.33f, 0.20f),
            new Color(0.30f, 0.18f, 0.10f),
        };

        private bool m_AvatarReady;
        private string m_CurrentSlot;
        private List<GameObject> m_SpawnedWardrobeButtons = new List<GameObject>();

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            SetStatus("Building character…");

            if (playButton != null) playButton.interactable = false;
            if (maleButton != null) maleButton.onClick.AddListener(() => SelectRace("HumanMale"));
            if (femaleButton != null) femaleButton.onClick.AddListener(() => SelectRace("HumanFemale"));
            if (playButton != null) playButton.onClick.AddListener(SaveAndPlay);

            if (avatar != null)
            {
                avatar.CharacterCreated.AddListener(OnCharacterCreated);
                avatar.CharacterUpdated.AddListener(OnCharacterUpdated);
            }
        }

        private void OnDestroy()
        {
            if (avatar != null)
            {
                avatar.CharacterCreated.RemoveListener(OnCharacterCreated);
                avatar.CharacterUpdated.RemoveListener(OnCharacterUpdated);
            }
        }

        // ── UMA Callbacks ─────────────────────────────────────────────────────

        private void OnCharacterCreated(UMAData umaData)
        {
            m_AvatarReady = true;
            SetStatus(string.Empty);
            if (playButton != null) playButton.interactable = true;
            if (avatar.AvailableRecipes == null || avatar.AvailableRecipes.Count == 0)
                StartCoroutine(WaitForRecipesThenBuild());
            else
            {
                BuildSlotTabs();
                BuildSkinColourButtons();
            }
        }

        private void OnCharacterUpdated(UMAData umaData)
        {
            // Rebuild slot tabs if they were deferred because recipes weren't ready yet.
            if (m_AvatarReady && slotTabsParent != null && slotTabsParent.childCount == 0
                && avatar.AvailableRecipes != null && avatar.AvailableRecipes.Count > 0)
            {
                BuildSlotTabs();
            }
        }

        private IEnumerator WaitForRecipesThenBuild()
        {
            // Poll until the DynamicCharacterSystem has loaded wardrobe recipes.
            float timeout = 5f;
            float elapsed = 0f;
            while ((avatar.AvailableRecipes == null || avatar.AvailableRecipes.Count == 0) && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            BuildSlotTabs();
            BuildSkinColourButtons();
        }

        // ── Race Selection ────────────────────────────────────────────────────

        public void SelectRace(string raceName)
        {
            if (avatar == null) return;
            m_AvatarReady = false;
            SetStatus("Building character…");
            if (playButton != null) playButton.interactable = false;
            ClearWardrobeButtons();
            avatar.ChangeRace(raceName);
        }

        // ── Wardrobe ──────────────────────────────────────────────────────────

        /// <summary>Builds one tab button per wardrobe slot available on the current race.</summary>
        private void BuildSlotTabs()
        {
            if (slotTabsParent == null || slotTabPrefab == null) return;

            foreach (Transform child in slotTabsParent)
                Destroy(child.gameObject);

            if (avatar.AvailableRecipes == null || avatar.AvailableRecipes.Count == 0) return;

            bool first = true;
            foreach (var slot in avatar.AvailableRecipes.Keys)
            {
                string capturedSlot = slot;
                var tab = Instantiate(slotTabPrefab, slotTabsParent);
                tab.SetActive(true);
                var label = tab.GetComponentInChildren<Text>();
                if (label != null) label.text = slot;
                var btn = tab.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => ShowSlot(capturedSlot));

                if (first) { ShowSlot(slot); first = false; }
            }
        }

        /// <summary>Populates the wardrobe container with buttons for a specific slot.</summary>
        public void ShowSlot(string slotName)
        {
            if (avatar == null || avatar.AvailableRecipes == null) return;
            m_CurrentSlot = slotName;
            ClearWardrobeButtons();

            if (!avatar.AvailableRecipes.TryGetValue(slotName, out var recipes)) return;

            // "None" button — clears the slot
            SpawnWardrobeButton("None", () =>
            {
                avatar.ClearSlot(slotName);
                avatar.BuildCharacter(true);
            });

            foreach (var recipe in recipes)
            {
                var captured = recipe;
                SpawnWardrobeButton(recipe.DisplayValue, () =>
                {
                    avatar.SetSlot(captured);
                    avatar.BuildCharacter(true);
                });
            }
        }

        private void SpawnWardrobeButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            if (wardrobeButtonPrefab == null || wardrobeContainer == null) return;
            var go = Instantiate(wardrobeButtonPrefab, wardrobeContainer);
            go.SetActive(true);
            m_SpawnedWardrobeButtons.Add(go);
            var text = go.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
            var btn = go.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(onClick);
        }

        private void ClearWardrobeButtons()
        {
            foreach (var b in m_SpawnedWardrobeButtons)
                if (b != null) Destroy(b);
            m_SpawnedWardrobeButtons.Clear();
        }

        // ── Skin Colour ───────────────────────────────────────────────────────

        private void BuildSkinColourButtons()
        {
            if (skinColorContainer == null || colorButtonPrefab == null) return;

            foreach (Transform child in skinColorContainer)
                Destroy(child.gameObject);

            foreach (var colour in skinColours)
            {
                var captured = colour;
                var go = Instantiate(colorButtonPrefab, skinColorContainer);
                go.SetActive(true);
                var img = go.GetComponent<Image>();
                if (img != null) img.color = captured;
                var btn = go.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => ApplySkinColour(captured));
            }
        }

        private void ApplySkinColour(Color colour)
        {
            if (avatar == null) return;
            // OverlayColorData(3) = albedo / metallic / emission channels
            var colorData = new OverlayColorData(3);
            colorData.color = colour;
            // "Skin" is the standard shared colour name in the default UMA human races.
            avatar.SetColor("Skin", colorData);
            avatar.BuildCharacter(true);
        }

        // ── Save & Play ───────────────────────────────────────────────────────

        public void SaveAndPlay()
        {
            if (avatar == null)
            {
                Debug.LogWarning("[CharacterCreationManager] No avatar assigned.");
                return;
            }

            string recipe = avatar.GetCurrentRecipe();
            PlayerPrefs.SetString(RecipePrefsKey, recipe);
            PlayerPrefs.Save();

            Debug.Log("[CharacterCreationManager] Recipe saved. Loading game scene.");
            SceneManager.LoadScene(gameSceneName);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}
