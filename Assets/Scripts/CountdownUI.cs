using UnityEngine;
using TMPro;
using System.Collections;

namespace MazeEscape
{
    /// <summary>
    /// Reads CountdownController's NetworkVariables each frame and updates the
    /// local screen-space countdown display. Pure display logic — no network calls.
    /// Attach to the CountdownCanvas in the scene.
    /// </summary>
    public class CountdownUI : MonoBehaviour
    {
        [SerializeField] private CountdownController controller;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private GameObject panel;

        [Header("GO! Flash")]
        [SerializeField] private float goDuration = 2f;    // seconds to show "GO!"

        private bool _goShown;

        // ── Update ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (controller == null)
            {
                SetPanelActive(false);
                return;
            }

            bool counting  = controller.IsCountingDown.Value;
            bool complete  = controller.CountdownComplete.Value;

            if (!counting && !complete)
            {
                SetPanelActive(false);
                _goShown = false;
                return;
            }

            if (complete)
            {
                // Only act on the first frame of completion; after that the coroutine
                // owns the panel — returning early stops Update() re-showing it.
                if (!_goShown)
                {
                    SetPanelActive(true);
                    if (countdownText != null)
                        countdownText.text = "GO!";
                    _goShown = true;
                    StartCoroutine(HidePanelAfterDelay(goDuration));
                }
                return;
            }

            SetPanelActive(true);

            if (countdownText == null) return;

            // Ceiling so "10" shows for a full second, never shows "0" while ticking
            int seconds = Mathf.CeilToInt(controller.TimeRemaining.Value);
            countdownText.text = seconds.ToString();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void SetPanelActive(bool active)
        {
            if (panel != null && panel.activeSelf != active)
                panel.SetActive(active);
        }

        private IEnumerator HidePanelAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetPanelActive(false);
        }
    }
}
