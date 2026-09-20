using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MonsterHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField, Min(1)] int hp = 3;
        [SerializeField, Min(0)] int experienceReward = 1;
        [SerializeField] bool isBoss;

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
        public int MaxHp => hp;
        public bool IsBoss => isBoss;
        public bool IsDead => IsSpawned ? networkDead.Value : localDead;
        public float HealthNormalized => hp > 0 ? Mathf.Clamp01((float)CurrentHp / hp) : 0f;

        void Awake()
        {
            localHp = hp;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                networkHp.Value = hp;
                networkDead.Value = false;
            }
        }

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, null);
        }

        public void TakeDamage(int damage, TentacleProgression source)
        {
            if ((IsSpawned && !IsServer) || IsDead)
            {
                return;
            }

            var remaining = Mathf.Max(0, CurrentHp - Mathf.Max(0, damage));
            SetCurrentHp(remaining);
            if (remaining <= 0)
            {
                SetDead(true);
                var receiver = source != null ? source : FindFirstObjectByType<TentacleProgression>();
                receiver?.AddExperience(experienceReward);
                if (isBoss)
                {
                    var sharedWarrior = FindFirstObjectByType<SharedTentacleWarriorNetwork>();
                    if (sharedWarrior != null)
                    {
                        sharedWarrior.DeclareGameClear();
                    }
                    else
                    {
                        GameResultUI.Show(GameResultState.GameClear);
                    }
                }
                RemoveFromWorld();
            }
        }

        public void ApplyBurn(int damagePerTick, float duration, float tickInterval, TentacleProgression source)
        {
            if ((!IsSpawned || IsServer) && !IsDead)
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
            while (!IsDead && elapsed < duration)
            {
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
                TakeDamage(damagePerTick, source);
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

        void SetDead(bool value)
        {
            if (IsSpawned)
            {
                networkDead.Value = value;
            }
            else
            {
                localDead = value;
            }
        }

        void RemoveFromWorld()
        {
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
