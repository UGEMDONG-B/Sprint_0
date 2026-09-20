using Unity.Netcode;
using UnityEngine;

namespace Sprint0.Prototype
{
    public enum TentacleEvolution
    {
        None,
        Fire,
        Pierce,
        Strike
    }

    public enum TentacleStatUpgrade
    {
        AttackDamage,
        AttackSpeed,
        ProjectileSize,
        MoveSpeed
    }

    [DisallowMultipleComponent]
    public sealed class TentacleProgression : NetworkBehaviour
    {
        const string StandardProjectilePath = "Assets/Art/Prefab/Attack_Standard.prefab";
        const string FireProjectilePath = "Assets/Art/Prefab/Attack_Fire.prefab";
        const string PierceProjectilePath = "Assets/Art/Prefab/Attack_Spear.prefab";
        const string StrikeProjectilePath = "Assets/Art/Prefab/Attack_Break.prefab";

        [Header("References")]
        [SerializeField] TentacleProceduralController tentacle;
        [SerializeField] ThirdPersonCharacterMotor characterMotor;
        [SerializeField] GameObject standardProjectilePrefab;
        [SerializeField] GameObject fireProjectilePrefab;
        [SerializeField] GameObject pierceProjectilePrefab;
        [SerializeField] GameObject strikeProjectilePrefab;

        [Header("Optional evolved visuals")]
        [SerializeField] GameObject fireVisualPrefab;
        [SerializeField] GameObject pierceVisualPrefab;
        [SerializeField] GameObject strikeVisualPrefab;

        [Header("Progress")]
        [SerializeField, Range(1, 10)] int level = 1;
        [SerializeField, Min(0)] int experience;
        [SerializeField, Min(1)] int experiencePerLevel = 3;

        [Header("Per-tentacle stats")]
        [SerializeField, Min(1)] int attackDamage = 1;
        [SerializeField, Min(0.1f)] float attackCooldown = 0.5f;
        [SerializeField, Min(0.1f)] float projectileScaleMultiplier = 1f;
        [SerializeField, Min(0f)] float moveSpeedBonus;
        [SerializeField] TentacleEvolution evolution;

        readonly NetworkVariable<int> networkLevel = new(1);
        readonly NetworkVariable<int> networkExperience = new(0);
        readonly NetworkVariable<int> networkAttackDamage = new(1);
        readonly NetworkVariable<float> networkAttackCooldown = new(0.5f);
        readonly NetworkVariable<float> networkProjectileScale = new(1f);
        readonly NetworkVariable<float> networkMoveSpeedBonus = new(0f);
        readonly NetworkVariable<TentacleEvolution> networkEvolution = new(TentacleEvolution.None);
        readonly NetworkVariable<bool> networkChoicePending = new(false);

        bool localChoicePending;
        bool choiceUiShown;
        TentacleEvolution appliedVisualEvolution;

        public int Level => IsSpawned ? networkLevel.Value : level;
        public int Experience => IsSpawned ? networkExperience.Value : experience;
        public int ExperienceToNextLevel => experiencePerLevel;
        public int AttackDamage => IsSpawned ? networkAttackDamage.Value : attackDamage;
        public float AttackCooldown => IsSpawned ? networkAttackCooldown.Value : attackCooldown;
        public float ProjectileScaleMultiplier => IsSpawned
            ? networkProjectileScale.Value
            : projectileScaleMultiplier;
        public TentacleEvolution Evolution => IsSpawned ? networkEvolution.Value : evolution;
        public bool ChoicePending => IsSpawned ? networkChoicePending.Value : localChoicePending;
        public bool IsLocallyControlled
        {
            get
            {
                var shared = GetComponentInParent<SharedTentacleWarriorNetwork>();
                return shared != null && shared.LocalSlot == shared.GetTentacleSlot(tentacle);
            }
        }

