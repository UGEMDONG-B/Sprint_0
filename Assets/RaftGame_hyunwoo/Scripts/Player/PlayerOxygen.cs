using UnityEngine;

namespace RaftSharkDive
{
    [RequireComponent(typeof(PlayerHealth), typeof(Inventory))]
    public sealed class PlayerOxygen : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxOxygenWithTank = 60f;
        [SerializeField, Min(1f)] private float maxOxygenWithoutTank = 12f;
        [SerializeField, Min(0.1f)] private float surfaceRechargePerSecond = 24f;
        [SerializeField, Min(0.1f)] private float zeroOxygenDamagePerSecond = 15f;

        private PlayerController movement;
        private PlayerHealth health;
        private Inventory inventory;

        public float Current { get; private set; }
        public float Max => HasTank ? maxOxygenWithTank : maxOxygenWithoutTank;
        public bool HasTank => inventory != null && inventory.Has(ItemId.OxygenTank);

        private void Awake()
        {
            movement = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            inventory = GetComponent<Inventory>();
            Current = maxOxygenWithoutTank;
        }

        private void Update()
        {
            Current = Mathf.Min(Current, Max);
            if (movement != null && movement.IsSwimming)
            {
                Current = Mathf.Max(0f, Current - Time.deltaTime);
                if (Current <= 0f) health.Damage(zeroOxygenDamagePerSecond * Time.deltaTime);
            }
            else
            {
                Current = Mathf.Min(Max, Current + surfaceRechargePerSecond * Time.deltaTime);
            }
        }

        public void RestoreFull() => Current = Max;
        public void OnTankCrafted() => Current = maxOxygenWithTank;
        public void SetCurrent(float value) => Current = Mathf.Clamp(value, 0f, Max);
    }
}
