using System.Collections;
using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class MonsterHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] int hp = 3;
        [SerializeField, Min(0)] int experienceReward = 1;
        [SerializeField] bool isBoss;

        bool dead;

        public int CurrentHp { get; private set; }
        public int MaxHp => hp;
        public bool IsBoss => isBoss;
        public bool IsDead => dead;
        public float HealthNormalized => hp > 0 ? Mathf.Clamp01((float)CurrentHp / hp) : 0f;

        void Awake()
        {
            CurrentHp = hp;
        }

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, null);
        }

        public void TakeDamage(int damage, TentacleProgression source)
        {
            if (dead)
            {
                return;
            }

            CurrentHp -= Mathf.Max(0, damage);
            if (CurrentHp <= 0)
            {
                dead = true;
                var receiver = source != null ? source : FindFirstObjectByType<TentacleProgression>();
                receiver?.AddExperience(experienceReward);
                Destroy(gameObject);
            }
        }

        public void ApplyBurn(int damagePerTick, float duration, float tickInterval, TentacleProgression source)
        {
            if (!dead)
            {
                StartCoroutine(BurnRoutine(
                    Mathf.Max(1, damagePerTick),
                    Mathf.Max(0.1f, duration),
                    Mathf.Max(0.1f, tickInterval),
                    source));
            }
        }

        IEnumerator BurnRoutine(int damagePerTick, float duration, float tickInterval, TentacleProgression source)
        {
            var elapsed = 0f;
            while (!dead && elapsed < duration)
            {
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
                TakeDamage(damagePerTick, source);
            }
        }
    }
}
