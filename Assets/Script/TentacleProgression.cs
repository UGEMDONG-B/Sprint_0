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
    public sealed class TentacleProgression : MonoBehaviour
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

        bool choicePending;

        public int Level => level;
        public int Experience => experience;
        public int ExperienceToNextLevel => experiencePerLevel;
        public int AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public float ProjectileScaleMultiplier => projectileScaleMultiplier;
        public TentacleEvolution Evolution => evolution;
        public GameObject CurrentProjectilePrefab => evolution switch
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

        public void AddExperience(int amount)
        {
            if (level >= 10 || choicePending || amount <= 0)
            {
                return;
            }

            experience += amount;
            if (experience < experiencePerLevel)
            {
                return;
            }

            experience -= experiencePerLevel;
            level++;
            choicePending = true;
            TentacleLevelUpUI.EnsureInstance().ShowChoices(this, level == 5);
        }

        public void ApplyStatUpgrade(TentacleStatUpgrade upgrade)
        {
            if (!choicePending)
            {
                return;
            }

            switch (upgrade)
            {
                case TentacleStatUpgrade.AttackDamage:
                    attackDamage++;
                    break;
                case TentacleStatUpgrade.AttackSpeed:
                    attackCooldown = Mathf.Max(0.1f, attackCooldown * 0.82f);
                    break;
                case TentacleStatUpgrade.ProjectileSize:
                    projectileScaleMultiplier *= 1.25f;
                    break;
                case TentacleStatUpgrade.MoveSpeed:
                    moveSpeedBonus += 0.6f;
                    characterMotor?.AddMoveSpeed(0.6f);
                    break;
            }

            choicePending = false;
        }

        public void SelectEvolution(TentacleEvolution selectedEvolution)
        {
            if (!choicePending || level != 5 || selectedEvolution == TentacleEvolution.None)
            {
                return;
            }

            evolution = selectedEvolution;
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
            }

            choicePending = false;
        }
    }
}
