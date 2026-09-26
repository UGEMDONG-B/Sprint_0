using System.Collections.Generic;
using UnityEngine;

namespace RaftSharkDive
{
    [DisallowMultipleComponent]
    public sealed class EndlessOceanItemSpawner : MonoBehaviour
    {
        private sealed class SpawnedPickup
        {
            public GameObject instance;
            public ItemId item;
        }

        private static readonly ItemId[] spawnSequence =
        {
            ItemId.Wood, ItemId.Scrap, ItemId.Rubber, ItemId.Rope,
            ItemId.Scrap, ItemId.Wood, ItemId.Filter, ItemId.Rubber
        };

        [SerializeField, Min(5f)] private float nearestSpawnDistance = 22f;
        [SerializeField, Min(10f)] private float farthestSpawnDistance = 70f;
        [SerializeField, Min(2f)] private float rowSpacing = 12f;
        [SerializeField, Min(2f)] private float lateralRange = 18f;
        [SerializeField, Range(1, 5)] private int itemsPerRow = 3;
        [SerializeField, Range(1, 8)] private int initialRows = 4;
        [SerializeField, Min(5f)] private float recycleBehindDistance = 35f;

        private readonly Dictionary<ItemId, GameObject> templates = new Dictionary<ItemId, GameObject>();
        private readonly Dictionary<ItemId, Stack<GameObject>> pools = new Dictionary<ItemId, Stack<GameObject>>();
        private readonly List<SpawnedPickup> activePickups = new List<SpawnedPickup>();
        private Transform raft;
        private Vector3 lastRaftPosition;
        private float distanceSinceRow;
        private int sequenceIndex;
        private int serial;
        private bool initialized;

        public bool IsRunning => initialized && GameManager.Instance != null && GameManager.Instance.IslandDescentComplete;
        public int ActiveSpawnCount => activePickups.Count;
        public int TotalSpawnedCount { get; private set; }

        public bool TryGetNearestPickup(Vector3 origin, out ItemId item, out Vector3 position)
        {
            item = default;
            position = default;
            float nearestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < activePickups.Count; i++)
            {
                SpawnedPickup spawned = activePickups[i];
                if (spawned.instance == null || !spawned.instance.activeSelf) continue;
                float sqrDistance = (spawned.instance.transform.position - origin).sqrMagnitude;
                if (sqrDistance >= nearestDistance) continue;
                nearestDistance = sqrDistance;
                item = spawned.item;
                position = spawned.instance.transform.position;
                found = true;
            }
            return found;
        }

        public void Configure(Transform raftTransform)
        {
            raft = raftTransform;
        }

        private void Start()
        {
            FindTemplates();
            if (raft == null)
            {
                RaftUpgradeStation station = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
                if (station != null) raft = station.transform;
            }
        }

        private void Update()
        {
            if (raft == null || GameManager.Instance == null || !GameManager.Instance.IslandDescentComplete) return;
            if (!initialized) BeginSpawning();

            Vector3 displacement = raft.position - lastRaftPosition;
            float forwardTravel = Vector3.Dot(displacement, raft.forward);
            if (forwardTravel > 0f && forwardTravel < rowSpacing * 3f)
                distanceSinceRow += forwardTravel;
            lastRaftPosition = raft.position;

            while (distanceSinceRow >= rowSpacing)
            {
                distanceSinceRow -= rowSpacing;
                SpawnRow(farthestSpawnDistance);
            }
            RecyclePassedOrCollectedItems();
        }

        private void FindTemplates()
        {
            templates.Clear();
            foreach (PickupItem pickup in FindObjectsByType<PickupItem>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (pickup.Item == ItemId.OxygenTank || templates.ContainsKey(pickup.Item)) continue;
                templates.Add(pickup.Item, pickup.gameObject);
                if (!pools.ContainsKey(pickup.Item)) pools.Add(pickup.Item, new Stack<GameObject>());
            }
        }

        private void BeginSpawning()
        {
            if (templates.Count == 0) FindTemplates();
            lastRaftPosition = raft.position;
            float interval = initialRows <= 1
                ? 0f
                : (farthestSpawnDistance - nearestSpawnDistance) / (initialRows - 1f);
            for (int i = 0; i < initialRows; i++) SpawnRow(nearestSpawnDistance + interval * i);
            initialized = true;
        }

        private void SpawnRow(float forwardDistance)
        {
            for (int i = 0; i < itemsPerRow; i++)
            {
                ItemId item = NextAvailableItem();
                if (!templates.ContainsKey(item)) continue;
                float lane = itemsPerRow <= 1
                    ? Random.Range(-lateralRange, lateralRange)
                    : Mathf.Lerp(-lateralRange, lateralRange, i / (itemsPerRow - 1f)) + Random.Range(-2f, 2f);
                float depth = item == ItemId.Wood || item == ItemId.Rope
                    ? Random.Range(0.05f, 0.4f)
                    : Random.Range(0.35f, 1.8f);
                Vector3 position = raft.position + raft.forward * (forwardDistance + Random.Range(-2f, 2f)) +
                                   raft.right * lane;
                position.y = GameManager.Instance.WaterSurfaceY - depth;
                Spawn(item, position);
            }
        }

        private ItemId NextAvailableItem()
        {
            for (int attempts = 0; attempts < spawnSequence.Length; attempts++)
            {
                ItemId item = spawnSequence[sequenceIndex++ % spawnSequence.Length];
                if (templates.ContainsKey(item)) return item;
            }
            foreach (KeyValuePair<ItemId, GameObject> pair in templates) return pair.Key;
            return ItemId.Wood;
        }

        private void Spawn(ItemId item, Vector3 position)
        {
            GameObject instance = pools[item].Count > 0
                ? pools[item].Pop()
                : Instantiate(templates[item], transform);
            instance.name = $"Endless_{item}_{++serial:0000}";
            instance.transform.SetParent(transform, true);
            instance.transform.SetPositionAndRotation(position, item == ItemId.Wood
                ? Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), 90f)
                : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            instance.GetComponent<PickupItem>()?.Configure(item, 1);
            instance.SetActive(true);
            activePickups.Add(new SpawnedPickup { instance = instance, item = item });
            TotalSpawnedCount++;
        }

        private void RecyclePassedOrCollectedItems()
        {
            for (int i = activePickups.Count - 1; i >= 0; i--)
            {
                SpawnedPickup spawned = activePickups[i];
                if (spawned.instance == null)
                {
                    activePickups.RemoveAt(i);
                    continue;
                }
                float forwardDistance = Vector3.Dot(spawned.instance.transform.position - raft.position, raft.forward);
                if (spawned.instance.activeSelf && forwardDistance >= -recycleBehindDistance) continue;
                spawned.instance.SetActive(false);
                pools[spawned.item].Push(spawned.instance);
                activePickups.RemoveAt(i);
            }
        }
    }
}
