using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class MonsterWaveManager : MonoBehaviour
    {
        const string EditorMonsterPath = "Assets/Art/Prefab/몬스터.prefab";
        const string EditorBossPath = "Assets/Art/Prefab/보스.prefab";

        [Header("References")]
        [SerializeField] GameObject monsterPrefab;
        [SerializeField] GameObject bossPrefab;
        [SerializeField] Transform playerTarget;
        [SerializeField] Transform[] spawnPoints;

        [Header("Wave")]
        [SerializeField, Min(0f)] float initialDelay = 1.5f;
        [SerializeField, Min(0.1f)] float spawnInterval = 3f;
        [SerializeField, Min(1)] int maximumAliveMonsters = 30;
        [SerializeField, Min(1f)] float fallbackSpawnRadius = 18f;

        readonly List<GameObject> aliveMonsters = new();
        Coroutine spawnRoutine;
        GameObject bossInstance;
        bool bossWaveStarted;

        public bool BossWaveStarted => bossWaveStarted;
        public GameObject BossInstance => bossInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePrototypeWaveManager()
        {
            var existing = FindFirstObjectByType<MonsterWaveManager>();
            var player = FindFirstObjectByType<ThirdPersonCharacterMotor>();
            if (player == null)
            {
                return;
            }

            if (existing == null)
            {
                existing = new GameObject("Step4_WaveManager").AddComponent<MonsterWaveManager>();
            }

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(EditorMonsterPath);
            var boss = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(EditorBossPath);
            existing.Configure(prefab, player.transform, existing.spawnPoints, boss);
#else
            existing.Configure(existing.monsterPrefab, player.transform, existing.spawnPoints, existing.bossPrefab);
#endif
        }

        public void Configure(GameObject prefab, Transform target, Transform[] points, GameObject boss = null)
        {
            monsterPrefab = prefab;
            if (boss != null)
            {
                bossPrefab = boss;
            }
            playerTarget = target;
            if (points != null && points.Length > 0)
            {
                spawnPoints = points;
            }

            EnsureSpawnPoints();
            if (isActiveAndEnabled && spawnRoutine == null)
            {
                spawnRoutine = StartCoroutine(SpawnLoop());
            }
        }

        void Start()
        {
            if (playerTarget == null)
            {
                var player = FindFirstObjectByType<ThirdPersonCharacterMotor>();
                playerTarget = player != null ? player.transform : null;
            }

            EnsureSpawnPoints();
            if (monsterPrefab != null && playerTarget != null && spawnRoutine == null)
            {
                spawnRoutine = StartCoroutine(SpawnLoop());
            }
        }

        IEnumerator SpawnLoop()
        {
            if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            while (enabled)
            {
                RemoveDestroyedMonsters();
                if (AreAllTentaclesReadyForBoss())
                {
                    SpawnBoss();
                    spawnRoutine = null;
                    yield break;
                }

                if (aliveMonsters.Count < maximumAliveMonsters)
                {
                    SpawnMonster();
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        public bool TryStartBossWave()
        {
            if (!AreAllTentaclesReadyForBoss())
            {
                return false;
            }

            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            SpawnBoss();
            return bossWaveStarted;
        }

        bool AreAllTentaclesReadyForBoss()
        {
            var progressions = FindObjectsByType<TentacleProgression>(FindObjectsSortMode.None);
            if (progressions.Length == 0)
            {
                return false;
            }

            foreach (var progression in progressions)
            {
                if (progression.Level < 5)
                {
                    return false;
                }
            }

            return true;
        }

        void SpawnBoss()
        {
            if (bossWaveStarted || bossPrefab == null || playerTarget == null
                || spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            var point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point == null)
            {
                return;
            }

            bossWaveStarted = true;
            bossInstance = Instantiate(bossPrefab, point.position, point.rotation);
            bossInstance.name = "Step7_Boss";
            SetLayerRecursively(bossInstance, LayerMask.NameToLayer("Monster"));
            bossInstance.GetComponent<MonsterChaseController>()?.Initialize(playerTarget);
        }

        void SpawnMonster()
        {
            if (monsterPrefab == null || playerTarget == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            var point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point == null)
            {
                return;
            }

            var instance = Instantiate(monsterPrefab, point.position, point.rotation);
            instance.name = $"Monster_{Time.frameCount}";
            SetLayerRecursively(instance, LayerMask.NameToLayer("Monster"));

            var health = instance.GetComponent<MonsterHealth>();
            if (health == null)
            {
                health = instance.AddComponent<MonsterHealth>();
            }

            var contactDamage = instance.GetComponent<MonsterContactDamage>();
            if (contactDamage == null)
            {
                contactDamage = instance.AddComponent<MonsterContactDamage>();
            }

            var chase = instance.GetComponent<MonsterChaseController>();
            if (chase == null)
            {
                chase = instance.AddComponent<MonsterChaseController>();
            }

            chase.Initialize(playerTarget);
            aliveMonsters.Add(instance);
        }

        void EnsureSpawnPoints()
        {
            if (spawnPoints != null && spawnPoints.Length > 0 || playerTarget == null)
            {
                return;
            }

            var center = playerTarget.position;
            var offsets = new[]
            {
                Vector3.forward * fallbackSpawnRadius,
                Vector3.back * fallbackSpawnRadius,
                Vector3.right * fallbackSpawnRadius,
                Vector3.left * fallbackSpawnRadius
            };
            var names = new[] { "North", "South", "East", "West" };
            spawnPoints = new Transform[offsets.Length];

            for (var i = 0; i < offsets.Length; i++)
            {
                var point = new GameObject($"Spawn_{names[i]}").transform;
                point.SetParent(transform, false);
                var position = center + offsets[i];
                var terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
                }

                point.position = position;
                point.rotation = Quaternion.LookRotation((center - position).normalized, Vector3.up);
                spawnPoints[i] = point;
            }
        }

        void RemoveDestroyedMonsters()
        {
            aliveMonsters.RemoveAll(monster => monster == null);
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0)
            {
                return;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }
    }
}
