using Sprint0.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class GameResultUI : MonoBehaviour
    {
        static GameResultUI instance;

        GameObject overlay;
        Text title;
        Text message;
        GameResultState currentState = GameResultState.Playing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInstance()
        {
            Ensure();
        }

        public static void Show(GameResultState state)
        {
            if (state == GameResultState.Playing)
            {
                return;
            }

            var ui = Ensure();
            ui.ShowInternal(state);
        }

        static GameResultUI Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<GameResultUI>();
            if (instance == null)
            {
                instance = new GameObject("Step10_GameResultUI").AddComponent<GameResultUI>();
            }

            instance.BuildIfNeeded();
            return instance;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            BuildIfNeeded();
        }

        void ShowInternal(GameResultState state)
        {
            if (currentState == state && overlay.activeSelf)
            {
                return;
            }

            currentState = state;
            title.text = state == GameResultState.GameClear ? "게임 클리어" : "게임 오버";
            message.text = state == GameResultState.GameClear
                ? "보스를 처치했습니다."
                : "시체 용사가 쓰러졌습니다.";
            overlay.SetActive(true);
            Time.timeScale = 0f;
        }

        void ReturnToLobby()
        {
            Time.timeScale = 1f;
            overlay.SetActive(false);
            currentState = GameResultState.Playing;
            var controller = MultiplayerGameController.Instance;
            if (controller != null)
            {
                controller.LeaveSession();
            }
        }

        void BuildIfNeeded()
        {
            if (overlay != null)
            {
                return;
            }

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1300;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            overlay = CreateRect("ResultOverlay", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0.015f, 0.02f, 0.035f, 0.88f);

            var panel = CreateRect("ResultPanel", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(760f, 360f), Vector2.zero);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.1f, 0.16f, 0.98f);

            title = CreateText("Title", panel.transform, 54, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.62f);
            title.rectTransform.anchorMax = new Vector2(0.95f, 0.9f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            message = CreateText("Message", panel.transform, 28, TextAnchor.MiddleCenter);
            message.rectTransform.anchorMin = new Vector2(0.05f, 0.43f);
            message.rectTransform.anchorMax = new Vector2(0.95f, 0.62f);
            message.rectTransform.offsetMin = message.rectTransform.offsetMax = Vector2.zero;

            var buttonObject = CreateRect("ReturnButton", panel.transform, new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.36f), Vector2.zero, Vector2.zero);
            var buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.18f, 0.34f, 0.52f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(ReturnToLobby);

            var buttonLabel = CreateText("Label", buttonObject.transform, 26, TextAnchor.MiddleCenter);
            buttonLabel.text = "로비로 돌아가기";
            buttonLabel.rectTransform.anchorMin = Vector2.zero;
            buttonLabel.rectTransform.anchorMax = Vector2.one;
            buttonLabel.rectTransform.offsetMin = buttonLabel.rectTransform.offsetMax = Vector2.zero;

            overlay.SetActive(false);
        }

        static GameObject CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
        {
            var value = new GameObject(objectName, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return value;
        }

        static Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment)
        {
            var textObject = CreateRect(objectName, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = textObject.AddComponent<Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, size);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
