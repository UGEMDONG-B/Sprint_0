using Sprint0.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaftGame.Hyunwoo.Multiplayer
{
    /// <summary>
    /// Adds Raft-only scene switching on top of the shared multiplayer lobby.
    /// The shared lobby scripts and NetworkManager assets remain untouched.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class RaftMultiplayerBootstrap : MonoBehaviour
    {
        public const string LobbyScenePath = "Assets/RaftGame_hyunwoo/Scenes/RaftLobby.unity";
        public const string MainScenePath = "Assets/RaftGame_hyunwoo/Scenes/Main.unity";

        bool transitionRequested;
        bool sceneEventsSubscribed;
        NetworkManager networkManager;
        RaftPlayerVisualBridge playerVisualBridge;
        RaftSharedWorldStateBridge sharedWorldStateBridge;
        RaftExtendedWorldStateBridge extendedWorldStateBridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallInRaftLobby()
        {
            if (SceneManager.GetActiveScene().path != LobbyScenePath ||
                FindFirstObjectByType<RaftMultiplayerBootstrap>() != null)
            {
                return;
            }

            var bootstrap = new GameObject(nameof(RaftMultiplayerBootstrap));
            bootstrap.AddComponent<RaftMultiplayerBootstrap>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            networkManager = NetworkManager.Singleton;

            if (networkManager == null)
            {
                Debug.LogError("[RaftMultiplayer] NetworkManager was not found in RaftLobby.");
                enabled = false;
                return;
            }

            if (networkManager.IsListening)
            {
                Debug.LogError("[RaftMultiplayer] Scene management must be enabled before starting the network session.");
                enabled = false;
                return;
            }

            // This changes only the RaftLobby runtime instance. No shared prefab or scene asset is modified.
            networkManager.NetworkConfig.EnableSceneManagement = true;
            Debug.Log("[RaftMultiplayer] RaftLobby bootstrap ready.");
        }

        void Update()
        {
            EnsureSceneEventSubscription();
            EnsurePlayerVisualBridge();

            if (transitionRequested || networkManager == null || !networkManager.IsListening ||
                !networkManager.IsServer)
            {
                return;
            }

            var lobby = MultiplayerGameController.Instance;
            if (lobby == null || !lobby.CanControlPlayer)
            {
                return;
            }

            transitionRequested = true;
            var status = networkManager.SceneManager.LoadScene(MainScenePath, LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
            {
                transitionRequested = false;
                Debug.LogError($"[RaftMultiplayer] Failed to start Main scene transition: {status}");
                return;
            }

            Debug.Log("[RaftMultiplayer] Host started the synchronized Main scene transition.");
        }

        void EnsureSceneEventSubscription()
        {
            if (sceneEventsSubscribed || networkManager == null || !networkManager.IsListening ||
                networkManager.SceneManager == null)
            {
                return;
            }

            networkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            sceneEventsSubscribed = true;
        }

        void EnsurePlayerVisualBridge()
        {
            if (networkManager == null || !networkManager.IsListening ||
                SceneManager.GetActiveScene().path != MainScenePath)
            {
                return;
            }

            if (playerVisualBridge == null)
            {
                playerVisualBridge = gameObject.AddComponent<RaftPlayerVisualBridge>();
                Debug.Log("[RaftMultiplayer] Raft player mesh bridge installed in Main.");
            }

            if (sharedWorldStateBridge == null)
            {
                sharedWorldStateBridge = gameObject.AddComponent<RaftSharedWorldStateBridge>();
            }

            if (extendedWorldStateBridge == null)
            {
                extendedWorldStateBridge = gameObject.AddComponent<RaftExtendedWorldStateBridge>();
            }
        }

        void OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete &&
                sceneEvent.SceneName == "Main")
            {
                Debug.Log($"[RaftMultiplayer] Client {sceneEvent.ClientId} loaded Main.");
            }

            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted &&
                sceneEvent.SceneName == "Main")
            {
                Debug.Log("[RaftMultiplayer] Every connected client completed the Main scene transition.");
            }
        }

        void OnDestroy()
        {
            if (sceneEventsSubscribed && networkManager != null && networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }
    }
}