        public GameObject CurrentProjectilePrefab => Evolution switch
        {
            TentacleEvolution.Fire => fireProjectilePrefab != null ? fireProjectilePrefab : standardProjectilePrefab,
            TentacleEvolution.Pierce => pierceProjectilePrefab != null ? pierceProjectilePrefab : standardProjectilePrefab,
            TentacleEvolution.Strike => strikeProjectilePrefab != null ? strikeProjectilePrefab : standardProjectilePrefab,
            _ => standardProjectilePrefab
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureProgression()
        {
            var controller = FindFirstObjectByType<TentacleProceduralController>();
            if (controller == null)
            {
                return;
            }

            var progression = controller.GetComponent<TentacleProgression>();
            if (progression == null)
            {
                progression = controller.gameObject.AddComponent<TentacleProgression>();
            }

#if UNITY_EDITOR
            progression.Configure(
                controller,
                controller.transform.root.GetComponent<ThirdPersonCharacterMotor>(),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(StandardProjectilePath),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FireProjectilePath),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PierceProjectilePath),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(StrikeProjectilePath));
#endif
        }

        public void Configure(
            TentacleProceduralController controller,
            ThirdPersonCharacterMotor motor,
            GameObject standardProjectile,
            GameObject fireProjectile,
            GameObject pierceProjectile,
            GameObject strikeProjectile)
        {
            tentacle = controller;
            characterMotor = motor;
            standardProjectilePrefab = standardProjectile;
            fireProjectilePrefab = fireProjectile;
            pierceProjectilePrefab = pierceProjectile;
            strikeProjectilePrefab = strikeProjectile;
        }

        void Awake()
        {
            tentacle = tentacle != null ? tentacle : GetComponent<TentacleProceduralController>();
            characterMotor = characterMotor != null
                ? characterMotor
                : transform.root.GetComponent<ThirdPersonCharacterMotor>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            networkEvolution.OnValueChanged += OnEvolutionChanged;
            networkChoicePending.OnValueChanged += OnChoicePendingChanged;

            if (IsServer)
            {
                networkLevel.Value = level;
                networkExperience.Value = experience;
                networkAttackDamage.Value = attackDamage;
                networkAttackCooldown.Value = attackCooldown;
                networkProjectileScale.Value = projectileScaleMultiplier;
                networkMoveSpeedBonus.Value = moveSpeedBonus;
                networkEvolution.Value = evolution;
                networkChoicePending.Value = false;
            }

            ApplyEvolutionVisual(networkEvolution.Value);
        }

        public override void OnNetworkDespawn()
        {
            networkEvolution.OnValueChanged -= OnEvolutionChanged;
            networkChoicePending.OnValueChanged -= OnChoicePendingChanged;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (ChoicePending && !choiceUiShown && IsLocallyControlled)
            {
                choiceUiShown = true;
                TentacleLevelUpUI.EnsureInstance().ShowChoices(this, Level == 5);
            }
            else if (!ChoicePending)
            {
                choiceUiShown = false;
            }
        }

        public void AddExperience(int amount)
        {
            if ((IsSpawned && !IsServer) || Level >= 10 || ChoicePending || amount <= 0)
            {
                return;
            }

            var nextExperience = Experience + amount;
            if (nextExperience < experiencePerLevel)
            {
                SetExperience(nextExperience);
                return;
            }

            SetExperience(nextExperience - experiencePerLevel);
            SetLevel(Level + 1);
            SetChoicePending(true);

            if (!IsSpawned)
            {
                TentacleLevelUpUI.EnsureInstance().ShowChoices(this, Level == 5);
            }
        }

        public void ApplyStatUpgrade(TentacleStatUpgrade upgrade)
        {
            if (IsSpawned)
            {
                RequestStatUpgradeServerRpc(upgrade);
                return;
            }

            ApplyStatUpgradeInternal(upgrade);
        }

        [Rpc(SendTo.Server)]
        void RequestStatUpgradeServerRpc(
            TentacleStatUpgrade upgrade,
            RpcParams rpcParams = default)
        {
            if (IsAuthorized(rpcParams.Receive.SenderClientId))
            {
                ApplyStatUpgradeInternal(upgrade);
            }
        }

