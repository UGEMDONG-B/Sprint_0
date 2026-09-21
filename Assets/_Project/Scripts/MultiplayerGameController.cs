using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sprint0.Multiplayer
{
    public sealed class MultiplayerGameController : MonoBehaviour
    {
        const string GameStartedProperty = "gameStarted";

        public static MultiplayerGameController Instance { get; private set; }

        [Header("Screens")]
        [SerializeField] GameObject mainScreen;
        [SerializeField] GameObject roomBrowserScreen;
        [SerializeField] GameObject lobbyScreen;
        [SerializeField] GameObject gameHudScreen;
        [SerializeField] GameObject settingsScreen;
        [SerializeField] GameObject serverClosedScreen;

        [Header("Main")]
        [SerializeField] Button createRoomButton;
        [SerializeField] Button openRoomBrowserButton;
        [SerializeField] Button quitButton;

        [Header("Room Browser")]
        [SerializeField] Button refreshRoomsButton;
        [SerializeField] Button roomBrowserBackButton;
        [SerializeField] RectTransform roomListContent;

        [Header("Game")]
        [SerializeField] Text lobbyPlayerCountText;
        [SerializeField] Button lobbyStartButton;
        [SerializeField] Button lobbyLeaveButton;
        [SerializeField] Text gameRoomText;
        [SerializeField] Button resumeButton;
        [SerializeField] Button leaveGameButton;
        [SerializeField] Button serverClosedConfirmButton;

        [Header("Common")]
        [SerializeField] Text statusText;
        [SerializeField] Font fallbackFont;
        [SerializeField] int maxPlayers = 4;
        [SerializeField] int sceneBuildVersion = 1;
        [Header("Game integration")]
        [SerializeField] int minimumPlayersToStart = 1;
        [SerializeField] string sessionType = "Sprint0.GameSession";
        [SerializeField] bool lockCursorForGameplay = true;
        [SerializeField] bool reloadSceneAfterSession;

        ISession activeSession;
        NetworkManager boundNetworkManager;
        bool isReady;
        bool isBusy;
        bool isSettingsOpen;
        bool isGameStarted;
        bool isRefreshingRooms;
        float nextRoomRefreshTime;
        Font runtimeFont;
        Button soloButton;
        bool isInitializing;
        bool SupportsSolo => sessionType == "Sprint0.GravityCoop";
        public bool IsSoloMode { get; private set; }

        public bool CanControlPlayer => (IsSoloMode || activeSession != null) && isGameStarted && !isSettingsOpen && !isBusy;
        public int SceneBuildVersion => sceneBuildVersion;
        public bool HasGameStarted => isGameStarted;
        public GameObject GameHudScreen => gameHudScreen;
        public void OpenSettings() => SetSettingsOpen(true);

        public void Configure(
            GameObject main,
            GameObject roomBrowser,
            GameObject lobby,
            GameObject gameHud,
            GameObject settings,
            GameObject serverClosed,
            Button createButton,
            Button browseButton,
            Button exitButton,
            Button refreshButton,
            Button browserBackButton,
            RectTransform listContent,
            Text lobbyPlayerCount,
            Button startLobbyButton,
            Button leaveLobbyButton,
            Text roomText,
            Button continueButton,
            Button leaveButton,
            Button serverClosedConfirm,
            Text commonStatus,
            Font font,
            int buildVersion)
        {
            mainScreen = main;
            roomBrowserScreen = roomBrowser;
            lobbyScreen = lobby;
            gameHudScreen = gameHud;
            settingsScreen = settings;
            serverClosedScreen = serverClosed;
            createRoomButton = createButton;
            openRoomBrowserButton = browseButton;
            quitButton = exitButton;
            refreshRoomsButton = refreshButton;
            roomBrowserBackButton = browserBackButton;
            roomListContent = listContent;
            lobbyPlayerCountText = lobbyPlayerCount;
            lobbyStartButton = startLobbyButton;
            lobbyLeaveButton = leaveLobbyButton;
            gameRoomText = roomText;
            resumeButton = continueButton;
            leaveGameButton = leaveButton;
            serverClosedConfirmButton = serverClosedConfirm;
            statusText = commonStatus;
            fallbackFont = font;
            sceneBuildVersion = buildVersion;
        }

        void Awake()
        {
            Application.runInBackground = true;
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureLobbyScreen();
            ApplyRuntimeFont();
            BindButtons();
            if (SupportsSolo) AddSoloButton();
            ShowMainScreen();
        }

        async void Start()
        {
            // Scene Awake order is unspecified; NetworkManager.Singleton is ready by Start.
            BindNetworkCallbacks();
            if (SupportsSolo)
            {
                SetMenuInteractable(true);
                SetStatus("혼자 테스트하거나 멀티 방을 생성·참가하세요.");
                return;
            }
            await EnsureOnlineReady();
        }

        async Task<bool> EnsureOnlineReady()
        {
            if (IsSoloMode || isInitializing) return false;
            if (isReady) return true;
            isInitializing = true;
            SetStatus("온라인 서비스에 연결 중...");
            SetMenuInteractable(false);

            try
            {
                var initializationOptions = new InitializationOptions();
                var profile = GetDevelopmentProfile();
                if (!string.IsNullOrEmpty(profile))
                {
                    initializationOptions.SetProfile(profile);
                }

                await UnityServices.InitializeAsync(initializationOptions);

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                isReady = true;
                SetStatus("연결 완료");
                Debug.Log("[Sprint0] Unity Services initialized and anonymous authentication succeeded.");
            }
            catch (Exception exception)
            {
                SetStatus($"서비스 연결 실패: {exception.Message}");
                Debug.LogException(exception);
            }
            finally
            {
                isInitializing = false;
                if (this != null) SetMenuInteractable(isReady || SupportsSolo);
            }
            return isReady;
        }

        void Update()
        {
            if ((IsSoloMode || activeSession != null) && Keyboard.current?.escapeKey.wasPressedThisFrame == true && !isBusy)
            {
                SetSettingsOpen(!isSettingsOpen);
            }

            if (roomBrowserScreen != null && roomBrowserScreen.activeSelf && isReady && !isBusy
                && Time.unscaledTime >= nextRoomRefreshTime)
            {
                RefreshRooms();
            }

            if (activeSession != null && gameRoomText != null)
            {
                gameRoomText.text = $"{activeSession.Name}   {activeSession.Players.Count}/{activeSession.MaxPlayers}";

                if (lobbyPlayerCountText != null)
                {
                    lobbyPlayerCountText.text = $"현재 플레이어  {activeSession.Players.Count} / {activeSession.MaxPlayers}";
                    if (minimumPlayersToStart > 1)
                        lobbyPlayerCountText.text += activeSession.Players.Count < minimumPlayersToStart ? "\n플레이어를 기다리는 중" : "\n호스트가 시작하면 게임에 입장합니다";
                }
                lobbyStartButton.interactable = !isBusy && activeSession.IsHost && activeSession.Players.Count >= minimumPlayersToStart
                    && NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds.Count >= minimumPlayersToStart;
            }
        }

        void OnDestroy()
        {
            if (runtimeFont != null) Destroy(runtimeFont);
            UnbindSessionCallbacks(activeSession);
            UnbindNetworkCallbacks();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        void BindNetworkCallbacks()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            if (boundNetworkManager != manager)
            {
                UnbindNetworkCallbacks();
                boundNetworkManager = manager;
                manager.OnClientStopped += OnNetworkStopped;
                manager.OnServerStopped += OnNetworkStopped;
            }
            if (manager.NetworkConfig.ConnectionApproval)
                manager.ConnectionApprovalCallback = ApproveConnection;
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = boundNetworkManager != null && boundNetworkManager.ConnectedClientsIds.Count < maxPlayers;
            response.CreatePlayerObject = response.Approved && boundNetworkManager.NetworkConfig.PlayerPrefab != null;
            response.Pending = false;
            response.Reason = response.Approved ? "" : "Room is full.";
        }

        void UnbindNetworkCallbacks()
        {
            if (boundNetworkManager == null)
            {
                return;
            }

            boundNetworkManager.OnClientStopped -= OnNetworkStopped;
            boundNetworkManager.OnServerStopped -= OnNetworkStopped;
            if (boundNetworkManager.ConnectionApprovalCallback == ApproveConnection)
                boundNetworkManager.ConnectionApprovalCallback = null;
            boundNetworkManager = null;
        }

        async void OnNetworkStopped(bool _)
        {
            if (activeSession == null || isBusy)
            {
                return;
            }

            var disconnectedSession = activeSession;
            UnbindSessionCallbacks(disconnectedSession);
            activeSession = null;
            ShowServerClosedScreen();

            try
            {
                await disconnectedSession.LeaveAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Sprint0] Session cleanup after disconnect failed: {exception.Message}");
            }
        }

        void BindButtons()
        {
            createRoomButton.onClick.AddListener(CreateRoom);
            openRoomBrowserButton.onClick.AddListener(OpenRoomBrowser);
            quitButton.onClick.AddListener(QuitGame);
            refreshRoomsButton.onClick.AddListener(RefreshRooms);
            roomBrowserBackButton.onClick.AddListener(ShowMainScreen);
            lobbyStartButton.onClick.AddListener(StartGameFromLobby);
            lobbyLeaveButton.onClick.AddListener(LeaveSession);
            resumeButton.onClick.AddListener(() => SetSettingsOpen(false));
            leaveGameButton.onClick.AddListener(LeaveSession);
            serverClosedConfirmButton.onClick.AddListener(AcknowledgeServerClosed);
        }

        async void CreateRoom()
        {
            if (!await EnsureOnlineReady()) return;
            if (!CanBeginOnlineAction())
            {
                return;
            }

            SetBusy(true, "방을 생성하는 중...");

            try
            {
                var options = new SessionOptions
                {
                    Name = $"테스트 방 {UnityEngine.Random.Range(1000, 9999)}",
                    MaxPlayers = maxPlayers,
                    IsPrivate = false,
                    IsLocked = false,
                    Type = sessionType,
                    SessionProperties = new System.Collections.Generic.Dictionary<string, SessionProperty>
                    {
                        [GameStartedProperty] = new SessionProperty("false"),
                        ["gameType"] = new SessionProperty(sessionType)
                    }
                }.WithRelayNetwork();

                activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                BindSessionCallbacks();
                Debug.Log($"[Sprint0] Created Relay session '{activeSession.Name}'.");
                EnterLobbyScreen();
            }
            catch (Exception exception)
            {
                HandleOnlineError("방 생성 실패", exception);
            }
            finally
            {
                SetBusy(false);
            }
        }

        async void OpenRoomBrowser()
        {
            if (!await EnsureOnlineReady()) return;
            if (!isReady || isBusy)
            {
                return;
            }

            mainScreen.SetActive(false);
            roomBrowserScreen.SetActive(true);
            gameHudScreen.SetActive(false);
            settingsScreen.SetActive(false);
            SetStatus("공개 방을 불러오는 중...");
            nextRoomRefreshTime = 0f;
            RefreshRooms();
        }

        async void RefreshRooms()
        {
            if (!isReady || isBusy || isRefreshingRooms || !roomBrowserScreen.activeSelf)
            {
                return;
            }

            isRefreshingRooms = true;
            refreshRoomsButton.interactable = false;
            nextRoomRefreshTime = Time.unscaledTime + 5f;

            try
            {
                var result = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions
                {
                    Count = 50
                });

                ClearRoomList();

                var compatible = new System.Collections.Generic.List<ISessionInfo>();
                foreach (var candidate in result.Sessions)
                    if (candidate.Properties != null && candidate.Properties.TryGetValue("gameType", out var type)
                        ? type.Value == sessionType : sessionType == "Sprint0.GameSession") compatible.Add(candidate);
                if (compatible.Count == 0)
                {
                    CreateMessageRow("현재 참가 가능한 공개 방이 없습니다.");
                    SetStatus("방 목록은 5초마다 자동 갱신됩니다.");
                    return;
                }

                foreach (var session in compatible)
                {
                    CreateRoomRow(session);
                }

                SetStatus($"공개 방 {compatible.Count}개");
            }
            catch (Exception exception)
            {
                ClearRoomList();
                CreateMessageRow("방 목록을 불러오지 못했습니다.");
                HandleOnlineError("방 목록 조회 실패", exception);
            }
            finally
            {
                isRefreshingRooms = false;
                refreshRoomsButton.interactable = isReady && !isBusy;
            }
        }

        void CreateRoomRow(ISessionInfo session)
        {
            var row = CreateUiObject($"Room_{session.Id}", roomListContent);
            var image = row.AddComponent<Image>();
            image.color = new Color(0.10f, 0.13f, 0.18f, 0.96f);
            var layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 82f;
            layout.minHeight = 82f;

            var roomName = CreateText(row.transform, session.Name, 26, TextAnchor.MiddleLeft);
            SetAnchors(roomName.rectTransform, new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(24f, 0f), new Vector2(-8f, 0f));

            var players = session.MaxPlayers - session.AvailableSlots;
            var countText = CreateText(row.transform, $"{players}/{session.MaxPlayers}", 22, TextAnchor.MiddleCenter);
            SetAnchors(countText.rectTransform, new Vector2(0.70f, 0f), new Vector2(0.82f, 1f), Vector2.zero, Vector2.zero);

            var joinButton = CreateButton(row.transform, "참가", out _);
            SetAnchors((RectTransform)joinButton.transform, new Vector2(0.82f, 0.16f), new Vector2(0.98f, 0.84f), Vector2.zero, Vector2.zero);
            joinButton.interactable = !session.IsLocked && session.AvailableSlots > 0 && !session.HasPassword;
            var sessionId = session.Id;
            joinButton.onClick.AddListener(() => JoinRoom(sessionId, session.Name));
        }

        async void JoinRoom(string sessionId, string sessionName)
        {
            if (!CanBeginOnlineAction())
            {
                return;
            }

            SetBusy(true, $"{sessionName}에 참가하는 중...");

            try
            {
                activeSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
                BindSessionCallbacks();
                Debug.Log($"[Sprint0] Joined Relay session '{activeSession.Name}'.");
                EnterLobbyScreen();
            }
            catch (Exception exception)
            {
                HandleOnlineError("방 참가 실패", exception);
                RefreshRooms();
            }
            finally
            {
                SetBusy(false);
            }
        }

        async void LeaveSession()
        {
            if (IsSoloMode && !isBusy)
            {
                isGameStarted = false;
                StartCoroutine(ReloadSessionScene());
                return;
            }
            if (activeSession == null || isBusy)
            {
                return;
            }

            SetSettingsOpen(false);
            SetBusy(true, "방에서 나가는 중...");

            try
            {
                var sessionToLeave = activeSession;
                UnbindSessionCallbacks(sessionToLeave);
                activeSession = null;

                if (sessionToLeave.IsHost && sessionToLeave is IHostSession hostSession)
                {
                    await hostSession.DeleteAsync();
                    Debug.Log("[Sprint0] Host deleted the multiplayer session.");
                }
                else
                {
                    await sessionToLeave.LeaveAsync();
                }

                Debug.Log("[Sprint0] Left multiplayer session.");
            }
            catch (Exception exception)
            {
                HandleOnlineError("방 나가기 실패", exception);

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }
            finally
            {
                SetBusy(false);
                if (reloadSceneAfterSession) StartCoroutine(ReloadSessionScene());
                else ShowMainScreen();
            }
        }

        System.Collections.IEnumerator ReloadSessionScene()
        {
            isBusy = true;
            SetCursorForGameplay(false);
            var manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.Shutdown();
                while (manager != null && manager.ShutdownInProgress) yield return null;
                if (manager != null) Destroy(manager.gameObject);
                yield return null;
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.path);
        }

        void EnterGameScreen()
        {
            mainScreen.SetActive(false);
            roomBrowserScreen.SetActive(false);
            lobbyScreen.SetActive(false);
            gameHudScreen.SetActive(true);
            settingsScreen.SetActive(false);
            serverClosedScreen.SetActive(false);
            isSettingsOpen = false;
            isGameStarted = true;
            SetStatus(string.Empty);
            SetCursorForGameplay(true);
        }

        void ShowMainScreen()
        {
            if (activeSession != null)
            {
                return;
            }

            mainScreen.SetActive(true);
            roomBrowserScreen.SetActive(false);
            lobbyScreen.SetActive(false);
            gameHudScreen.SetActive(false);
            settingsScreen.SetActive(false);
            serverClosedScreen.SetActive(false);
            isSettingsOpen = false;
            isGameStarted = false;
            SetCursorForGameplay(false);

            if (isReady)
            {
                SetStatus("방을 만들거나 공개 방에 참가하세요.");
            }
        }

        void SetSettingsOpen(bool open)
        {
            isSettingsOpen = open;
            settingsScreen.SetActive(open);
            SetCursorForGameplay(!open);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(open ? resumeButton.gameObject : null);
            }
        }

        void ShowServerClosedScreen()
        {
            mainScreen.SetActive(false);
            roomBrowserScreen.SetActive(false);
            lobbyScreen.SetActive(false);
            gameHudScreen.SetActive(false);
            settingsScreen.SetActive(false);
            serverClosedScreen.SetActive(true);
            isSettingsOpen = false;
            isGameStarted = false;
            SetStatus(string.Empty);
            SetCursorForGameplay(false);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(serverClosedConfirmButton.gameObject);
            }
        }

        void AcknowledgeServerClosed()
        {
            if (reloadSceneAfterSession) { StartCoroutine(ReloadSessionScene()); return; }
            serverClosedScreen.SetActive(false);
            ShowMainScreen();
        }

        void EnterLobbyScreen()
        {
            mainScreen.SetActive(false);
            roomBrowserScreen.SetActive(false);
            lobbyScreen.SetActive(true);
            gameHudScreen.SetActive(false);
            settingsScreen.SetActive(false);
            serverClosedScreen.SetActive(false);
            isSettingsOpen = false;
            isGameStarted = false;
            SetStatus(string.Empty);
            SetCursorForGameplay(false);

            var isHost = activeSession != null && activeSession.IsHost;
            lobbyStartButton.gameObject.SetActive(isHost);
            lobbyStartButton.interactable = isHost && activeSession.Players.Count >= minimumPlayersToStart;

            if (IsSessionGameStarted())
            {
                HandleNetworkGameStarted();
            }
        }

        async void StartGameFromLobby()
        {
            if (isBusy || activeSession == null || !activeSession.IsHost || activeSession.Players.Count < minimumPlayersToStart
                || NetworkManager.Singleton.ConnectedClientsIds.Count < minimumPlayersToStart || activeSession is not IHostSession hostSession)
            {
                return;
            }

            SetBusy(true, "게임을 시작하는 중...");

            try
            {
                hostSession.IsLocked = true;
                hostSession.SetProperty(GameStartedProperty, new SessionProperty("true"));
                await hostSession.SavePropertiesAsync();
                HandleNetworkGameStarted();
            }
            catch (Exception exception)
            {
                HandleOnlineError("게임 시작 실패", exception);
            }
            finally
            {
                SetBusy(false);
            }
        }

        public void HandleNetworkGameStarted()
        {
            if (activeSession == null || isGameStarted)
            {
                return;
            }

            EnterGameScreen();
        }

        void BindSessionCallbacks()
        {
            if (activeSession != null)
            {
                activeSession.SessionPropertiesChanged += OnSessionPropertiesChanged;
                activeSession.RemovedFromSession += OnRemovedFromSession;
                activeSession.Deleted += OnSessionDeleted;
            }
        }

        void UnbindSessionCallbacks(ISession session)
        {
            if (session != null)
            {
                session.SessionPropertiesChanged -= OnSessionPropertiesChanged;
                session.RemovedFromSession -= OnRemovedFromSession;
                session.Deleted -= OnSessionDeleted;
            }
        }

        void OnRemovedFromSession()
        {
            HandleSessionEndedByHost();
        }

        void OnSessionDeleted()
        {
            HandleSessionEndedByHost();
        }

        void HandleSessionEndedByHost()
        {
            if (activeSession == null)
            {
                return;
            }

            var endedSession = activeSession;
            UnbindSessionCallbacks(endedSession);
            activeSession = null;
            isBusy = false;
            ShowServerClosedScreen();
            Debug.Log("[Sprint0] The host ended the multiplayer session.");
        }

        void OnSessionPropertiesChanged()
        {
            if (IsSessionGameStarted())
            {
                HandleNetworkGameStarted();
            }
        }

        bool IsSessionGameStarted()
        {
            return activeSession != null
                && activeSession.Properties != null
                && activeSession.Properties.TryGetValue(GameStartedProperty, out var property)
                && string.Equals(property.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        void EnsureLobbyScreen()
        {
            if (lobbyScreen != null && lobbyPlayerCountText != null && lobbyStartButton != null && lobbyLeaveButton != null)
            {
                return;
            }

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Sprint0] Lobby UI could not be created because no Canvas exists.");
                return;
            }

            lobbyScreen = CreateUiObject("LobbyScreen", canvas.transform);
            var background = lobbyScreen.AddComponent<Image>();
            background.color = new Color(0.025f, 0.04f, 0.07f, 1f);
            SetAnchors((RectTransform)lobbyScreen.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            lobbyPlayerCountText = CreateText(lobbyScreen.transform, "현재 플레이어  1 / 4", 34, TextAnchor.MiddleCenter);
            lobbyPlayerCountText.fontStyle = FontStyle.Bold;
            SetAnchors(lobbyPlayerCountText.rectTransform, new Vector2(0.25f, 0.54f), new Vector2(0.75f, 0.64f), Vector2.zero, Vector2.zero);

            lobbyStartButton = CreateButton(lobbyScreen.transform, "시작", out var startLabel);
            startLabel.fontSize = 28;
            SetAnchors((RectTransform)lobbyStartButton.transform, new Vector2(0.39f, 0.41f), new Vector2(0.61f, 0.49f), Vector2.zero, Vector2.zero);

            lobbyLeaveButton = CreateButton(lobbyScreen.transform, "나가기", out var leaveLabel);
            leaveLabel.fontSize = 22;
            lobbyLeaveButton.GetComponent<Image>().color = new Color(0.72f, 0.22f, 0.25f, 1f);
            SetAnchors((RectTransform)lobbyLeaveButton.transform, new Vector2(0.025f, 0.90f), new Vector2(0.13f, 0.965f), Vector2.zero, Vector2.zero);
            lobbyScreen.SetActive(false);
        }

        bool CanBeginOnlineAction()
        {
            if (!isReady || isBusy || activeSession != null) return false;

            // Also cover a manager replaced or created after this controller's Start.
            BindNetworkCallbacks();
            if (boundNetworkManager == null)
            {
                SetStatus("NetworkManager가 없어 연결을 시작할 수 없습니다.");
                return false;
            }
            return !boundNetworkManager.IsListening && !boundNetworkManager.ShutdownInProgress;
        }

        void SetBusy(bool busy, string message = null)
        {
            isBusy = busy;
            SetMenuInteractable(isReady && !busy);
            leaveGameButton.interactable = !busy;
            resumeButton.interactable = !busy;

            if (!string.IsNullOrWhiteSpace(message))
            {
                SetStatus(message);
            }
        }

        void SetMenuInteractable(bool interactable)
        {
            if (soloButton != null) soloButton.interactable = !isInitializing && !isBusy;
            createRoomButton.interactable = interactable;
            openRoomBrowserButton.interactable = interactable;
            refreshRoomsButton.interactable = interactable;
            roomBrowserBackButton.interactable = interactable;
            lobbyStartButton.interactable = interactable && activeSession != null && activeSession.IsHost && activeSession.Players.Count >= minimumPlayersToStart;
            lobbyLeaveButton.interactable = interactable && activeSession != null;
        }

        void AddSoloButton()
        {
            soloButton = CreateButton(createRoomButton.transform.parent, "혼자 테스트 (좌우 분할)", out _);
            SetAnchors((RectTransform)soloButton.transform, new Vector2(0.14f, 0.49f), new Vector2(0.86f, 0.59f), Vector2.zero, Vector2.zero);
            SetAnchors((RectTransform)createRoomButton.transform, new Vector2(0.14f, 0.36f), new Vector2(0.86f, 0.46f), Vector2.zero, Vector2.zero);
            SetAnchors((RectTransform)openRoomBrowserButton.transform, new Vector2(0.14f, 0.23f), new Vector2(0.86f, 0.33f), Vector2.zero, Vector2.zero);
            SetAnchors((RectTransform)quitButton.transform, new Vector2(0.14f, 0.10f), new Vector2(0.86f, 0.20f), Vector2.zero, Vector2.zero);
            createRoomButton.GetComponentInChildren<Text>().text = "멀티 · 방 생성";
            openRoomBrowserButton.GetComponentInChildren<Text>().text = "멀티 · 방 참가";
            soloButton.onClick.AddListener(StartSoloTest);
        }

        public void StartSoloTest()
        {
            var manager = NetworkManager.Singleton;
            if (!SupportsSolo || IsSoloMode || activeSession != null || isBusy || isInitializing
                || manager == null || manager.IsListening || manager.ShutdownInProgress) return;
            var previousTransport = manager.NetworkConfig.NetworkTransport;
            bool previousApproval = manager.NetworkConfig.ConnectionApproval;
            manager.NetworkConfig.NetworkTransport = manager.gameObject.AddComponent<Sprint0.GravityCoop.OfflineTransport>();
            manager.NetworkConfig.ConnectionApproval = false;
            manager.ConnectionApprovalCallback = null;
            IsSoloMode = true;
            if (!manager.StartHost())
            {
                IsSoloMode = false;
                Destroy(manager.NetworkConfig.NetworkTransport);
                manager.NetworkConfig.NetworkTransport = previousTransport;
                manager.NetworkConfig.ConnectionApproval = previousApproval;
                BindNetworkCallbacks();
                SetStatus("혼자 테스트를 시작하지 못했습니다.");
                return;
            }
            EnterGameScreen();
            leaveGameButton.GetComponentInChildren<Text>().text = "테스트 종료 · 모드 선택";
        }

        void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                var statusContainer = statusText.transform.parent.gameObject;
                statusContainer.SetActive(!string.IsNullOrEmpty(message));
            }
        }

        void HandleOnlineError(string context, Exception exception)
        {
            SetStatus($"{context}: {exception.Message}");
            Debug.LogException(exception);
        }

        void ClearRoomList()
        {
            for (var index = roomListContent.childCount - 1; index >= 0; index--)
            {
                Destroy(roomListContent.GetChild(index).gameObject);
            }
        }

        void CreateMessageRow(string message)
        {
            var text = CreateText(roomListContent, message, 24, TextAnchor.MiddleCenter);
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 90f;
        }

        Text CreateText(Transform parent, string value, int size, TextAnchor alignment)
        {
            var gameObject = CreateUiObject("Text", parent);
            var text = gameObject.AddComponent<Text>();
            text.text = value;
            text.font = fallbackFont;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.95f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        Button CreateButton(Transform parent, string label, out Text labelText)
        {
            var gameObject = CreateUiObject($"{label}Button", parent);
            var image = gameObject.AddComponent<Image>();
            image.color = new Color(0.10f, 0.62f, 0.58f);

            var button = gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.72f, 0.78f, 0.78f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.48f, 0.6f);
            button.colors = colors;

            labelText = CreateText(gameObject.transform, label, 23, TextAnchor.MiddleCenter);
            SetAnchors(labelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        void ApplyRuntimeFont()
        {
            try
            {
                runtimeFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);

                if (runtimeFont != null)
                {
                    fallbackFont = runtimeFont;
                }
            }
            catch (Exception)
            {
                // The serialized legacy font remains available on platforms without OS font access.
            }

            foreach (var screen in new[] { mainScreen, roomBrowserScreen, lobbyScreen, gameHudScreen, settingsScreen, serverClosedScreen })
                foreach (var text in screen.GetComponentsInChildren<Text>(true)) text.font = fallbackFont;
            if (statusText != null) statusText.font = fallbackFont;
        }

        void SetCursorForGameplay(bool gameplay)
        {
            gameplay = gameplay && isGameStarted && lockCursorForGameplay;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        static string GetDevelopmentProfile()
        {
            const string argumentPrefix = "-ugs-profile=";
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith(argumentPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return argument.Substring(argumentPrefix.Length);
                }
            }

#if UNITY_EDITOR
            unchecked
            {
                uint hash = 2166136261;
                foreach (var character in Application.dataPath.Replace('\\', '/').ToLowerInvariant())
                {
                    hash = (hash ^ character) * 16777619;
                }

                // MPPM clones have different project paths, so each virtual player gets a distinct login.
                return $"editor_{hash:X8}";
            }
#else
            return null;
#endif
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
