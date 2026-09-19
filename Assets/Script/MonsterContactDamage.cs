using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MonsterContactDamage : MonoBehaviour
    {
        [SerializeField, Min(1)] int attackDamage = 1;
        float damageMultiplier = 1f;
        bool consumed;

        public int AttackDamage => attackDamage;

        public void SetDamageMultiplier(float value)
        {
            damageMultiplier = Mathf.Max(0f, value);
        }

        void Awake()
        {
            var body = GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ;

            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                collider.isTrigger = true;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            TryDamage(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryDamage(other);
        }

        void TryDamage(Collider other)
        {
            if (consumed)
            {
                return;
            }

            var player = other.GetComponentInParent<PlayerStats>();
            if (player != null && player.TryTakeDamage(Mathf.Max(1, Mathf.RoundToInt(attackDamage * damageMultiplier))))
            {
                var health = GetComponent<MonsterHealth>();
                if (health != null && health.IsBoss)
                {
                    return;
                }

                consumed = true;
                foreach (var collider in GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
                Destroy(gameObject);
            }
        }
    }
}
