using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterHealth), typeof(MonsterChaseController), typeof(MonsterContactDamage))]
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField, Range(0.05f, 0.95f)] float enrageThreshold = 0.5f;
        [SerializeField, Min(1f)] float enrageMultiplier = 1.2f;

        MonsterHealth health;
        MonsterChaseController chase;
        MonsterContactDamage contactDamage;

        public MonsterHealth Health => health;
        public bool IsEnraged { get; private set; }

        void Awake()
        {
            health = GetComponent<MonsterHealth>();
            chase = GetComponent<MonsterChaseController>();
            contactDamage = GetComponent<MonsterContactDamage>();
        }

        void Update()
        {
            EvaluatePhase();
        }

        public void EvaluatePhase()
        {
            if (IsEnraged || health == null || health.IsDead || health.HealthNormalized > enrageThreshold)
            {
                return;
            }

            IsEnraged = true;
            chase?.SetSpeedMultiplier(enrageMultiplier);
            contactDamage?.SetDamageMultiplier(enrageMultiplier);
            TintRed();
        }

        void TintRed()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    if (material.HasProperty("_BaseColor"))
                    {
                        var color = material.GetColor("_BaseColor");
                        material.SetColor("_BaseColor", new Color(1f, color.g * 0.4f, color.b * 0.4f, color.a));
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        var color = material.color;
                        material.color = new Color(1f, color.g * 0.4f, color.b * 0.4f, color.a);
                    }
                }
            }
        }
    }
}
