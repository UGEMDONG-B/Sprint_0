using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RaftSharkDive
{
    public sealed class SaveSystem : MonoBehaviour
    {
        [Serializable]
        public sealed class SaveData
        {
            public Vector3 playerPosition;
            public Quaternion playerRotation;
            public float health;
            public float oxygen;
            public float lootTimeRemaining;
            public int[] inventoryCounts;
            public int[] inventorySlots;
            public bool raftStorageBuilt;
            public int[] raftStorageSlots;
            public int raftDepositedWood;
            public int raftDepositedRope;
            public string[] collectedPickupPaths;
            public int stage;
            public bool raftBuilt;
            public bool islandGone;
            public bool raftExpanded;
            public int raftExpansionLevel;
            public float raftDurability;
            public bool hasRaftTransform;
            public Vector3 raftPosition;
            public Quaternion raftRotation;
        }

        public static SaveSystem Instance { get; private set; }
        public string SavePath => Path.Combine(Application.persistentDataPath, "raft_shark_dive_checkpoint.json");
        public bool HasCheckpoint => File.Exists(SavePath);
        public bool LastSaveWasIslandCheckpoint { get; private set; }
        public bool DiskWritesEnabled { get; set; } = true;
        public SaveData LastSnapshot { get; private set; }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.f5Key.wasPressedThisFrame) SaveNow(false);
            if (Keyboard.current.f9Key.wasPressedThisFrame) LoadNow();
        }

        public SaveData CaptureSnapshot(bool islandCheckpoint)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
            PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;
            PlayerOxygen oxygen = player != null ? player.GetComponent<PlayerOxygen>() : null;
            RaftUpgradeStation raft = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            RaftStorageBox storage = RaftStorageBox.FindExisting();
            RaftBuildPoint buildPoint = FindFirstObjectByType<RaftBuildPoint>(FindObjectsInactive.Include);
            SaveData data = new SaveData
            {
                playerPosition = player != null ? player.transform.position : Vector3.zero,
                playerRotation = player != null ? player.transform.rotation : Quaternion.identity,
                health = health != null ? health.Current : 100f,
                oxygen = oxygen != null ? oxygen.Current : 12f,
                lootTimeRemaining = GameManager.Instance != null ? GameManager.Instance.LootTimeRemaining : 60f,
                inventoryCounts = inventory != null ? inventory.GetSerializableCounts() : new int[Enum.GetValues(typeof(ItemId)).Length],
                inventorySlots = inventory != null ? inventory.GetSerializableSlots() : Array.Empty<int>(),
                raftStorageBuilt = storage != null,
                raftStorageSlots = storage != null ? storage.GetSerializableSlots() : Array.Empty<int>(),
                raftDepositedWood = buildPoint != null ? buildPoint.DepositedWood : 0,
                raftDepositedRope = buildPoint != null ? buildPoint.DepositedRope : 0,
                collectedPickupPaths = GetCollectedPickupPaths(),
                stage = GameManager.Instance != null ? (int)GameManager.Instance.Stage : 0,
                raftBuilt = GameManager.Instance != null && GameManager.Instance.RaftBuilt,
                islandGone = islandCheckpoint || (GameManager.Instance != null && GameManager.Instance.IslandDescentComplete),
                raftExpanded = raft != null && raft.IsExpanded,
                raftExpansionLevel = raft != null ? raft.ExpansionLevel : 0,
                raftDurability = raft != null ? raft.Durability : 100f,
                hasRaftTransform = raft != null,
                raftPosition = raft != null ? raft.transform.position : Vector3.zero,
                raftRotation = raft != null ? raft.transform.rotation : Quaternion.identity
            };
            if (islandCheckpoint && data.raftBuilt && GameManager.Instance?.RaftSpawn != null)
            {
                data.playerPosition = GameManager.Instance.RaftSpawn.position;
                data.playerRotation = GameManager.Instance.RaftSpawn.rotation;
            }
            return data;
        }

        public void SaveNow(bool islandCheckpoint)
        {
            SaveData data = CaptureSnapshot(islandCheckpoint);
            LastSnapshot = data;
            if (DiskWritesEnabled) File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            LastSaveWasIslandCheckpoint = islandCheckpoint;
            GameUI.Instance?.ShowNotice(islandCheckpoint ? "Island-lost checkpoint saved. F9 to load." : "Game saved. F9 to load.", 3f);
        }

        public void LoadNow()
        {
            if (!HasCheckpoint)
            {
                GameUI.Instance?.ShowNotice("No saved checkpoint.", 2f);
                return;
            }
            RestoreSnapshot(JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)));
            GameUI.Instance?.ShowNotice("Checkpoint loaded.", 2.5f);
        }

        public void RestoreSnapshot(SaveData data)
        {
            if (data == null) return;
            RaftUpgradeStation raft = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            if (raft != null && data.hasRaftTransform)
                raft.transform.SetPositionAndRotation(data.raftPosition, data.raftRotation);
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                Inventory inventory = player.GetComponent<Inventory>();
                if (data.inventorySlots != null && data.inventorySlots.Length > 0)
                    inventory?.RestoreSerializableSlots(data.inventorySlots);
                else
                    inventory?.RestoreSerializableCounts(data.inventoryCounts);
                player.GetComponent<PlayerHealth>()?.SetCurrent(data.health);
                player.GetComponent<PlayerOxygen>()?.SetCurrent(data.oxygen);
                player.Teleport(data.playerPosition, data.playerRotation);
            }
            GameManager.Instance?.RestoreProgress((GameStage)Mathf.Clamp(data.stage, 0, (int)GameStage.Ending),
                data.raftBuilt, data.islandGone, data.lootTimeRemaining);
            RaftBuildPoint buildPoint = FindFirstObjectByType<RaftBuildPoint>(FindObjectsInactive.Include);
            buildPoint?.ApplyDeposits(data.raftDepositedWood, data.raftDepositedRope);
            if (raft != null)
                raft.ApplyState(data.raftExpansionLevel > 0 ? data.raftExpansionLevel : data.raftExpanded ? 1 : 0,
                    data.raftDurability);
            RaftStorageBox storage = RaftStorageBox.FindExisting();
            if (data.raftStorageBuilt && raft != null)
            {
                if (storage == null) storage = RaftStorageBox.CreateOnRaft(raft.transform);
                storage?.RestoreSlots(data.raftStorageSlots);
            }
            else if (storage != null)
            {
                Destroy(storage.gameObject);
            }
            RestorePickupStates(data.collectedPickupPaths);
        }

        private static string[] GetCollectedPickupPaths()
        {
            System.Collections.Generic.List<string> paths = new System.Collections.Generic.List<string>();
            foreach (PickupItem pickup in FindObjectsByType<PickupItem>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (pickup.gameObject.scene.IsValid() &&
                    pickup.GetComponentInParent<EndlessOceanItemSpawner>() == null && !pickup.gameObject.activeSelf)
                    paths.Add(GetHierarchyPath(pickup.transform));
            return paths.ToArray();
        }

        private static void RestorePickupStates(string[] collectedPaths)
        {
            System.Collections.Generic.HashSet<string> collected = new System.Collections.Generic.HashSet<string>(
                collectedPaths ?? Array.Empty<string>());
            foreach (PickupItem pickup in FindObjectsByType<PickupItem>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (!pickup.gameObject.scene.IsValid() ||
                    pickup.GetComponentInParent<EndlessOceanItemSpawner>() != null) continue;
                pickup.gameObject.SetActive(!collected.Contains(GetHierarchyPath(pickup.transform)));
            }
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.name + "#" + target.GetSiblingIndex();
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "#" + target.GetSiblingIndex() + "/" + path;
            }
            return path;
        }
    }
}
