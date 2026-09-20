using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class TentacleLevelUpUI : MonoBehaviour
    {
        static readonly TentacleStatUpgrade[] AllStats =
        {
            TentacleStatUpgrade.AttackDamage,
            TentacleStatUpgrade.AttackSpeed,
            TentacleStatUpgrade.ProjectileSize,
            TentacleStatUpgrade.MoveSpeed
        };

        static TentacleLevelUpUI instance;
        GameObject overlay;
        UnityEngine.UI.Text statusText;
        UnityEngine.UI.Image experienceFill;
        UnityEngine.UI.Text title;
        UnityEngine.UI.Button[] buttons;
        UnityEngine.UI.Text[] buttonLabels;
        TentacleProgression displayedProgression;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureHud()
        {
            EnsureInstance();
        }

        public static TentacleLevelUpUI EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<TentacleLevelUpUI>();
            if (instance == null)
            {
                var root = new GameObject("Step6_LevelUpUI");
                instance = root.AddComponent<TentacleLevelUpUI>();
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

        void Update()
        {
            if (statusText == null || experienceFill == null)
            {
                return;
            }

            if (displayedProgression == null)
            {
                displayedProgression = FindFirstObjectByType<TentacleProgression>();
            }

            var progression = displayedProgression;
            if (progression == null)
            {
                statusText.text = "촉수 성장 정보를 찾는 중...";
                experienceFill.fillAmount = 0f;
                return;
            }

            var evolution = progression.Evolution == TentacleEvolution.None
                ? "기본"
                : progression.Evolution switch
                {
                    TentacleEvolution.Fire => "화염",
                    TentacleEvolution.Pierce => "관통",
                    TentacleEvolution.Strike => "타격",
                    _ => progression.Evolution.ToString()
                };
            statusText.text = $"촉수 LV.{progression.Level}  EXP {progression.Experience} / {progression.ExperienceToNextLevel}  ·  {evolution}";
            experienceFill.fillAmount = progression.Level >= 10
                ? 1f
                : Mathf.Clamp01((float)progression.Experience / progression.ExperienceToNextLevel);
        }

        public void SetDisplayedProgression(TentacleProgression progression)
        {
            displayedProgression = progression;
        }

        public void ShowChoices(TentacleProgression progression, bool evolutionChoice)
        {
            BuildIfNeeded();
            overlay.SetActive(true);
            title.text = evolutionChoice
                ? $"레벨 {progression.Level} · 촉수 진화 선택"
                : $"레벨 {progression.Level} · 성장 선택";

            if (evolutionChoice)
            {
                SetButton(0, "화염\n범위 피해 + 지속 피해", () => SelectEvolution(progression, TentacleEvolution.Fire));
                SetButton(1, "관통\n최대 4마리 관통", () => SelectEvolution(progression, TentacleEvolution.Pierce));
                SetButton(2, "타격\n강한 피해 + 넉백", () => SelectEvolution(progression, TentacleEvolution.Strike));
                return;
            }

            var choices = new List<TentacleStatUpgrade>(AllStats);
            for (var i = choices.Count - 1; i > 0; i--)
            {
                var swap = UnityEngine.Random.Range(0, i + 1);
                (choices[i], choices[swap]) = (choices[swap], choices[i]);
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                var choice = choices[i];
                SetButton(i, StatLabel(choice), () => SelectStat(progression, choice));
            }
        }

        void SelectStat(TentacleProgression progression, TentacleStatUpgrade choice)
        {
            progression.ApplyStatUpgrade(choice);
            Close();
        }

        void SelectEvolution(TentacleProgression progression, TentacleEvolution choice)
        {
            progression.SelectEvolution(choice);
            Close();
        }

        void Close()
        {
            overlay.SetActive(false);
        }

        void SetButton(int index, string label, UnityEngine.Events.UnityAction action)
        {
            buttonLabels[index].text = label;
            buttons[index].onClick.RemoveAllListeners();
            buttons[index].onClick.AddListener(action);
        }

        void BuildIfNeeded()
        {
            if (overlay != null)
            {
                return;
            }

            EnsureEventSystem();
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            BuildStatusHud();

            overlay = CreateRect("PauseOverlay", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0.02f, 0.025f, 0.04f, 0.82f);

            var panel = CreateRect("ChoicePanel", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(980f, 460f), Vector2.zero);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.09f, 0.11f, 0.17f, 0.98f);

            title = CreateText("Title", panel.transform, 42, TextAnchor.MiddleCenter);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.05f, 0.72f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

            buttons = new UnityEngine.UI.Button[3];
            buttonLabels = new UnityEngine.UI.Text[3];
            for (var i = 0; i < 3; i++)
            {
                var xMin = 0.055f + i * 0.315f;
                var buttonObject = CreateRect(
                    $"Choice_{i + 1}",
                    panel.transform,
                    new Vector2(xMin, 0.12f),
                    new Vector2(xMin + 0.26f, 0.65f),
                    Vector2.zero,
                    Vector2.zero);
                var image = buttonObject.AddComponent<Image>();
                image.color = new Color(0.18f, 0.28f, 0.42f, 1f);
                var button = buttonObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                var colors = button.colors;
                colors.highlightedColor = new Color(0.28f, 0.48f, 0.7f, 1f);
                colors.pressedColor = new Color(0.12f, 0.2f, 0.32f, 1f);
                button.colors = colors;
                buttons[i] = button;
                buttonLabels[i] = CreateText("Label", buttonObject.transform, 28, TextAnchor.MiddleCenter);
                buttonLabels[i].rectTransform.anchorMin = Vector2.zero;
                buttonLabels[i].rectTransform.anchorMax = Vector2.one;
                buttonLabels[i].rectTransform.offsetMin = new Vector2(14f, 14f);
                buttonLabels[i].rectTransform.offsetMax = new Vector2(-14f, -14f);
            }

            overlay.SetActive(false);
        }

        void BuildStatusHud()
        {
            var hud = CreateRect(
                "ProgressHud",
                transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(520f, 96f),
                new Vector2(-280f, -60f));
            var hudImage = hud.AddComponent<Image>();
            hudImage.color = new Color(0.025f, 0.035f, 0.055f, 0.88f);
            hudImage.raycastTarget = false;

            statusText = CreateText("Status", hud.transform, 25, TextAnchor.MiddleLeft);
            statusText.rectTransform.anchorMin = new Vector2(0.05f, 0.43f);
            statusText.rectTransform.anchorMax = new Vector2(0.95f, 0.92f);
            statusText.rectTransform.offsetMin = statusText.rectTransform.offsetMax = Vector2.zero;
            statusText.raycastTarget = false;

            var barBackground = CreateRect(
                "ExperienceBar",
                hud.transform,
                new Vector2(0.05f, 0.15f),
                new Vector2(0.95f, 0.37f),
                Vector2.zero,
                Vector2.zero);
            var backgroundImage = barBackground.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.15f, 0.21f, 1f);
            backgroundImage.raycastTarget = false;

            var fillObject = CreateRect(
                "Fill",
                barBackground.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(-8f, -8f),
                Vector2.zero);
            experienceFill = fillObject.AddComponent<Image>();
            experienceFill.color = new Color(0.2f, 0.78f, 0.96f, 1f);
            experienceFill.type = Image.Type.Filled;
            experienceFill.fillMethod = Image.FillMethod.Horizontal;
            experienceFill.fillOrigin = 0;
            experienceFill.fillAmount = 0f;
            experienceFill.raycastTarget = false;
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

        static UnityEngine.UI.Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment)
        {
            var textObject = CreateRect(objectName, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = textObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, size);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        static string StatLabel(TentacleStatUpgrade upgrade)
        {
            return upgrade switch
            {
                TentacleStatUpgrade.AttackDamage => "공격력 증가\n피해량 +1",
                TentacleStatUpgrade.AttackSpeed => "공격 속도 증가\n재사용 대기시간 -18%",
                TentacleStatUpgrade.ProjectileSize => "투사체 크기 증가\n크기 +25%",
                TentacleStatUpgrade.MoveSpeed => "이동 속도 증가\n속도 +0.6",
                _ => upgrade.ToString()
            };
        }
    }
}
