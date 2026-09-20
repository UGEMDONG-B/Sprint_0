using Unity.Netcode;
using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MonsterContactDamage : NetworkBehaviour
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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer)
            {
                foreach (var collider in GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
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
            if ((IsSpawned && !IsServer) || consumed)
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
                if (IsSpawned && NetworkObject != null && NetworkObject.IsSpawned)
                {
                    NetworkObject.Despawn(true);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
