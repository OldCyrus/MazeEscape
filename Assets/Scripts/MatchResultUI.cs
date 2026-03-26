using UnityEngine;
using Unity.Netcode;
using TMPro;

namespace MazeEscape
{
    /// <summary>
    /// Watches MatchManager's NetworkVariables and shows a full-screen result panel
    /// when the match ends. Pure display logic — no network calls.
    ///
    ///   Key carrier  → "You Win!"  (yellow)
    ///   Everyone else → "Game Over" (red)
    ///
    /// Attach to the MatchResultCanvas in the scene.
    /// </summary>
    public class MatchResultUI : MonoBehaviour
    {
        [SerializeField] private MatchManager      matchManager;
        [SerializeField] private GameObject        panel;
        [SerializeField] private TextMeshProUGUI   resultText;

        private bool _shown;

        private void Update()
        {
            if (_shown) return;
            if (matchManager == null || !matchManager.IsSpawned) return;
            if (!matchManager.MatchEnded.Value) return;

            _shown = true;
            panel?.SetActive(true);

            if (resultText == null) return;

            bool iWon = matchManager.WinnerId.Value == NetworkManager.Singleton.LocalClientId;
            resultText.text  = iWon ? "You Win!" : "Game Over";
            resultText.color = iWon ? Color.yellow : Color.red;
        }
    }
}
