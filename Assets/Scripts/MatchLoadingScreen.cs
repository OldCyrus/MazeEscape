using UnityEngine;
using Unity.Netcode;

namespace MazeEscape
{
    /// <summary>
    /// Shows a full-screen overlay while the match is loading and hides it once every
    /// client has reported ready (MatchManager.MatchReady == true).
    ///
    /// Input locking is NOT handled here. That responsibility belongs to UMAPlayerSetup,
    /// which locks input on spawn and unlocks it after CharacterCreated fires. Without
    /// UMA on the player prefab the loading screen is purely visual.
    ///
    /// Inspector setup:
    ///  - Assign the loading Canvas to the Loading Canvas field.
    ///  - Leave Report Ready Immediately = true until UMAPlayerSetup is on the player prefab.
    /// </summary>
    public class MatchLoadingScreen : MonoBehaviour
    {
        [Tooltip("Canvas to show while the match is loading. Starts hidden.")]
        [SerializeField] private Canvas loadingCanvas;

        [Tooltip("Reports this client as ready immediately when they connect (pre-UMA). " +
                 "Set false once UMAPlayerSetup is on the player prefab.")]
        [SerializeField] private bool reportReadyImmediately = true;

        private bool m_MatchManagerSubscribed;
        private bool m_NetworkManagerSubscribed;

        private void Start()
        {
            if (loadingCanvas != null)
                loadingCanvas.enabled = false;

            TrySubscribeToNetworkManager();
        }

        private void Update()
        {
            if (!m_NetworkManagerSubscribed)
                TrySubscribeToNetworkManager();

            if (!m_MatchManagerSubscribed && MatchManager.Instance != null)
            {
                MatchManager.Instance.MatchReady.OnValueChanged += OnMatchReadyChanged;
                m_MatchManagerSubscribed = true;

                if (MatchManager.Instance.MatchReady.Value)
                    HideLoadingScreen();
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            if (m_MatchManagerSubscribed && MatchManager.Instance != null)
                MatchManager.Instance.MatchReady.OnValueChanged -= OnMatchReadyChanged;
        }

        private void TrySubscribeToNetworkManager()
        {
            if (m_NetworkManagerSubscribed) return;
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            m_NetworkManagerSubscribed = true;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            if (loadingCanvas != null)
                loadingCanvas.enabled = true;

            if (reportReadyImmediately)
                MatchManager.Instance?.ReportReadyServerRpc();
        }

        private void OnMatchReadyChanged(bool _, bool isReady)
        {
            if (isReady) HideLoadingScreen();
        }

        private void HideLoadingScreen()
        {
            if (loadingCanvas != null)
                loadingCanvas.enabled = false;
        }
    }
}
