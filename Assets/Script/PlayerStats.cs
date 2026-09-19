using System.Collections;
using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField, Min(1)] int maxHp = 10;
        [SerializeField, Min(1)] int attackDamage = 1;
        [SerializeField, Min(0f)] float invincibilityDuration = 0.75f;

        ThirdPersonCharacterMotor motor;
        bool invincible;

        public int CurrentHp { get; private set; }
        public int MaxHp => maxHp;
        public int AttackDamage => attackDamage;
        public bool IsDead { get; private set; }

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
            CurrentHp = maxHp;
            motor = GetComponent<ThirdPersonCharacterMotor>();
        }

        public void TakeDamage(int damage)
        {
            TryTakeDamage(damage);
        }

        public bool TryTakeDamage(int damage)
        {
            if (IsDead || invincible || damage <= 0)
            {
                return false;
            }

            CurrentHp = Mathf.Max(0, CurrentHp - damage);
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
            IsDead = true;
            invincible = true;
            if (motor != null)
            {
                motor.SetControlsEnabled(false);
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
