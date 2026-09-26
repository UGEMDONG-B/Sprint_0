using System;
using System.Collections.Generic;
using RaftSharkDive;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaftGame.Hyunwoo.Multiplayer
{
    /// <summary>
    /// Host-authoritative Raft extensions: upgrade planks, shared storage, and dropped items.
    /// </summary>
    public sealed class RaftExtendedWorldStateBridge : MonoBehaviour
    {
        const string StateRequestMessage = "Raft.ExtendedState.Request.v1";
        const string UpgradeRequestMessage = "Raft.Upgrade.Request.v1";
        const string UpgradeReceiptMessage = "Raft.Upgrade.Receipt.v1";
        const string UpgradeStateMessage = "Raft.Upgrade.State.v1";
        const string StorageInstallRequestMessage = "Raft.Storage.InstallRequest.v1";
        const string StorageInstallReceiptMessage = "Raft.Storage.InstallReceipt.v1";
        const string StorageTransferRequestMessage = "Raft.Storage.TransferRequest.v1";
        const string StorageTransferReceiptMessage = "Raft.Storage.TransferReceipt.v1";
        const string StorageStateMessage = "Raft.Storage.State.v1";
        const string DropSpawnRequestMessage = "Raft.Drop.SpawnRequest.v1";
        const string DropSpawnStateMessage = "Raft.Drop.SpawnState.v1";
        const string DropTransformMessage = "Raft.Drop.Transform.v1";
        const string DropCollectRequestMessage = "Raft.Drop.CollectRequest.v1";
        const string DropCollectReceiptMessage = "Raft.Drop.CollectReceipt.v1";
        const string DropDespawnMessage = "Raft.Drop.Despawn.v1";
        const string FortressDiscoverRequestMessage = "Raft.Fortress.DiscoverRequest.v1";
        const string FortressStateMessage = "Raft.Fortress.State.v1";
        const string FortressDoorRequestMessage = "Raft.Fortress.DoorRequest.v1";
        const string FortressDoorStateMessage = "Raft.Fortress.DoorState.v1";
        const string EndingRequestMessage = "Raft.Ending.Request.v1";
        const string EndingStateMessage = "Raft.Ending.State.v1";
        const string RestartRequestMessage = "Raft.Restart.Request.v1";

        readonly Dictionary<string, RaftDroppedItemReplica> droppedItems =
            new Dictionary<string, RaftDroppedItemReplica>();
        readonly HashSet<string> pendingDropCollections = new HashSet<string>();

        NetworkManager networkManager;
        bool handlersRegistered;
        bool initialStateReceived;
        bool upgradePending;
        bool storageInstallPending;
        bool storageTransferPending;
        bool doorRequestPending;
        bool endingRequestPending;
        bool restartPending;
        float nextStateRequestAt;
        float nextDropSnapshotAt;
        float nextStorageSnapshotAt;
        int localDropSerial;

        void Awake()
        {
            networkManager = NetworkManager.Singleton;
            RegisterHandlers();
            RaftMultiplayerHooks.TryUpgradeRaft = TryUpgradeRaft;
            RaftMultiplayerHooks.TryReleaseHeldItem = TryReleaseHeldItem;
            RaftMultiplayerHooks.TryInstallStorage = TryInstallStorage;
            RaftMultiplayerHooks.TryStoreItem = TryStoreItem;
            RaftMultiplayerHooks.TryTakeItem = TryTakeItem;
            RaftMultiplayerHooks.TryCollectDroppedItem = TryCollectDroppedItem;
            RaftMultiplayerHooks.TryDiscoverFortress = TryDiscoverFortress;
            RaftMultiplayerHooks.TryOpenFortressDoor = TryOpenFortressDoor;
            RaftMultiplayerHooks.TryDrainOcean = TryDrainOcean;
            RaftMultiplayerHooks.TryRestartGame = TryRestartGame;
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        void Update()
        {
            if (networkManager == null || !networkManager.IsListening) return;

            if (!networkManager.IsServer && !initialStateReceived && Time.unscaledTime >= nextStateRequestAt)
            {
                nextStateRequestAt = Time.unscaledTime + 1f;
                SendEmptyMessage(StateRequestMessage, NetworkManager.ServerClientId);
            }

            if (!networkManager.IsServer) return;

            if (Time.unscaledTime >= nextDropSnapshotAt)
            {
                nextDropSnapshotAt = Time.unscaledTime + 0.1f;
                BroadcastDropSnapshots();
            }

            if (Time.unscaledTime >= nextStorageSnapshotAt)
            {
                nextStorageSnapshotAt = Time.unscaledTime + 0.5f;
                BroadcastUpgradeState();
                BroadcastStorageState();
                BroadcastFortressState();
                BroadcastFortressDoorState();
                BroadcastEndingState();
            }
        }

        bool TryUpgradeRaft(RaftUpgradeStation raft, PlayerController player)
        {
            if (!IsNetworkReady()) return false;
            if (upgradePending) return true;

            var inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (raft == null || inventory == null) return true;

            byte action;
            int cost;
            if (raft.ExpansionLevel < raft.MaxExpansionLevel)
            {
                action = 0;
                cost = raft.WoodPerPlank;
            }
            else if (raft.Durability < raft.MaxDurability - 0.1f)
            {
                action = 1;
                cost = raft.RepairWoodCost;
            }
            else
            {
                GameUI.Instance?.ShowNotice("Raft is already fully upgraded and repaired.", 2f);
                return true;
            }

            if (!inventory.Has(ItemId.Wood, cost))
            {
                GameUI.Instance?.ShowNotice($"Need Wood x{cost}.", 2f);
                return true;
            }

            if (networkManager.IsServer)
            {
                ProcessUpgradeRequest(networkManager.LocalClientId, action, player);
            }
            else
            {
                upgradePending = true;
                using (var writer = new FastBufferWriter(1, Allocator.Temp))
                {
                    writer.WriteValueSafe(action);
                    networkManager.CustomMessagingManager.SendNamedMessage(
                        UpgradeRequestMessage, NetworkManager.ServerClientId, writer);
                }
            }
            return true;
        }

        bool TryInstallStorage(HeldItemController heldItems, Transform raft, Vector3 worldPosition)
        {
            if (!IsNetworkReady()) return false;
            if (storageInstallPending) return true;

            var inventory = heldItems != null ? heldItems.GetComponent<Inventory>() : null;
            if (heldItems == null || raft == null || inventory?.SelectedItem != ItemId.RaftStorageBox) return true;
            if (RaftStorageBox.FindExisting() != null)
            {
                GameUI.Instance?.ShowNotice("Raft Storage already exists.", 1.5f);
                return true;
            }

            Vector3 localPosition = raft.InverseTransformPoint(worldPosition);
            if (networkManager.IsServer)
            {
                ProcessStorageInstall(networkManager.LocalClientId, localPosition, inventory);
            }
            else
            {
                storageInstallPending = true;
                using (var writer = new FastBufferWriter(16, Allocator.Temp))
                {
                    writer.WriteValueSafe(localPosition);
                    networkManager.CustomMessagingManager.SendNamedMessage(
                        StorageInstallRequestMessage, NetworkManager.ServerClientId, writer);
                }
            }
            return true;
        }

        bool TryStoreItem(RaftStorageBox storage, Inventory inventory, int inventorySlot)
        {
            if (!IsNetworkReady()) return false;
            if (storageTransferPending) return true;
            ItemId? item = inventory?.GetSlotItem(inventorySlot);
            if (storage == null || !item.HasValue || storage.Count >= storage.Capacity) return true;

            if (networkManager.IsServer)
            {
                bool stored = storage.TryStoreFromLocal(inventory, inventorySlot);
                if (stored) BroadcastStorageState();
            }
            else
            {
                storageTransferPending = true;
                SendStorageTransferRequest(0, inventorySlot, (int)item.Value);
            }
            return true;
        }

        bool TryTakeItem(RaftStorageBox storage, Inventory inventory, int storageSlot)
        {
            if (!IsNetworkReady()) return false;
            if (storageTransferPending) return true;
            ItemId? item = storage?.GetSlotItem(storageSlot);
            if (!item.HasValue || inventory == null || inventory.FreeSlotCount <= 0) return true;

            if (networkManager.IsServer)
            {
                bool taken = storage.TryTakeToLocal(inventory, storageSlot);
                if (taken) BroadcastStorageState();
            }
            else
            {
                storageTransferPending = true;
                SendStorageTransferRequest(1, storageSlot, (int)item.Value);
            }
            return true;
        }

        bool TryReleaseHeldItem(HeldItemController heldItems, float charge, bool gentleDrop)
        {
            if (!IsNetworkReady()) return false;
            if (heldItems == null || !heldItems.ReleaseSelectedLocal(charge, gentleDrop)) return true;

            GameObject dropped = heldItems.LastReleasedObject;
            var pickup = dropped != null ? dropped.GetComponent<PickupItem>() : null;
            var body = dropped != null ? dropped.GetComponent<Rigidbody>() : null;
            if (dropped == null || pickup == null || body == null) return true;

            string id = networkManager.LocalClientId + ":" + (++localDropSerial);
            var marker = dropped.AddComponent<RaftDroppedItemReplica>();
            marker.Configure(id, networkManager.IsServer);
            droppedItems[id] = marker;

            if (networkManager.IsServer)
            {
                BroadcastDropSpawn(marker, pickup.Item, body.linearVelocity);
            }
            else
            {
                SendDropSpawnRequest(marker, pickup.Item, body.linearVelocity);
            }
            return true;
        }

        bool TryCollectDroppedItem(PickupItem pickup, PlayerController player)
        {
            if (!IsNetworkReady()) return false;
            var marker = pickup != null ? pickup.GetComponent<RaftDroppedItemReplica>() : null;
            if (marker == null) return false;

            var inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (inventory == null || inventory.FreeSlotCount < pickup.Amount) return true;
            if (pendingDropCollections.Contains(marker.Id)) return true;

            if (networkManager.IsServer)
            {
                ProcessDropCollect(networkManager.LocalClientId, marker.Id, player);
            }
            else
            {
                pendingDropCollections.Add(marker.Id);
                SendStringMessage(DropCollectRequestMessage, NetworkManager.ServerClientId, marker.Id);
            }
            return true;
        }

        bool TryDiscoverFortress(GameManager gameManager)
        {
            if (!IsNetworkReady()) return false;
            if (gameManager == null) return true;

            if (networkManager.IsServer)
            {
                RevealFortressAndBroadcast(networkManager.LocalClientId);
            }
            else
            {
                SendEmptyMessage(FortressDiscoverRequestMessage, NetworkManager.ServerClientId);
            }
            return true;
        }

        bool TryOpenFortressDoor(FortressDoor door, PlayerController player)
        {
            if (!IsNetworkReady()) return false;
            if (doorRequestPending || door == null) return true;

            if (networkManager.IsServer)
            {
                OpenFortressDoorAndBroadcast(networkManager.LocalClientId);
            }
            else
            {
                doorRequestPending = true;
                SendEmptyMessage(FortressDoorRequestMessage, NetworkManager.ServerClientId);
            }
            return true;
        }

        bool TryDrainOcean(EndingDrain drain, PlayerController player)
        {
            if (!IsNetworkReady()) return false;
            if (endingRequestPending || drain == null) return true;

            if (networkManager.IsServer)
            {
                StartEndingAndBroadcast(networkManager.LocalClientId);
            }
            else
            {
                endingRequestPending = true;
                SendEmptyMessage(EndingRequestMessage, NetworkManager.ServerClientId);
            }
            return true;
        }

        bool TryRestartGame(GameManager gameManager)
        {
            if (!IsNetworkReady()) return false;
            if (restartPending) return true;
            restartPending = true;

            if (networkManager.IsServer)
                StartSynchronizedRestart(networkManager.LocalClientId);
            else
                SendEmptyMessage(RestartRequestMessage, NetworkManager.ServerClientId);
            return true;
        }

        void RegisterHandlers()
        {
            if (handlersRegistered || !IsNetworkReady()) return;
            var messages = networkManager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(StateRequestMessage, OnStateRequest);
            messages.RegisterNamedMessageHandler(UpgradeRequestMessage, OnUpgradeRequest);
            messages.RegisterNamedMessageHandler(UpgradeReceiptMessage, OnUpgradeReceipt);
            messages.RegisterNamedMessageHandler(UpgradeStateMessage, OnUpgradeState);
            messages.RegisterNamedMessageHandler(StorageInstallRequestMessage, OnStorageInstallRequest);
            messages.RegisterNamedMessageHandler(StorageInstallReceiptMessage, OnStorageInstallReceipt);
            messages.RegisterNamedMessageHandler(StorageTransferRequestMessage, OnStorageTransferRequest);
            messages.RegisterNamedMessageHandler(StorageTransferReceiptMessage, OnStorageTransferReceipt);
            messages.RegisterNamedMessageHandler(StorageStateMessage, OnStorageState);
            messages.RegisterNamedMessageHandler(DropSpawnRequestMessage, OnDropSpawnRequest);
            messages.RegisterNamedMessageHandler(DropSpawnStateMessage, OnDropSpawnState);
            messages.RegisterNamedMessageHandler(DropTransformMessage, OnDropTransform);
            messages.RegisterNamedMessageHandler(DropCollectRequestMessage, OnDropCollectRequest);
            messages.RegisterNamedMessageHandler(DropCollectReceiptMessage, OnDropCollectReceipt);
            messages.RegisterNamedMessageHandler(DropDespawnMessage, OnDropDespawn);
            messages.RegisterNamedMessageHandler(FortressDiscoverRequestMessage, OnFortressDiscoverRequest);
            messages.RegisterNamedMessageHandler(FortressStateMessage, OnFortressState);
            messages.RegisterNamedMessageHandler(FortressDoorRequestMessage, OnFortressDoorRequest);
            messages.RegisterNamedMessageHandler(FortressDoorStateMessage, OnFortressDoorState);
            messages.RegisterNamedMessageHandler(EndingRequestMessage, OnEndingRequest);
            messages.RegisterNamedMessageHandler(EndingStateMessage, OnEndingState);
            messages.RegisterNamedMessageHandler(RestartRequestMessage, OnRestartRequest);
            handlersRegistered = true;
            Debug.Log("[RaftMultiplayer] Raft upgrades, storage, and dropped-item bridge ready.");
        }

        void OnStateRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            SendUpgradeState(senderClientId);
            SendStorageState(senderClientId);
            SendFortressState(senderClientId);
            SendFortressDoorState(senderClientId);
            SendEndingState(senderClientId);
            foreach (var pair in droppedItems)
            {
                var marker = pair.Value;
                if (marker == null || !marker.gameObject.activeSelf) continue;
                var pickup = marker.GetComponent<PickupItem>();
                var body = marker.GetComponent<Rigidbody>();
                if (pickup != null && body != null) SendDropSpawn(senderClientId, marker, pickup.Item, body.linearVelocity);
            }
        }

        void OnUpgradeRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            reader.ReadValueSafe(out byte action);
            ProcessUpgradeRequest(senderClientId, action, null);
        }

        void ProcessUpgradeRequest(ulong senderClientId, byte action, PlayerController serverPlayer)
        {
            var raft = FindRaft();
            bool accepted = false;
            int cost = 0;
            if (raft != null && action == 0 && raft.ExpansionLevel < raft.MaxExpansionLevel)
            {
                accepted = true;
                cost = raft.WoodPerPlank;
                raft.ApplyState(raft.ExpansionLevel + 1, raft.Durability);
            }
            else if (raft != null && action == 1 && raft.ExpansionLevel >= raft.MaxExpansionLevel &&
                     raft.Durability < raft.MaxDurability - 0.1f)
            {
                accepted = true;
                cost = raft.RepairWoodCost;
                raft.ApplyState(raft.ExpansionLevel, Mathf.Min(raft.MaxDurability,
                    raft.Durability + raft.RepairAmount));
            }

            if (accepted && senderClientId == networkManager.LocalClientId && serverPlayer != null)
            {
                serverPlayer.GetComponent<Inventory>()?.Consume(ItemId.Wood, cost);
                ShowUpgradeNotice(raft, action);
            }
            else if (senderClientId != networkManager.LocalClientId)
            {
                SendUpgradeReceipt(senderClientId, accepted, action, cost, raft);
            }

            BroadcastUpgradeState();
        }

        void OnUpgradeReceipt(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out bool accepted);
            reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out int cost);
            reader.ReadValueSafe(out int level);
            reader.ReadValueSafe(out float durability);
            upgradePending = false;
            var raft = FindRaft();
            raft?.ApplyState(level, durability);
            if (!accepted) return;
            FindFirstObjectByType<PlayerController>()?.GetComponent<Inventory>()?.Consume(ItemId.Wood, cost);
            ShowUpgradeNotice(raft, action);
        }

        void OnUpgradeState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out int level);
            reader.ReadValueSafe(out float durability);
            FindRaft()?.ApplyState(level, durability);
            initialStateReceived = true;
        }

        void OnStorageInstallRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            reader.ReadValueSafe(out Vector3 localPosition);
            ProcessStorageInstall(senderClientId, localPosition, null);
        }

        void ProcessStorageInstall(ulong senderClientId, Vector3 localPosition, Inventory serverInventory)
        {
            var raft = FindRaft();
            bool accepted = raft != null && RaftStorageBox.FindExisting() == null;
            if (accepted)
            {
                accepted = RaftStorageBox.CreateOnRaft(raft.transform,
                    raft.transform.TransformPoint(localPosition)) != null;
            }

            if (accepted && senderClientId == networkManager.LocalClientId && serverInventory != null)
            {
                ConsumeSelectedStorage(serverInventory);
                GameUI.Instance?.ShowNotice("Raft Storage installed for every player.", 2.5f);
            }
            else if (senderClientId != networkManager.LocalClientId)
            {
                SendStorageInstallReceipt(senderClientId, accepted);
            }
            BroadcastStorageState();
        }

        void OnStorageInstallReceipt(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out bool accepted);
            storageInstallPending = false;
            if (!accepted) return;
            var inventory = FindFirstObjectByType<PlayerController>()?.GetComponent<Inventory>();
            ConsumeSelectedStorage(inventory);
            GameUI.Instance?.ShowNotice("Raft Storage installed for every player.", 2.5f);
        }

        void OnStorageTransferRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            reader.ReadValueSafe(out byte operation);
            reader.ReadValueSafe(out int slotIndex);
            reader.ReadValueSafe(out int itemValue);
            ProcessRemoteStorageTransfer(senderClientId, operation, slotIndex, itemValue);
        }

        void ProcessRemoteStorageTransfer(ulong senderClientId, byte operation, int slotIndex, int itemValue)
        {
            var storage = RaftStorageBox.FindExisting();
            bool accepted = false;
            ItemId item = IsValidItem(itemValue) ? (ItemId)itemValue : default;
            if (storage != null && IsValidItem(itemValue))
            {
                var slots = new List<int>(storage.GetSerializableSlots());
                if (operation == 0 && slots.Count < storage.Capacity)
                {
                    slots.Add(itemValue);
                    accepted = true;
                }
                else if (operation == 1 && slotIndex >= 0 && slotIndex < slots.Count &&
                         slots[slotIndex] == itemValue)
                {
                    slots.RemoveAt(slotIndex);
                    accepted = true;
                }
                if (accepted) storage.RestoreSlots(slots.ToArray());
            }

            SendStorageTransferReceipt(senderClientId, operation, slotIndex, (int)item, accepted);
            BroadcastStorageState();
        }

        void OnStorageTransferReceipt(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out byte operation);
            reader.ReadValueSafe(out int slotIndex);
            reader.ReadValueSafe(out int itemValue);
            reader.ReadValueSafe(out bool accepted);
            storageTransferPending = false;
            if (!accepted || !IsValidItem(itemValue)) return;

            var inventory = FindFirstObjectByType<PlayerController>()?.GetComponent<Inventory>();
            if (inventory == null) return;
            ItemId item = (ItemId)itemValue;
            if (operation == 0)
            {
                if (inventory.GetSlotItem(slotIndex) == item)
                    inventory.TryRemoveSlot(slotIndex, out _);
                else
                    inventory.Consume(item);
            }
            else
            {
                inventory.TryAdd(item);
            }
        }

        void OnStorageState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out bool exists);
            reader.ReadValueSafe(out Vector3 localPosition);
            reader.ReadValueSafe(out int slotCount);
            int[] slots = new int[Mathf.Clamp(slotCount, 0, 30)];
            for (int i = 0; i < slots.Length; i++) reader.ReadValueSafe(out slots[i]);
            ApplyStorageState(exists, localPosition, slots);
        }

        void OnDropSpawnRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            ReadDropSpawn(reader, out string id, out ItemId item, out Vector3 position,
                out Quaternion rotation, out Vector3 velocity);
            if (!id.StartsWith(senderClientId + ":", StringComparison.Ordinal) || droppedItems.ContainsKey(id)) return;
            var marker = CreateDrop(id, item, position, rotation, velocity, true);
            if (marker != null) BroadcastDropSpawn(marker, item, velocity);
        }

        void OnDropSpawnState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            ReadDropSpawn(reader, out string id, out ItemId item, out Vector3 position,
                out Quaternion rotation, out Vector3 velocity);
            if (!droppedItems.TryGetValue(id, out var marker) || marker == null)
                marker = CreateDrop(id, item, position, rotation, velocity, networkManager.IsServer);
            if (marker == null) return;
            marker.transform.SetPositionAndRotation(position, rotation);
            marker.Configure(id, networkManager.IsServer);
            var body = marker.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = !networkManager.IsServer;
                if (networkManager.IsServer) body.linearVelocity = velocity;
            }
            var motion = marker.GetComponent<ThrownPickupMotion>();
            if (motion != null) motion.enabled = networkManager.IsServer;
        }

        void OnDropTransform(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId || networkManager.IsServer) return;
            reader.ReadValueSafe(out string id);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Quaternion rotation);
            if (!droppedItems.TryGetValue(id, out var marker) || marker == null) return;
            marker.transform.SetPositionAndRotation(position, rotation);
        }

        void OnDropCollectRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            reader.ReadValueSafe(out string id);
            ProcessDropCollect(senderClientId, id, null);
        }

        void ProcessDropCollect(ulong senderClientId, string id, PlayerController serverPlayer)
        {
            bool accepted = droppedItems.TryGetValue(id, out var marker) && marker != null && marker.gameObject.activeSelf;
            var pickup = accepted ? marker.GetComponent<PickupItem>() : null;
            ItemId item = pickup != null ? pickup.Item : default;
            if (accepted && senderClientId == networkManager.LocalClientId && serverPlayer != null)
                accepted = pickup.TryCollectLocal(serverPlayer);
            else if (accepted)
                marker.gameObject.SetActive(false);

            if (senderClientId != networkManager.LocalClientId)
                SendDropCollectReceipt(senderClientId, id, item, accepted);
            if (accepted) BroadcastDropDespawn(id);
        }

        void OnDropCollectReceipt(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string id);
            reader.ReadValueSafe(out int itemValue);
            reader.ReadValueSafe(out bool accepted);
            pendingDropCollections.Remove(id);
            if (!accepted || !IsValidItem(itemValue)) return;
            FindFirstObjectByType<PlayerController>()?.GetComponent<Inventory>()?.TryAdd((ItemId)itemValue);
            GameUI.Instance?.ShowNotice($"Picked up {(ItemId)itemValue} x1", 1.5f);
        }

        void OnDropDespawn(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out string id);
            if (droppedItems.TryGetValue(id, out var marker) && marker != null) marker.gameObject.SetActive(false);
        }

        void OnFortressDiscoverRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            RevealFortressAndBroadcast(senderClientId);
        }

        void OnFortressState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out bool visible);
            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Quaternion rotation);
            GameManager.Instance?.ApplyFortressNetworkState(visible, position, rotation);
        }

        void RevealFortressAndBroadcast(ulong requesterClientId)
        {
            GameManager.Instance?.OnOxygenTankCraftedLocal();
            BroadcastFortressState();
            Debug.Log($"[RaftMultiplayer] Host synchronized fortress reveal requested by client {requesterClientId}.");
        }

        void OnFortressDoorRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            OpenFortressDoorAndBroadcast(senderClientId);
        }

        void OnFortressDoorState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out bool open);
            doorRequestPending = false;
            FindFortressDoor()?.ApplyOpenState(open);
        }

        void OpenFortressDoorAndBroadcast(ulong requesterClientId)
        {
            FortressDoor door = FindFortressDoor();
            if (door == null) return;
            door.OpenLocal();
            BroadcastFortressDoorState();
            Debug.Log($"[RaftMultiplayer] Host synchronized fortress door requested by client {requesterClientId}.");
        }

        void OnEndingRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            StartEndingAndBroadcast(senderClientId);
        }

        void OnEndingState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out bool started);
            endingRequestPending = false;
            if (!started) return;
            FindEndingDrain()?.ApplyUsedState(true);
            GameManager.Instance?.CompleteEndingLocal();
        }

        void StartEndingAndBroadcast(ulong requesterClientId)
        {
            FortressDoor door = FindFortressDoor();
            EndingDrain drain = FindEndingDrain();
            if (door == null || drain == null || !door.IsOpen) return;
            drain.CompleteLocal();
            BroadcastEndingState();
            Debug.Log($"[RaftMultiplayer] Host synchronized ocean drain requested by client {requesterClientId}.");
        }

        void OnRestartRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsValidServerRequest(senderClientId)) return;
            if (GameManager.Instance?.EndingStarted != true) return;
            StartSynchronizedRestart(senderClientId);
        }

        void StartSynchronizedRestart(ulong requesterClientId)
        {
            if (!networkManager.IsServer || networkManager.SceneManager == null)
            {
                restartPending = false;
                return;
            }

            restartPending = true;
            SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(
                RaftMultiplayerBootstrap.MainScenePath, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                restartPending = false;
                Debug.LogError($"[RaftMultiplayer] Failed to synchronize restart: {status}");
                return;
            }
            Debug.Log($"[RaftMultiplayer] Host synchronized restart requested by client {requesterClientId}.");
        }

        void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != RaftMultiplayerBootstrap.MainScenePath) return;
            restartPending = false;
            upgradePending = false;
            storageInstallPending = false;
            storageTransferPending = false;
            doorRequestPending = false;
            endingRequestPending = false;
            pendingDropCollections.Clear();
            droppedItems.Clear();
            initialStateReceived = networkManager != null && networkManager.IsServer;
            nextStateRequestAt = 0f;
            nextDropSnapshotAt = 0f;
            nextStorageSnapshotAt = 0f;
        }

        RaftDroppedItemReplica CreateDrop(string id, ItemId item, Vector3 position, Quaternion rotation,
            Vector3 velocity, bool authoritative)
        {
            var factory = FindFirstObjectByType<HeldItemController>();
            GameObject instance = factory?.CreateDroppedReplica(item, position, rotation, velocity, authoritative);
            if (instance == null) return null;
            var marker = instance.AddComponent<RaftDroppedItemReplica>();
            marker.Configure(id, authoritative);
            var motion = marker.GetComponent<ThrownPickupMotion>();
            if (motion != null) motion.enabled = authoritative;
            droppedItems[id] = marker;
            return marker;
        }

        void BroadcastDropSnapshots()
        {
            foreach (var pair in droppedItems)
            {
                var marker = pair.Value;
                if (marker == null || !marker.IsAuthoritative || !marker.gameObject.activeSelf) continue;
                using (var writer = new FastBufferWriter(4096, Allocator.Temp))
                {
                    writer.WriteValueSafe(marker.Id);
                    writer.WriteValueSafe(marker.transform.position);
                    writer.WriteValueSafe(marker.transform.rotation);
                    networkManager.CustomMessagingManager.SendNamedMessageToAll(DropTransformMessage, writer,
                        NetworkDelivery.UnreliableSequenced);
                }
            }
        }

        void BroadcastUpgradeState()
        {
            var raft = FindRaft();
            if (raft == null) return;
            using (var writer = new FastBufferWriter(16, Allocator.Temp))
            {
                writer.WriteValueSafe(raft.ExpansionLevel);
                writer.WriteValueSafe(raft.Durability);
                networkManager.CustomMessagingManager.SendNamedMessageToAll(UpgradeStateMessage, writer);
            }
        }

        void SendUpgradeState(ulong clientId)
        {
            var raft = FindRaft();
            if (raft == null) return;
            using (var writer = new FastBufferWriter(16, Allocator.Temp))
            {
                writer.WriteValueSafe(raft.ExpansionLevel);
                writer.WriteValueSafe(raft.Durability);
                networkManager.CustomMessagingManager.SendNamedMessage(UpgradeStateMessage, clientId, writer);
            }
        }

        void SendUpgradeReceipt(ulong clientId, bool accepted, byte action, int cost, RaftUpgradeStation raft)
        {
            using (var writer = new FastBufferWriter(24, Allocator.Temp))
            {
                writer.WriteValueSafe(accepted);
                writer.WriteValueSafe(action);
                writer.WriteValueSafe(cost);
                writer.WriteValueSafe(raft != null ? raft.ExpansionLevel : 0);
                writer.WriteValueSafe(raft != null ? raft.Durability : 0f);
                networkManager.CustomMessagingManager.SendNamedMessage(UpgradeReceiptMessage, clientId, writer);
            }
        }

        void BroadcastStorageState()
        {
            SendStorageStateToAll();
        }

        void SendStorageState(ulong clientId)
        {
            WriteStorageState((writer) => networkManager.CustomMessagingManager.SendNamedMessage(
                StorageStateMessage, clientId, writer));
        }

        void SendStorageStateToAll()
        {
            WriteStorageState((writer) => networkManager.CustomMessagingManager.SendNamedMessageToAll(
                StorageStateMessage, writer));
        }

        delegate void StorageWriterSender(FastBufferWriter writer);

        void WriteStorageState(StorageWriterSender sender)
        {
            var storage = RaftStorageBox.FindExisting();
            bool exists = storage != null;
            int[] slots = exists ? storage.GetSerializableSlots() : Array.Empty<int>();
            Vector3 localPosition = exists ? storage.transform.localPosition : Vector3.zero;
            using (var writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteValueSafe(exists);
                writer.WriteValueSafe(localPosition);
                writer.WriteValueSafe(slots.Length);
                for (int i = 0; i < slots.Length; i++) writer.WriteValueSafe(slots[i]);
                sender(writer);
            }
        }

        void ApplyStorageState(bool exists, Vector3 localPosition, int[] slots)
        {
            var storage = RaftStorageBox.FindExisting();
            if (!exists)
            {
                if (storage != null) Destroy(storage.gameObject);
                return;
            }

            var raft = FindRaft();
            if (raft == null) return;
            if (storage == null)
                storage = RaftStorageBox.CreateOnRaft(raft.transform, raft.transform.TransformPoint(localPosition));
            if (storage == null) return;
            storage.transform.localPosition = localPosition;
            storage.RestoreSlots(slots);
        }

        void SendStorageTransferRequest(byte operation, int slotIndex, int itemValue)
        {
            using (var writer = new FastBufferWriter(16, Allocator.Temp))
            {
                writer.WriteValueSafe(operation);
                writer.WriteValueSafe(slotIndex);
                writer.WriteValueSafe(itemValue);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    StorageTransferRequestMessage, NetworkManager.ServerClientId, writer);
            }
        }

        void SendStorageTransferReceipt(ulong clientId, byte operation, int slotIndex, int itemValue, bool accepted)
        {
            using (var writer = new FastBufferWriter(20, Allocator.Temp))
            {
                writer.WriteValueSafe(operation);
                writer.WriteValueSafe(slotIndex);
                writer.WriteValueSafe(itemValue);
                writer.WriteValueSafe(accepted);
                networkManager.CustomMessagingManager.SendNamedMessage(StorageTransferReceiptMessage, clientId, writer);
            }
        }

        void SendStorageInstallReceipt(ulong clientId, bool accepted)
        {
            using (var writer = new FastBufferWriter(1, Allocator.Temp))
            {
                writer.WriteValueSafe(accepted);
                networkManager.CustomMessagingManager.SendNamedMessage(StorageInstallReceiptMessage, clientId, writer);
            }
        }

        void BroadcastFortressState()
        {
            using (var writer = CreateFortressStateWriter())
                networkManager.CustomMessagingManager.SendNamedMessageToAll(FortressStateMessage, writer);
        }

        void SendFortressState(ulong clientId)
        {
            using (var writer = CreateFortressStateWriter())
                networkManager.CustomMessagingManager.SendNamedMessage(FortressStateMessage, clientId, writer);
        }

        static FastBufferWriter CreateFortressStateWriter()
        {
            GameManager gameManager = GameManager.Instance;
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool visible = gameManager != null && gameManager.FortressVisible &&
                           gameManager.TryGetFortressTransform(out position, out rotation);
            var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(visible);
            writer.WriteValueSafe(position);
            writer.WriteValueSafe(rotation);
            return writer;
        }

        void BroadcastFortressDoorState()
        {
            using (var writer = CreateBoolWriter(FindFortressDoor()?.IsOpen == true))
                networkManager.CustomMessagingManager.SendNamedMessageToAll(FortressDoorStateMessage, writer);
        }

        void SendFortressDoorState(ulong clientId)
        {
            using (var writer = CreateBoolWriter(FindFortressDoor()?.IsOpen == true))
                networkManager.CustomMessagingManager.SendNamedMessage(FortressDoorStateMessage, clientId, writer);
        }

        void BroadcastEndingState()
        {
            using (var writer = CreateBoolWriter(GameManager.Instance?.EndingStarted == true))
                networkManager.CustomMessagingManager.SendNamedMessageToAll(EndingStateMessage, writer);
        }

        void SendEndingState(ulong clientId)
        {
            using (var writer = CreateBoolWriter(GameManager.Instance?.EndingStarted == true))
                networkManager.CustomMessagingManager.SendNamedMessage(EndingStateMessage, clientId, writer);
        }

        static FastBufferWriter CreateBoolWriter(bool value)
        {
            var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe(value);
            return writer;
        }

        void SendDropSpawnRequest(RaftDroppedItemReplica marker, ItemId item, Vector3 velocity)
        {
            SendDropSpawnMessage(DropSpawnRequestMessage, NetworkManager.ServerClientId, marker, item, velocity);
        }

        void BroadcastDropSpawn(RaftDroppedItemReplica marker, ItemId item, Vector3 velocity)
        {
            using (var writer = CreateDropSpawnWriter(marker, item, velocity))
                networkManager.CustomMessagingManager.SendNamedMessageToAll(DropSpawnStateMessage, writer);
        }

        void SendDropSpawn(ulong clientId, RaftDroppedItemReplica marker, ItemId item, Vector3 velocity)
        {
            SendDropSpawnMessage(DropSpawnStateMessage, clientId, marker, item, velocity);
        }

        void SendDropSpawnMessage(string messageName, ulong clientId, RaftDroppedItemReplica marker,
            ItemId item, Vector3 velocity)
        {
            using (var writer = CreateDropSpawnWriter(marker, item, velocity))
                networkManager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer);
        }

        static FastBufferWriter CreateDropSpawnWriter(RaftDroppedItemReplica marker, ItemId item, Vector3 velocity)
        {
            var writer = new FastBufferWriter(4096, Allocator.Temp);
            writer.WriteValueSafe(marker.Id);
            writer.WriteValueSafe((int)item);
            writer.WriteValueSafe(marker.transform.position);
            writer.WriteValueSafe(marker.transform.rotation);
            writer.WriteValueSafe(velocity);
            return writer;
        }

        static void ReadDropSpawn(FastBufferReader reader, out string id, out ItemId item,
            out Vector3 position, out Quaternion rotation, out Vector3 velocity)
        {
            reader.ReadValueSafe(out id);
            reader.ReadValueSafe(out int itemValue);
            reader.ReadValueSafe(out position);
            reader.ReadValueSafe(out rotation);
            reader.ReadValueSafe(out velocity);
            item = IsValidItem(itemValue) ? (ItemId)itemValue : ItemId.Wood;
        }

        void SendDropCollectReceipt(ulong clientId, string id, ItemId item, bool accepted)
        {
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(id);
                writer.WriteValueSafe((int)item);
                writer.WriteValueSafe(accepted);
                networkManager.CustomMessagingManager.SendNamedMessage(DropCollectReceiptMessage, clientId, writer);
            }
        }

        void BroadcastDropDespawn(string id)
        {
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(id);
                networkManager.CustomMessagingManager.SendNamedMessageToAll(DropDespawnMessage, writer);
            }
        }

        void SendEmptyMessage(string messageName, ulong clientId)
        {
            using (var writer = new FastBufferWriter(1, Allocator.Temp))
            {
                writer.WriteValueSafe((byte)1);
                networkManager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer);
            }
        }

        void SendStringMessage(string messageName, ulong clientId, string value)
        {
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(value);
                networkManager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer);
            }
        }

        bool IsNetworkReady() => networkManager != null && networkManager.IsListening;

        bool IsValidServerRequest(ulong senderClientId) => networkManager.IsServer &&
            networkManager.ConnectedClients.ContainsKey(senderClientId);

        static RaftUpgradeStation FindRaft() =>
            FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);

        static FortressDoor FindFortressDoor() =>
            FindFirstObjectByType<FortressDoor>(FindObjectsInactive.Include);

        static EndingDrain FindEndingDrain() =>
            FindFirstObjectByType<EndingDrain>(FindObjectsInactive.Include);

        static bool IsValidItem(int itemValue) => itemValue >= 0 &&
            itemValue < Enum.GetValues(typeof(ItemId)).Length;

        static void ConsumeSelectedStorage(Inventory inventory)
        {
            if (inventory?.SelectedItem == ItemId.RaftStorageBox) inventory.ConsumeSelected(out _);
            else inventory?.Consume(ItemId.RaftStorageBox);
        }

        static void ShowUpgradeNotice(RaftUpgradeStation raft, byte action)
        {
            if (raft == null) return;
            GameUI.Instance?.ShowNotice(action == 0
                ? $"Shared raft expansion {raft.ExpansionLevel}/{raft.MaxExpansionLevel}."
                : $"Shared raft durability {raft.Durability:0}/{raft.MaxDurability:0}.", 2.5f);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            if (RaftMultiplayerHooks.TryUpgradeRaft == TryUpgradeRaft) RaftMultiplayerHooks.TryUpgradeRaft = null;
            if (RaftMultiplayerHooks.TryReleaseHeldItem == TryReleaseHeldItem) RaftMultiplayerHooks.TryReleaseHeldItem = null;
            if (RaftMultiplayerHooks.TryInstallStorage == TryInstallStorage) RaftMultiplayerHooks.TryInstallStorage = null;
            if (RaftMultiplayerHooks.TryStoreItem == TryStoreItem) RaftMultiplayerHooks.TryStoreItem = null;
            if (RaftMultiplayerHooks.TryTakeItem == TryTakeItem) RaftMultiplayerHooks.TryTakeItem = null;
            if (RaftMultiplayerHooks.TryCollectDroppedItem == TryCollectDroppedItem)
                RaftMultiplayerHooks.TryCollectDroppedItem = null;
            if (RaftMultiplayerHooks.TryDiscoverFortress == TryDiscoverFortress)
                RaftMultiplayerHooks.TryDiscoverFortress = null;
            if (RaftMultiplayerHooks.TryOpenFortressDoor == TryOpenFortressDoor)
                RaftMultiplayerHooks.TryOpenFortressDoor = null;
            if (RaftMultiplayerHooks.TryDrainOcean == TryDrainOcean)
                RaftMultiplayerHooks.TryDrainOcean = null;
            if (RaftMultiplayerHooks.TryRestartGame == TryRestartGame)
                RaftMultiplayerHooks.TryRestartGame = null;
            if (!handlersRegistered || networkManager?.CustomMessagingManager == null) return;

            var messages = networkManager.CustomMessagingManager;
            messages.UnregisterNamedMessageHandler(StateRequestMessage);
            messages.UnregisterNamedMessageHandler(UpgradeRequestMessage);
            messages.UnregisterNamedMessageHandler(UpgradeReceiptMessage);
            messages.UnregisterNamedMessageHandler(UpgradeStateMessage);
            messages.UnregisterNamedMessageHandler(StorageInstallRequestMessage);
            messages.UnregisterNamedMessageHandler(StorageInstallReceiptMessage);
            messages.UnregisterNamedMessageHandler(StorageTransferRequestMessage);
            messages.UnregisterNamedMessageHandler(StorageTransferReceiptMessage);
            messages.UnregisterNamedMessageHandler(StorageStateMessage);
            messages.UnregisterNamedMessageHandler(DropSpawnRequestMessage);
            messages.UnregisterNamedMessageHandler(DropSpawnStateMessage);
            messages.UnregisterNamedMessageHandler(DropTransformMessage);
            messages.UnregisterNamedMessageHandler(DropCollectRequestMessage);
            messages.UnregisterNamedMessageHandler(DropCollectReceiptMessage);
            messages.UnregisterNamedMessageHandler(DropDespawnMessage);
            messages.UnregisterNamedMessageHandler(FortressDiscoverRequestMessage);
            messages.UnregisterNamedMessageHandler(FortressStateMessage);
            messages.UnregisterNamedMessageHandler(FortressDoorRequestMessage);
            messages.UnregisterNamedMessageHandler(FortressDoorStateMessage);
            messages.UnregisterNamedMessageHandler(EndingRequestMessage);
            messages.UnregisterNamedMessageHandler(EndingStateMessage);
            messages.UnregisterNamedMessageHandler(RestartRequestMessage);
        }
    }

    public sealed class RaftDroppedItemReplica : MonoBehaviour
    {
        public string Id { get; private set; }
        public bool IsAuthoritative { get; private set; }

        public void Configure(string id, bool authoritative)
        {
            Id = id;
            IsAuthoritative = authoritative;
        }
    }
}
