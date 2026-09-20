using Sprint0.Multiplayer;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sprint0.Editor
{
    [InitializeOnLoad]
    public static class SampleSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        const string MaterialFolder = "Assets/_Project/Materials";
        const int BuildVersion = 5;

        static readonly Color Background = new(0.025f, 0.04f, 0.07f, 1f);
        static readonly Color Panel = new(0.055f, 0.075f, 0.11f, 0.97f);
        static readonly Color Primary = new(0.10f, 0.62f, 0.58f, 1f);
        static readonly Color Secondary = new(0.16f, 0.20f, 0.28f, 1f);
        static readonly Color Danger = new(0.72f, 0.22f, 0.25f, 1f);
        static readonly Color TextColor = new(0.92f, 0.95f, 1f, 1f);

        static SampleSceneBuilder()
        {
            if (!Application.isBatchMode)
            {
                EditorApplication.delayCall += BuildIfNeeded;
            }
        }

        [MenuItem("Tools/Sprint 0/Rebuild Multiplayer Sample Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            var playerPrefab = BuildPlayerPrefab();
            BuildWorld();
            BuildCamera();
            BuildLighting();
            BuildEventSystem();
            var networkManager = BuildNetworkManager(playerPrefab);
            BuildInterface(networkManager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sprint0] Multiplayer SampleScene build completed.");
        }

        public static void BuildSceneFromCommandLine()
        {
            BuildScene();
        }

        static void BuildIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            if (File.Exists(ScenePath)
                && File.ReadAllText(ScenePath).Contains($"sceneBuildVersion: {BuildVersion}"))
            {
                return;
            }

            // This helper belongs to the optional sample scene. Projects that only
            // import the networking scripts do not necessarily contain that scene.
            if (!File.Exists(ScenePath))
            {
                return;
            }

            BuildScene();
        }

        static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("NetworkPlayer");
            root.layer = LayerMask.NameToLayer("Default");

            var characterController = root.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.5f;
            characterController.center = Vector3.up;
            characterController.skinWidth = 0.05f;

            root.AddComponent<NetworkObject>();
            var networkTransform = root.AddComponent<OwnerNetworkTransform>();
            networkTransform.Interpolate = true;
            root.AddComponent<ThirdPersonNetworkPlayer>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "CapsuleVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up;
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateMaterial("PlayerPrototype", new Color(0.12f, 0.78f, 0.72f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildWorld()
        {
            var world = new GameObject("World");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(world.transform);
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Ground", new Color(0.11f, 0.16f, 0.18f));

            CreateBlock(world.transform, "NorthWall", new Vector3(0f, 1f, 25f), new Vector3(50f, 2f, 1f));
            CreateBlock(world.transform, "SouthWall", new Vector3(0f, 1f, -25f), new Vector3(50f, 2f, 1f));
            CreateBlock(world.transform, "EastWall", new Vector3(25f, 1f, 0f), new Vector3(1f, 2f, 50f));
            CreateBlock(world.transform, "WestWall", new Vector3(-25f, 1f, 0f), new Vector3(1f, 2f, 50f));

            CreateBlock(world.transform, "TestBlockA", new Vector3(7f, 1f, 5f), new Vector3(3f, 2f, 3f));
            CreateBlock(world.transform, "TestBlockB", new Vector3(-7f, 1.5f, 3f), new Vector3(4f, 3f, 2f));
            CreateBlock(world.transform, "TestBlockC", new Vector3(2f, 0.75f, -8f), new Vector3(6f, 1.5f, 2f));
        }

        static void CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.SetPositionAndRotation(position, Quaternion.identity);
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("WorldBlock", new Color(0.17f, 0.24f, 0.28f));
        }

        static Material GetOrCreateMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            var path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = name,
                color = color
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void BuildCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 8f, -12f), Quaternion.Euler(24f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.07f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            cameraObject.AddComponent<AudioListener>();
        }

        static void BuildLighting()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.color = new Color(0.88f, 0.94f, 1f);
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.26f, 0.30f);
        }

        public static void BuildEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        public static NetworkManager BuildNetworkManager(GameObject playerPrefab)
        {
            var managerObject = new GameObject("NetworkManager");
            var transport = managerObject.AddComponent<UnityTransport>();
            var networkManager = managerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.TickRate = 30;
            return networkManager;
        }

        public static void BuildInterface(NetworkManager networkManager)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("Interface", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var main = CreateFullPanel(canvasObject.transform, "MainScreen", Background);
            var mainCard = CreateCard(main.transform, "MainCard", new Vector2(560f, 650f));
            var createButton = CreateButton(mainCard.transform, "방 생성", font, Primary);
            SetRect((RectTransform)createButton.transform, new Vector2(0.14f, 0.46f), new Vector2(0.86f, 0.57f), Vector2.zero, Vector2.zero);
            var browseButton = CreateButton(mainCard.transform, "방 참가", font, Secondary);
            SetRect((RectTransform)browseButton.transform, new Vector2(0.14f, 0.31f), new Vector2(0.86f, 0.42f), Vector2.zero, Vector2.zero);
            var quitButton = CreateButton(mainCard.transform, "나가기", font, Danger);
            SetRect((RectTransform)quitButton.transform, new Vector2(0.14f, 0.16f), new Vector2(0.86f, 0.27f), Vector2.zero, Vector2.zero);

            var browser = CreateFullPanel(canvasObject.transform, "RoomBrowserScreen", Background);
            var browserCard = CreateCard(browser.transform, "BrowserCard", new Vector2(1120f, 760f));
            var browserTitle = CreateText(browserCard.transform, "공개 방 목록", font, 40, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(browserTitle.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.60f, 0.96f), Vector2.zero, Vector2.zero);
            var refreshButton = CreateButton(browserCard.transform, "새로고침", font, Primary, 21);
            SetRect((RectTransform)refreshButton.transform, new Vector2(0.70f, 0.87f), new Vector2(0.83f, 0.95f), Vector2.zero, Vector2.zero);
            var backButton = CreateButton(browserCard.transform, "뒤로", font, Secondary, 21);
            SetRect((RectTransform)backButton.transform, new Vector2(0.85f, 0.87f), new Vector2(0.94f, 0.95f), Vector2.zero, Vector2.zero);
            var content = CreateRoomScrollView(browserCard.transform);

            var lobby = CreateFullPanel(canvasObject.transform, "LobbyScreen", Background);
            var lobbyPlayerCount = CreateText(lobby.transform, "현재 플레이어  1 / 4", font, 34, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(lobbyPlayerCount.rectTransform, new Vector2(0.25f, 0.54f), new Vector2(0.75f, 0.64f), Vector2.zero, Vector2.zero);
            var lobbyStartButton = CreateButton(lobby.transform, "시작", font, Primary, 28);
            SetRect((RectTransform)lobbyStartButton.transform, new Vector2(0.39f, 0.41f), new Vector2(0.61f, 0.49f), Vector2.zero, Vector2.zero);
            var lobbyLeaveButton = CreateButton(lobby.transform, "나가기", font, Danger, 22);
            SetRect((RectTransform)lobbyLeaveButton.transform, new Vector2(0.025f, 0.90f), new Vector2(0.13f, 0.965f), Vector2.zero, Vector2.zero);

            var gameHud = CreateFullPanel(canvasObject.transform, "GameHudScreen", Color.clear, false);
            var roomText = CreateText(gameHud.transform, "Room", font, 27, FontStyle.Bold, TextAnchor.MiddleLeft);
            roomText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            SetRect(roomText.rectTransform, new Vector2(0.025f, 0.91f), new Vector2(0.55f, 0.975f), Vector2.zero, Vector2.zero);
            var helpText = CreateText(gameHud.transform, "WASD 이동  |  SHIFT 달리기  |  마우스 시점  |  ESC 설정", font, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            helpText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            SetRect(helpText.rectTransform, new Vector2(0.20f, 0.025f), new Vector2(0.80f, 0.08f), Vector2.zero, Vector2.zero);

            var settings = CreateFullPanel(canvasObject.transform, "SettingsScreen", new Color(0.01f, 0.02f, 0.035f, 0.78f));
            var settingsCard = CreateCard(settings.transform, "SettingsCard", new Vector2(520f, 470f));
            var settingsTitle = CreateText(settingsCard.transform, "설정", font, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(settingsTitle.rectTransform, new Vector2(0.10f, 0.72f), new Vector2(0.90f, 0.91f), Vector2.zero, Vector2.zero);
            var resumeButton = CreateButton(settingsCard.transform, "계속하기", font, Primary);
            SetRect((RectTransform)resumeButton.transform, new Vector2(0.14f, 0.43f), new Vector2(0.86f, 0.57f), Vector2.zero, Vector2.zero);
            var leaveButton = CreateButton(settingsCard.transform, "방 나가기", font, Danger);
            SetRect((RectTransform)leaveButton.transform, new Vector2(0.14f, 0.23f), new Vector2(0.86f, 0.37f), Vector2.zero, Vector2.zero);

            var serverClosed = CreateFullPanel(canvasObject.transform, "ServerClosedScreen", Background);
            var serverClosedText = CreateText(serverClosed.transform, "서버가 종료되었습니다", font, 38, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(serverClosedText.rectTransform, new Vector2(0.20f, 0.52f), new Vector2(0.80f, 0.64f), Vector2.zero, Vector2.zero);
            var serverClosedConfirm = CreateButton(serverClosed.transform, "확인", font, Primary);
            SetRect((RectTransform)serverClosedConfirm.transform, new Vector2(0.40f, 0.39f), new Vector2(0.60f, 0.47f), Vector2.zero, Vector2.zero);

            var statusBackground = CreateUiObject("Status", canvasObject.transform);
            var statusImage = statusBackground.AddComponent<Image>();
            statusImage.color = new Color(0.03f, 0.05f, 0.08f, 0.92f);
            SetRect((RectTransform)statusBackground.transform, new Vector2(0.23f, 0.02f), new Vector2(0.77f, 0.085f), Vector2.zero, Vector2.zero);
            var statusText = CreateText(statusBackground.transform, "온라인 서비스에 연결 중...", font, 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetRect(statusText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 4f), new Vector2(-18f, -4f));

            var systems = new GameObject("MultiplayerGame");
            var controller = systems.AddComponent<MultiplayerGameController>();
            controller.Configure(
                main,
                browser,
                lobby,
                gameHud,
                settings,
                serverClosed,
                createButton,
                browseButton,
                quitButton,
                refreshButton,
                backButton,
                content,
                lobbyPlayerCount,
                lobbyStartButton,
                lobbyLeaveButton,
                roomText,
                resumeButton,
                leaveButton,
                serverClosedConfirm,
                statusText,
                font,
                BuildVersion);

            browser.SetActive(false);
            lobby.SetActive(false);
            gameHud.SetActive(false);
            settings.SetActive(false);
            serverClosed.SetActive(false);
            Selection.activeGameObject = systems;
        }

        static RectTransform CreateRoomScrollView(Transform parent)
        {
            var scrollObject = CreateUiObject("RoomScrollView", parent);
            var scrollImage = scrollObject.AddComponent<Image>();
            scrollImage.color = new Color(0.025f, 0.035f, 0.055f, 0.95f);
            var scrollRect = scrollObject.AddComponent<ScrollRect>();
            SetRect((RectTransform)scrollObject.transform, new Vector2(0.055f, 0.10f), new Vector2(0.945f, 0.82f), Vector2.zero, Vector2.zero);

            var viewport = CreateUiObject("Viewport", scrollObject.transform);
            viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            SetRect((RectTransform)viewport.transform, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -14f));

            var content = CreateUiObject("Content", viewport.transform).GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = (RectTransform)viewport.transform;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;
            return content;
        }

        static GameObject CreateFullPanel(Transform parent, string name, Color color, bool raycastTarget = true)
        {
            var panel = CreateUiObject(name, parent);
            var image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            SetRect((RectTransform)panel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return panel;
        }

        static GameObject CreateCard(Transform parent, string name, Vector2 size)
        {
            var card = CreateUiObject(name, parent);
            card.AddComponent<Image>().color = Panel;
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return card;
        }

        static Button CreateButton(Transform parent, string label, Font font, Color color, int fontSize = 25)
        {
            var buttonObject = CreateUiObject($"{label}Button", parent);
            buttonObject.AddComponent<Image>().color = color;
            var button = buttonObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.72f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.42f, 0.44f, 0.48f, 0.65f);
            button.colors = colors;

            var labelText = CreateText(buttonObject.transform, label, font, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(labelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        static Text CreateText(Transform parent, string value, Font font, int size, FontStyle style, TextAnchor alignment)
        {
            var textObject = CreateUiObject("Text", parent);
            var text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
