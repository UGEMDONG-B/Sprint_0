using Unity.Netcode;
using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterHealth), typeof(MonsterChaseController), typeof(MonsterContactDamage))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class BossController : NetworkBehaviour
    {
        [SerializeField, Range(0.05f, 0.95f)] float enrageThreshold = 0.5f;
        [SerializeField, Min(1f)] float enrageMultiplier = 1.2f;

        MonsterHealth health;
        MonsterChaseController chase;
        MonsterContactDamage contactDamage;
        readonly NetworkVariable<bool> networkEnraged = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        bool localEnraged;
        bool visualEnrageApplied;

        public MonsterHealth Health => health;
        public bool IsEnraged => IsSpawned ? networkEnraged.Value : localEnraged;

        void Awake()
        {
            health = GetComponent<MonsterHealth>();
            chase = GetComponent<MonsterChaseController>();
            contactDamage = GetComponent<MonsterContactDamage>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            networkEnraged.OnValueChanged += OnEnragedChanged;
            if (networkEnraged.Value)
            {
                ApplyEnrageVisual();
            }
        }

        public override void OnNetworkDespawn()
        {
            networkEnraged.OnValueChanged -= OnEnragedChanged;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (!IsSpawned || IsServer)
            {
                EvaluatePhase();
            }
        }

        public void EvaluatePhase()
        {
            if (IsEnraged || health == null || health.IsDead || health.HealthNormalized > enrageThreshold)
            {
                return;
            }

            if (IsSpawned)
            {
                networkEnraged.Value = true;
            }
            else
            {
                localEnraged = true;
            }
            chase?.SetSpeedMultiplier(enrageMultiplier);
            contactDamage?.SetDamageMultiplier(enrageMultiplier);
            ApplyEnrageVisual();
        }

        void OnEnragedChanged(bool previous, bool current)
        {
            if (current)
            {
                ApplyEnrageVisual();
            }
        }

        void ApplyEnrageVisual()
        {
            if (visualEnrageApplied)
            {
                return;
            }

            visualEnrageApplied = true;
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
