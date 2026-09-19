using UnityEngine;
using UnityEngine.UI;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class BossHealthUI : MonoBehaviour
    {
        GameObject panel;
        UnityEngine.UI.Text label;
        UnityEngine.UI.Image fill;
        BossController boss;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInstance()
        {
            if (FindFirstObjectByType<BossHealthUI>() != null)
            {
                return;
            }

            new GameObject("Step7_BossHealthUI").AddComponent<BossHealthUI>();
        }

        void Awake()
        {
            Build();
        }

        void Update()
        {
            if (boss == null)
            {
                boss = FindFirstObjectByType<BossController>();
            }

            var visible = boss != null && boss.Health != null && !boss.Health.IsDead;
            panel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var health = boss.Health;
            fill.fillAmount = health.HealthNormalized;
            label.text = boss.IsEnraged
                ? $"보스 · 광폭화   {health.CurrentHp} / {health.MaxHp}"
                : $"보스   {health.CurrentHp} / {health.MaxHp}";
        }

        void Build()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreateRect("BossHealthPanel", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(820f, 100f), new Vector2(0f, -70f));
            var background = panel.AddComponent<Image>();
            background.color = new Color(0.08f, 0.015f, 0.02f, 0.9f);
            background.raycastTarget = false;

            label = CreateText("Label", panel.transform, 30, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = new Vector2(0.03f, 0.45f);
            label.rectTransform.anchorMax = new Vector2(0.97f, 0.95f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            var bar = CreateRect("Bar", panel.transform, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.4f), Vector2.zero, Vector2.zero);
            var barImage = bar.AddComponent<Image>();
            barImage.color = new Color(0.17f, 0.03f, 0.04f, 1f);
            barImage.raycastTarget = false;

            var fillObject = CreateRect("Fill", bar.transform, Vector2.zero, Vector2.one, new Vector2(-8f, -8f), Vector2.zero);
            fill = fillObject.AddComponent<Image>();
            fill.color = new Color(0.88f, 0.08f, 0.1f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;
            panel.SetActive(false);
        }

        static GameObject CreateRect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 size, Vector2 position)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return value;
        }

        static UnityEngine.UI.Text CreateText(string name, Transform parent, int size, TextAnchor alignment)
        {
            var value = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = value.AddComponent<UnityEngine.UI.Text>();
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, size);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
