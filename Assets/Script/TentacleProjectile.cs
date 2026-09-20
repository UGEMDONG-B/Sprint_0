using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    public sealed class TentacleProjectile : NetworkBehaviour
    {
        [SerializeField, Min(1)] int damage = 1;
        [SerializeField, Min(0.1f)] float lifetime = 12f;
        [SerializeField, Min(1)] int pierceTargetCount = 4;
        [SerializeField, Min(0.1f)] float fireRadius = 2.75f;
        [SerializeField, Min(0.1f)] float burnDuration = 3f;
        [SerializeField, Min(0.1f)] float burnInterval = 1f;
        [SerializeField, Min(0f)] float strikeKnockbackForce = 9f;
        [SerializeField, Min(1f)] float strikeDamageMultiplier = 2f;

        readonly HashSet<int> hitMonsterIds = new();
        Rigidbody body;
        SphereCollider hitbox;
        Collider[] projectileColliders;
        Transform ownerRoot;
        TentacleProgression sourceProgression;
        TentacleEvolution evolution;
        Vector3 previousPosition;
        int monsterMask;
        int piercedTargets;
        bool consumed;
        Coroutine lifetimeRoutine;

        public void SetDamage(int value) => damage = Mathf.Max(1, value);

        public void ConfigureEvolution(TentacleEvolution value, TentacleProgression source)
        {
            evolution = value;
            sourceProgression = source;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitbox = GetComponent<SphereCollider>();
            projectileColliders = GetComponentsInChildren<Collider>(true);
            foreach (var collider in projectileColliders)
            {
                collider.isTrigger = true;
            }

            body.useGravity = false;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            monsterMask = LayerMask.GetMask("Monster");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer)
            {
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
                foreach (var collider in projectileColliders)
                {
                    collider.enabled = false;
                }
            }
        }

        public void Launch(Vector3 direction, float speed, Transform owner)
        {
            ownerRoot = owner != null ? owner.root : null;
            previousPosition = transform.position;
            body.linearVelocity = direction.normalized * speed;

            if (ownerRoot != null)
            {
                foreach (var ownerCollider in ownerRoot.GetComponentsInChildren<Collider>(true))
                {
                    foreach (var projectileCollider in projectileColliders)
                    {
                        Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
                    }
                }
            }

            if (!IsSpawned || IsServer)
            {
                lifetimeRoutine = StartCoroutine(ExpireAfterDelay(Mathf.Max(lifetime, 12f)));
            }
        }

        void FixedUpdate()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            if (consumed || monsterMask == 0)
            {
                previousPosition = body.position;
                return;
            }

            var delta = body.position - previousPosition;
            var distance = delta.magnitude;
            if (distance > 0.0001f && Physics.SphereCast(
                previousPosition,
                hitbox.radius * MaxAbsScale(transform.lossyScale),
                delta / distance,
                out var hit,
                distance,
                monsterMask,
                QueryTriggerInteraction.Collide))
            {
                ResolveHit(hit.collider);
            }

            previousPosition = body.position;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsSpawned || IsServer)
            {
                ResolveHit(other);
            }
        }

        void ResolveHit(Collider other)
        {
            if (consumed || other == null || (ownerRoot != null && other.transform.IsChildOf(ownerRoot)))
            {
                return;
            }

            var monster = other.GetComponentInParent<MonsterHealth>();
            if (monster == null || !hitMonsterIds.Add(monster.GetInstanceID()))
            {
                return;
            }

            switch (evolution)
            {
                case TentacleEvolution.Fire:
                    ResolveFire(monster);
                    Consume();
                    break;
                case TentacleEvolution.Pierce:
                    monster.TakeDamage(damage, sourceProgression);
                    piercedTargets++;
                    if (piercedTargets >= pierceTargetCount)
                    {
                        Consume();
                    }
                    break;
                case TentacleEvolution.Strike:
                    ResolveStrike(monster);
                    Consume();
                    break;
                default:
                    monster.TakeDamage(damage, sourceProgression);
                    Consume();
                    break;
            }
        }

        void ResolveFire(MonsterHealth primary)
        {
            primary.TakeDamage(damage, sourceProgression);
            primary.ApplyBurn(Mathf.Max(1, damage / 2), burnDuration, burnInterval, sourceProgression);

            var overlaps = Physics.OverlapSphere(
                primary.transform.position,
                fireRadius,
                monsterMask,
                QueryTriggerInteraction.Collide);
            foreach (var overlap in overlaps)
            {
                var nearby = overlap.GetComponentInParent<MonsterHealth>();
                if (nearby == null || nearby == primary || !hitMonsterIds.Add(nearby.GetInstanceID()))
                {
                    continue;
                }

                nearby.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * 0.65f)), sourceProgression);
                nearby.ApplyBurn(Mathf.Max(1, damage / 2), burnDuration, burnInterval, sourceProgression);
            }
        }

        void ResolveStrike(MonsterHealth monster)
        {
            monster.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * strikeDamageMultiplier)), sourceProgression);
            if (monster == null)
            {
                return;
            }

            var monsterBody = monster.GetComponent<Rigidbody>();
            if (monster.IsBoss || monsterBody == null)
            {
                return;
            }

            monster.GetComponent<MonsterChaseController>()?.BeginKnockback(0.35f);
            monsterBody.WakeUp();
            var direction = body.linearVelocity.sqrMagnitude > 0.001f
                ? body.linearVelocity.normalized
                : transform.forward;
            monsterBody.AddForce(direction * strikeKnockbackForce, ForceMode.Impulse);
        }

        void Consume()
        {
            consumed = true;
            RemoveFromWorld();
        }

        System.Collections.IEnumerator ExpireAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            RemoveFromWorld();
        }

        void RemoveFromWorld()
        {
            if (lifetimeRoutine != null)
            {
                StopCoroutine(lifetimeRoutine);
                lifetimeRoutine = null;
            }

            if (IsSpawned && NetworkObject != null && NetworkObject.IsSpawned)
            {
                if (IsServer)
                {
                    NetworkObject.Despawn(true);
                }
                return;
            }

            Destroy(gameObject);
        }

        static float MaxAbsScale(Vector3 scale)
        {
            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
    }
}
