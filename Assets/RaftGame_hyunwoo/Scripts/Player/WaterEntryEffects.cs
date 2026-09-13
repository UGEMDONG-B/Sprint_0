using UnityEngine;

namespace RaftSharkDive
{
    [RequireComponent(typeof(PlayerWaterDetector))]
    public sealed class WaterEntryEffects : MonoBehaviour
    {
        private PlayerWaterDetector detector;
        private ParticleSystem splashParticles;
        private Texture2D overlayTexture;
        private Texture2D warningTexture;
        private float transitionFlash;

        public int SplashCount { get; private set; }
        public bool UnderwaterOverlayActive => detector != null && detector.IsInWater;

        private void Awake()
        {
            detector = GetComponent<PlayerWaterDetector>();
            BuildParticles();
            overlayTexture = MakeTexture(new Color(0.02f, 0.35f, 0.55f, 0.16f));
            warningTexture = MakeTexture(new Color(0.65f, 0.02f, 0.02f, 0.12f));
        }

        private void OnEnable()
        {
            if (detector != null) detector.WaterStateChanged += HandleWaterStateChanged;
        }

        private void OnDisable()
        {
            if (detector != null) detector.WaterStateChanged -= HandleWaterStateChanged;
        }

        private void Update()
        {
            transitionFlash = Mathf.MoveTowards(transitionFlash, 0f, Time.deltaTime * 1.8f);
        }

        private void HandleWaterStateChanged(bool inWater)
        {
            SplashCount++;
            transitionFlash = 1f;
            Vector3 position = transform.position;
            if (GameManager.Instance != null) position.y = GameManager.Instance.WaterSurfaceY;
            splashParticles.transform.position = position;
            splashParticles.Emit(inWater ? 26 : 18);
        }

        private void OnGUI()
        {
            if (detector != null && detector.IsInWater && overlayTexture != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayTexture);
            if (GameManager.Instance != null && GameManager.Instance.SharkThreatEnabled && warningTexture != null)
            {
                float pulse = 0.45f + Mathf.Sin(Time.time * 5f) * 0.25f;
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, pulse);
                const float edge = 20f;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, edge), warningTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - edge, Screen.width, edge), warningTexture);
                GUI.DrawTexture(new Rect(0, 0, edge, Screen.height), warningTexture);
                GUI.DrawTexture(new Rect(Screen.width - edge, 0, edge, Screen.height), warningTexture);
                GUI.color = previous;
            }
            if (transitionFlash > 0f && overlayTexture != null)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, transitionFlash * 0.45f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), overlayTexture);
                GUI.color = previous;
            }
        }

        private void BuildParticles()
        {
            GameObject particleObject = new GameObject("WaterSplashParticles");
            particleObject.transform.SetParent(transform);
            splashParticles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = splashParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = new Color(0.65f, 0.9f, 1f, 0.85f);
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.ShapeModule shape = splashParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
            ParticleSystem.EmissionModule emission = splashParticles.emission;
            emission.enabled = false;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

    }
}
