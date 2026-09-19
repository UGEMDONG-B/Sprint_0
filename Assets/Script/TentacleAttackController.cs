using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class TentacleAttackController : MonoBehaviour
    {
        const string EditorProjectilePath = "Assets/Art/Prefab/Attack_Standard.prefab";

        [Header("References")]
        [SerializeField] TentacleProceduralController tentacle;
        [SerializeField] ThirdPersonCharacterMotor characterMotor;
        [SerializeField] Camera aimingCamera;
        [SerializeField] GameObject projectilePrefab;
        [SerializeField] PlayerStats playerStats;
        [SerializeField] TentacleProgression progression;

        [Header("Attack")]
        [SerializeField, Min(0.01f)] float attackDelay = 0.5f;
        [SerializeField, Min(0.1f)] float projectileSpeed = 28f;
        [SerializeField, Min(1f)] float aimDistance = 300f;
        [SerializeField] LayerMask aimMask = ~0;

        public bool IsAttacking => characterMotor != null && characterMotor.IsAttacking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePrototypeAttackController()
        {
            var tentacleController = FindFirstObjectByType<TentacleProceduralController>();
            if (tentacleController == null)
            {
                return;
            }

            var attackController = tentacleController.GetComponent<TentacleAttackController>();
            if (attackController == null)
            {
                attackController = tentacleController.gameObject.AddComponent<TentacleAttackController>();
            }

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(EditorProjectilePath);
            attackController.Configure(
                tentacleController,
                tentacleController.transform.root.GetComponent<ThirdPersonCharacterMotor>(),
                Camera.main,
                prefab);
#endif
        }

        public void Configure(
            TentacleProceduralController tentacleController,
            ThirdPersonCharacterMotor motor,
            Camera camera,
            GameObject attackPrefab)
        {
            tentacle = tentacleController;
            characterMotor = motor;
            aimingCamera = camera;
            projectilePrefab = attackPrefab;
            playerStats = motor != null ? motor.GetComponent<PlayerStats>() : null;
            progression = GetComponent<TentacleProgression>();
        }

        void Awake()
        {
            if (tentacle == null)
            {
                tentacle = GetComponent<TentacleProceduralController>();
            }

            if (characterMotor == null)
            {
                characterMotor = transform.root.GetComponent<ThirdPersonCharacterMotor>();
            }

            if (aimingCamera == null)
            {
                aimingCamera = Camera.main;
            }

            if (playerStats == null)
            {
                playerStats = transform.root.GetComponent<PlayerStats>();
            }

            if (progression == null)
            {
                progression = GetComponent<TentacleProgression>();
            }
        }

        void Update()
        {
            if (progression == null)
            {
                progression = GetComponent<TentacleProgression>();
            }

            var selectedProjectile = progression != null
                ? progression.CurrentProjectilePrefab
                : projectilePrefab;
            if (!WasAttackPressed() || selectedProjectile == null || tentacle == null
                || characterMotor == null || characterMotor.IsMoving || characterMotor.IsAttacking
                || (playerStats != null && playerStats.IsDead))
            {
                return;
            }

            Fire();
        }

        void Fire()
        {
            var tip = tentacle.Tip;
            if (tip == null || aimingCamera == null)
            {
                return;
            }

            var effectiveAimDistance = Mathf.Max(aimDistance, 300f);
            var mousePosition = ReadMousePosition();
            var ray = aimingCamera.ScreenPointToRay(mousePosition);
            var targetPoint = ray.GetPoint(effectiveAimDistance);
            var hits = Physics.RaycastAll(
                ray,
                effectiveAimDistance,
                aimMask,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (!hit.transform.IsChildOf(transform.root))
                {
                    targetPoint = hit.point;
                    break;
                }
            }

            var direction = (targetPoint - tip.position).normalized;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var effectiveAttackDelay = progression != null ? progression.AttackCooldown : attackDelay;
            var selectedProjectile = progression != null
                ? progression.CurrentProjectilePrefab
                : projectilePrefab;
            characterMotor.LockMovementForAttack(effectiveAttackDelay);
            var instance = Instantiate(
                selectedProjectile,
                tip.position,
                Quaternion.LookRotation(direction, Vector3.up));
            if (progression != null)
            {
                instance.transform.localScale *= progression.ProjectileScaleMultiplier;
            }
            var projectile = instance.GetComponent<TentacleProjectile>();
            if (projectile == null)
            {
                projectile = instance.AddComponent<TentacleProjectile>();
            }

            projectile.SetDamage(progression != null
                ? progression.AttackDamage
                : playerStats != null ? playerStats.AttackDamage : 1);
            projectile.ConfigureEvolution(
                progression != null ? progression.Evolution : TentacleEvolution.None,
                progression);
            projectile.Launch(direction, Mathf.Max(projectileSpeed, 28f), transform.root);
        }

        static Vector2 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#endif
        }

        static bool WasAttackPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#elif ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
