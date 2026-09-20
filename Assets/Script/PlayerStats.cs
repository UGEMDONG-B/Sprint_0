using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerStats : NetworkBehaviour
    {
        [Header("Stats")]
        [SerializeField, Min(1)] int maxHp = 10;
        [SerializeField, Min(1)] int attackDamage = 1;
        [SerializeField, Min(0f)] float invincibilityDuration = 0.75f;

        ThirdPersonCharacterMotor motor;
        bool invincible;

        readonly NetworkVariable<int> networkHp = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> networkDead = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        int localHp;
        bool localDead;

        public int CurrentHp => IsSpawned ? networkHp.Value : localHp;
        public int MaxHp => maxHp;
        public int AttackDamage => attackDamage;
        public bool IsDead => IsSpawned ? networkDead.Value : localDead;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePrototypePlayerStats()
        {
            var motor = FindFirstObjectByType<ThirdPersonCharacterMotor>();
            if (motor != null && motor.GetComponent<PlayerStats>() == null)
            {
                motor.gameObject.AddComponent<PlayerStats>();
            }
        }

        void Awake()
        {
            localHp = maxHp;
            motor = GetComponent<ThirdPersonCharacterMotor>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                networkHp.Value = maxHp;
                networkDead.Value = false;
            }
        }

        public void TakeDamage(int damage)
        {
            TryTakeDamage(damage);
        }

        public bool TryTakeDamage(int damage)
        {
            if ((IsSpawned && !IsServer) || IsDead || invincible || damage <= 0)
            {
                return false;
            }

            SetCurrentHp(Mathf.Max(0, CurrentHp - damage));
            if (CurrentHp <= 0)
            {
                Die();
                return true;
            }

            StartCoroutine(InvincibilityRoutine());
            return true;
        }

        IEnumerator InvincibilityRoutine()
        {
            invincible = true;
            yield return new WaitForSeconds(invincibilityDuration);
            invincible = false;
        }

        void Die()
        {
            if (IsSpawned)
            {
                networkDead.Value = true;
            }
            else
            {
                localDead = true;
            }
            invincible = true;
            if (motor != null)
            {
                motor.SetControlsEnabled(false);
            }

            var sharedWarrior = GetComponent<SharedTentacleWarriorNetwork>();
            if (sharedWarrior != null)
            {
                sharedWarrior.DeclareGameOver();
            }
            else
            {
                GameResultUI.Show(GameResultState.GameOver);
            }
        }

        void SetCurrentHp(int value)
        {
            if (IsSpawned)
            {
                networkHp.Value = value;
            }
            else
            {
                localHp = value;
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16f, 132f, 240f, 72f), GUI.skin.box);
            GUILayout.Label($"플레이어 체력: {CurrentHp} / {maxHp}");
            GUILayout.Label(IsDead ? "상태: 사망 (조작 불가)" : invincible ? "상태: 피격 무적" : "상태: 생존");
            GUILayout.EndArea();
        }
    }
}