        void ApplyStatUpgradeInternal(TentacleStatUpgrade upgrade)
        {
            if (!ChoicePending || Level == 5)
            {
                return;
            }

            switch (upgrade)
            {
                case TentacleStatUpgrade.AttackDamage:
                    SetAttackDamage(AttackDamage + 1);
                    break;
                case TentacleStatUpgrade.AttackSpeed:
                    SetAttackCooldown(Mathf.Max(0.1f, AttackCooldown * 0.82f));
                    break;
                case TentacleStatUpgrade.ProjectileSize:
                    SetProjectileScale(ProjectileScaleMultiplier * 1.25f);
                    break;
                case TentacleStatUpgrade.MoveSpeed:
                    SetMoveSpeedBonus(CurrentMoveSpeedBonus() + 0.6f);
                    characterMotor?.AddMoveSpeed(0.6f);
                    break;
                default:
                    return;
            }

            SetChoicePending(false);
        }

        public void SelectEvolution(TentacleEvolution selectedEvolution)
        {
            if (IsSpawned)
            {
                RequestEvolutionServerRpc(selectedEvolution);
                return;
            }

            SelectEvolutionInternal(selectedEvolution);
        }

        [Rpc(SendTo.Server)]
        void RequestEvolutionServerRpc(
            TentacleEvolution selectedEvolution,
            RpcParams rpcParams = default)
        {
            if (IsAuthorized(rpcParams.Receive.SenderClientId))
            {
                SelectEvolutionInternal(selectedEvolution);
            }
        }

        void SelectEvolutionInternal(TentacleEvolution selectedEvolution)
        {
            if (!ChoicePending || Level != 5 || selectedEvolution == TentacleEvolution.None)
            {
                return;
            }

            if (selectedEvolution != TentacleEvolution.Fire
                && selectedEvolution != TentacleEvolution.Pierce
                && selectedEvolution != TentacleEvolution.Strike)
            {
                return;
            }

            if (IsSpawned)
            {
                networkEvolution.Value = selectedEvolution;
            }
            else
            {
                evolution = selectedEvolution;
                ApplyEvolutionVisual(selectedEvolution);
            }

            SetChoicePending(false);
        }

        bool IsAuthorized(ulong clientId)
        {
            var shared = GetComponentInParent<SharedTentacleWarriorNetwork>();
            var slot = shared != null ? shared.GetTentacleSlot(tentacle) : -1;
            return shared != null && shared.IsClientAssignedToSlot(clientId, slot);
        }

        void OnEvolutionChanged(TentacleEvolution previous, TentacleEvolution current)
        {
            ApplyEvolutionVisual(current);
        }

        void OnChoicePendingChanged(bool previous, bool current)
        {
            if (!current)
            {
                choiceUiShown = false;
            }
        }

        void ApplyEvolutionVisual(TentacleEvolution selectedEvolution)
        {
            if (selectedEvolution == TentacleEvolution.None
                || selectedEvolution == appliedVisualEvolution)
            {
                return;
            }

            var visual = selectedEvolution switch
            {
                TentacleEvolution.Fire => fireVisualPrefab,
                TentacleEvolution.Pierce => pierceVisualPrefab,
                TentacleEvolution.Strike => strikeVisualPrefab,
                _ => null
            };
            if (visual != null)
            {
                tentacle?.ReplaceVisual(visual);
                appliedVisualEvolution = selectedEvolution;
            }
        }

        float CurrentMoveSpeedBonus()
        {
            return IsSpawned ? networkMoveSpeedBonus.Value : moveSpeedBonus;
        }

        void SetLevel(int value)
        {
            if (IsSpawned) networkLevel.Value = value;
            else level = value;
        }

        void SetExperience(int value)
        {
            if (IsSpawned) networkExperience.Value = value;
            else experience = value;
        }

        void SetAttackDamage(int value)
        {
            if (IsSpawned) networkAttackDamage.Value = value;
            else attackDamage = value;
        }

        void SetAttackCooldown(float value)
        {
            if (IsSpawned) networkAttackCooldown.Value = value;
            else attackCooldown = value;
        }

        void SetProjectileScale(float value)
        {
            if (IsSpawned) networkProjectileScale.Value = value;
            else projectileScaleMultiplier = value;
        }

        void SetMoveSpeedBonus(float value)
        {
            if (IsSpawned) networkMoveSpeedBonus.Value = value;
            else moveSpeedBonus = value;
        }

        void SetChoicePending(bool value)
        {
            if (IsSpawned) networkChoicePending.Value = value;
            else localChoicePending = value;
        }
    }
}
