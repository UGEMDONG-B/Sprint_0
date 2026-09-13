using UnityEngine;

namespace RaftSharkDive
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        private bool dead;

        public float Current { get; private set; }
        public float Max => maxHealth;

        private void Awake() => Current = maxHealth;

        public void Damage(float amount)
        {
            if (dead || amount <= 0f || GameManager.Instance?.Stage == GameStage.Ending) return;
            Current = Mathf.Max(0f, Current - amount);
            if (Current > 0f) return;
            dead = true;
            GameManager.Instance?.Respawn(GetComponent<PlayerController>());
            dead = false;
        }

        public void RestoreFull()
        {
            Current = maxHealth;
            dead = false;
        }

        public float Heal(float amount)
        {
            if (amount <= 0f || Current >= maxHealth) return 0f;
            float before = Current;
            Current = Mathf.Min(maxHealth, Current + amount);
            return Current - before;
        }

        public void SetCurrent(float value)
        {
            Current = Mathf.Clamp(value, 1f, maxHealth);
            dead = false;
        }
    }
}
