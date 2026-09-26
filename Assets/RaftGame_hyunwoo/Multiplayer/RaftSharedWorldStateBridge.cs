using System.Collections.Generic;
using RaftSharkDive;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaftGame.Hyunwoo.Multiplayer
{
    /// <summary>
    /// Host-authoritative synchronization for the first shared Raft gameplay slice:
    /// raft material deposits/build completion and one-time supply-box collection.
    /// </summary>
    public sealed class RaftSharedWorldStateBridge : MonoBehaviour
    {
        const string StateRequestMessage = "Raft.State.Request.v1";
        const string BuildRequestMessage = "Raft.Build.Request.v1";
        const string BuildReceiptMessage = "Raft.Build.Receipt.v1";
        const string BuildStateMessage = "Raft.Build.State.v1";
        const string LootRequestMessage = "Raft.Loot.Request.v1";
        const string LootReceiptMessage = "Raft.Loot.Receipt.v1";
        const string LootStateMessage = "Raft.Loot.State.v1";
        const string PickupRequestMessage = "Raft.Pickup.Request.v1";
        const string PickupReceiptMessage = "Raft.Pickup.Receipt.v1";
        const string PickupStateMessage = "Raft.Pickup.State.v1";

        readonly HashSet<string> pendingLootRequests = new HashSet<string>();
        readonly HashSet<string> pendingPickupRequests = new HashSet<string>();

        NetworkManager networkManager;
        bool handlersRegistered;
        bool initialStateRequested;
        bool initialStateReceived;
        bool depositPending;
        float nextStateRequestAt;

        void Awake()
        {
            networkManager = NetworkManager.Singleton;
            RegisterHandlers();
            RaftMultiplayerHooks.TryDepositRaftMaterials = TryDepositRaftMaterials;
            RaftMultiplayerHooks.TryOpenLootCrate = TryOpenLootCrate;
            RaftMultiplayerHooks.TryCollectPickup = TryCollectPickup;
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        void Update()
        {
            if (networkManager == null || !networkManager.IsListening || FindBuildPoint() == null)
            {
                return;
            }

            if (networkManager.IsServer)
            {
                if (!initialStateRequested)
                {
                    initialStateRequested = true;
                    BroadcastBuildState();
                }
            }
            else if (!initialStateReceived && Time.unscaledTime >= nextStateRequestAt)
            {
                initialStateRequested = true;
                nextStateRequestAt = Time.unscaledTime + 1f;
                SendStateRequest();
            }
        }

        bool TryDepositRaftMaterials(RaftBuildPoint point, PlayerController player)
        {
            if (networkManager == null || !networkManager.IsListening)
            {
                return false;
            }

            if (depositPending)
            {
                GameUI.Instance?.ShowNotice("Waiting for the host to confirm the raft materials.", 1.5f);
                return true;
            }

            var inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (point == null || inventory == null)
            {
                return true;
            }

            var requestedWood = Mathf.Min(point.WoodRequired - point.DepositedWood,
                inventory.GetCount(ItemId.Wood));
            var requestedRope = Mathf.Min(point.RopeRequired - point.DepositedRope,
                inventory.GetCount(ItemId.Rope));
            if (requestedWood <= 0 && requestedRope <= 0)
            {
                GameUI.Instance?.ShowNotice("No required raft materials in this inventory.", 1.5f);
                return true;
            }

            if (networkManager.IsServer)
            {
                ProcessBuildRequest(networkManager.LocalClientId, requestedWood, requestedRope, player);
            }
            else
            {
                depositPending = true;
                using (var writer = new FastBufferWriter(16, Allocator.Temp))
                {
                    writer.WriteValueSafe(requestedWood);
                    writer.WriteValueSafe(requestedRope);
                    networkManager.CustomMessagingManager.SendNamedMessage(
                        BuildRequestMessage, NetworkManager.ServerClientId, writer);
                }
            }

            return true;
        }

        bool TryOpenLootCrate(LootCrate crate, PlayerController player)
        {
            if (networkManager == null || !networkManager.IsListening)
            {
                return false;
            }

            var inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (crate == null || inventory == null)
            {
                return true;
            }

            if (inventory.FreeSlotCount < crate.TotalItems)
            {
                GameUI.Instance?.ShowNotice($"Need {crate.TotalItems} empty inventory slots.", 2f);
                return true;
            }

            var path = GetHierarchyPath(crate.transform);
            if (pendingLootRequests.Contains(path))
            {
                return true;
            }

            if (networkManager.IsServer)
            {
                ProcessLootRequest(networkManager.LocalClientId, path, player);
            }
            else
            {
                pendingLootRequests.Add(path);
                SendPathMessage(LootRequestMessage, NetworkManager.ServerClientId, path);
            }

            return true;
        }

        bool TryCollectPickup(PickupItem pickup, PlayerController player)
        {
            if (pickup != null && pickup.GetComponent<ThrownPickupMotion>() != null)
            {
                return RaftMultiplayerHooks.TryCollectDroppedItem != null &&
                       RaftMultiplayerHooks.TryCollectDroppedItem(pickup, player);
            }

            if (networkManager == null || !networkManager.IsListening || !IsSharedMapPickup(pickup))
            {
                return false;
            }

            var inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (pickup == null || inventory == null)
            {
                return true;
            }

            if (inventory.FreeSlotCount < pickup.Amount)
            {
                GameUI.Instance?.ShowNotice($"Inventory full ({inventory.MaxSlots} slots).", 2f);
                return true;
            }

            var path = GetHierarchyPath(pickup.transform);
            if (pendingPickupRequests.Contains(path))
            {
                return true;
            }

            if (networkManager.IsServer)
            {
                ProcessPickupRequest(networkManager.LocalClientId, path, player);
            }
            else
            {
                pendingPickupRequests.Add(path);
                SendPathMessage(PickupRequestMessage, NetworkManager.ServerClientId, path);
            }

            return true;
        }

        void RegisterHandlers()
        {
            if (handlersRegistered || networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            var messages = networkManager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(StateRequestMessage, OnStateRequest);
            messages.RegisterNamedMessageHandler(BuildRequestMessage, OnBuildRequest);
            messages.RegisterNamedMessageHandler(BuildReceiptMessage, OnBuildReceipt);
            messages.RegisterNamedMessageHandler(BuildStateMessage, OnBuildState);
            messages.RegisterNamedMessageHandler(LootRequestMessage, OnLootRequest);
            messages.RegisterNamedMessageHandler(LootReceiptMessage, OnLootReceipt);
            messages.RegisterNamedMessageHandler(LootStateMessage, OnLootState);
            messages.RegisterNamedMessageHandler(PickupRequestMessage, OnPickupRequest);
            messages.RegisterNamedMessageHandler(PickupReceiptMessage, OnPickupReceipt);
            messages.RegisterNamedMessageHandler(PickupStateMessage, OnPickupState);
            handlersRegistered = true;
            Debug.Log("[RaftMultiplayer] Shared raft progress and supply-box state bridge ready.");
        }

        void OnStateRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!networkManager.IsServer || !networkManager.ConnectedClients.ContainsKey(senderClientId))
            {
                return;
            }

            SendBuildState(senderClientId);
            foreach (var crate in FindObjectsByType<LootCrate>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (!crate.gameObject.activeSelf)
                {
                    SendPathMessage(LootStateMessage, senderClientId, GetHierarchyPath(crate.transform));
                }
            }

            foreach (var pickup in FindObjectsByType<PickupItem>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (IsSharedMapPickup(pickup) && !pickup.gameObject.activeSelf)
                {
                    SendPathMessage(PickupStateMessage, senderClientId, GetHierarchyPath(pickup.transform));
                }
            }
        }

        void OnBuildRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!networkManager.IsServer || !networkManager.ConnectedClients.ContainsKey(senderClientId))
            {
                return;
            }

            reader.ReadValueSafe(out int requestedWood);
            reader.ReadValueSafe(out int requestedRope);
            ProcessBuildRequest(senderClientId, Mathf.Max(0, requestedWood), Mathf.Max(0, requestedRope), null);
        }

        void ProcessBuildRequest(ulong senderClientId, int requestedWood, int requestedRope,
            PlayerController serverPlayer)
        {
            var point = FindBuildPoint();
            if (point == null)
            {
                SendBuildReceipt(senderClientId, 0, 0, 0, 0, false);
                return;
            }

            var acceptedWood = Mathf.Min(requestedWood, point.WoodRequired - point.DepositedWood);
            var acceptedRope = Mathf.Min(requestedRope, point.RopeRequired - point.DepositedRope);
            var totalWood = point.DepositedWood + acceptedWood;
            var totalRope = point.DepositedRope + acceptedRope;
            var isBuilt = totalWood >= point.WoodRequired && totalRope >= point.RopeRequired;

            point.ApplyNetworkState(totalWood, totalRope, isBuilt);

            if (senderClientId == networkManager.LocalClientId && serverPlayer != null)
            {
                ConsumeAcceptedMaterials(serverPlayer, acceptedWood, acceptedRope);
                ShowBuildProgress(totalWood, totalRope, point.WoodRequired, point.RopeRequired, isBuilt);
            }
            else
            {
                SendBuildReceipt(senderClientId, acceptedWood, acceptedRope, totalWood, totalRope, isBuilt);
            }

            BroadcastBuildState();
            Debug.Log($"[RaftMultiplayer] Host accepted raft materials from client {senderClientId}: " +
                      $"Wood +{acceptedWood}, Rope +{acceptedRope}.");
        }

        void OnBuildReceipt(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out int acceptedWood);
            reader.ReadValueSafe(out int acceptedRope);
            reader.ReadValueSafe(out int totalWood);
            reader.ReadValueSafe(out int totalRope);
            reader.ReadValueSafe(out bool isBuilt);
            depositPending = false;

            var player = FindFirstObjectByType<PlayerController>();
            ConsumeAcceptedMaterials(player, acceptedWood, acceptedRope);
            var point = ApplyBuildState(totalWood, totalRope, isBuilt);
            if (point != null)
            {
                ShowBuildProgress(totalWood, totalRope, point.WoodRequired, point.RopeRequired, isBuilt);
            }
        }

        void OnBuildState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out int wood);
            reader.ReadValueSafe(out int rope);
            reader.ReadValueSafe(out bool isBuilt);
            initialStateReceived = true;
            ApplyBuildState(wood, rope, isBuilt);
        }

        void OnLootRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!networkManager.IsServer || !networkManager.ConnectedClients.ContainsKey(senderClientId))
            {
                return;
            }

            reader.ReadValueSafe(out string path);
            ProcessLootRequest(senderClientId, path, null);
        }

        void ProcessLootRequest(ulong senderClientId, string path, PlayerController serverPlayer)
        {
            var crate = FindLootCrate(path);
            var collected = crate != null && crate.gameObject.activeSelf;
            if (collected && senderClientId == networkManager.LocalClientId && serverPlayer != null)
            {
                collected = crate.TryCollectLocal(serverPlayer);
            }
            else if (collected)
            {
                crate.ApplyCollectedState(true);
            }

            if (senderClientId != networkManager.LocalClientId)
            {
                SendLootReceipt(senderClientId, path, collected);
            }

            if (collected)
            {
                BroadcastPathMessage(LootStateMessage, path);
                Debug.Log($"[RaftMultiplayer] Host assigned supply box '{path}' to client {senderClientId}.");
            }
        }

        void OnLootReceipt(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out string path);
            reader.ReadValueSafe(out bool collected);
            pendingLootRequests.Remove(path);
            if (!collected)
            {
                GameUI.Instance?.ShowNotice("That supply box was already collected.", 1.5f);
                return;
            }

            var crate = FindLootCrate(path);
            var player = FindFirstObjectByType<PlayerController>();
            crate?.TryCollectLocal(player);
        }

        void OnLootState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out string path);
            FindLootCrate(path)?.ApplyCollectedState(true);
        }

        void OnPickupRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!networkManager.IsServer || !networkManager.ConnectedClients.ContainsKey(senderClientId))
            {
                return;
            }

            reader.ReadValueSafe(out string path);
            ProcessPickupRequest(senderClientId, path, null);
        }

        void ProcessPickupRequest(ulong senderClientId, string path, PlayerController serverPlayer)
        {
            var pickup = FindPickup(path);
            var collected = pickup != null && pickup.gameObject.activeSelf;
            if (collected && senderClientId == networkManager.LocalClientId && serverPlayer != null)
            {
                collected = pickup.TryCollectLocal(serverPlayer);
            }
            else if (collected)
            {
                pickup.ApplyCollectedState(true);
            }

            if (senderClientId != networkManager.LocalClientId)
            {
                SendPickupReceipt(senderClientId, path, collected);
            }

            if (collected)
            {
                BroadcastPathMessage(PickupStateMessage, path);
                Debug.Log($"[RaftMultiplayer] Host assigned map pickup '{path}' to client {senderClientId}.");
            }
        }

        void OnPickupReceipt(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out string path);
            reader.ReadValueSafe(out bool collected);
            pendingPickupRequests.Remove(path);
            if (!collected)
            {
                GameUI.Instance?.ShowNotice("That item was already collected.", 1.5f);
                return;
            }

            var pickup = FindPickup(path);
            var player = FindFirstObjectByType<PlayerController>();
            pickup?.TryCollectLocal(player);
        }

        void OnPickupState(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            reader.ReadValueSafe(out string path);
            FindPickup(path)?.ApplyCollectedState(true);
        }

        void SendStateRequest()
        {
            using (var writer = new FastBufferWriter(1, Allocator.Temp))
            {
                writer.WriteValueSafe((byte)1);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    StateRequestMessage, NetworkManager.ServerClientId, writer);
            }
        }

        void BroadcastBuildState()
        {
            var point = FindBuildPoint();
            if (point == null) return;

            ApplyBuildState(point.DepositedWood, point.DepositedRope, point.IsBuilt);
            using (var writer = new FastBufferWriter(16, Allocator.Temp))
            {
                writer.WriteValueSafe(point.DepositedWood);
                writer.WriteValueSafe(point.DepositedRope);
                writer.WriteValueSafe(point.IsBuilt);
                networkManager.CustomMessagingManager.SendNamedMessageToAll(BuildStateMessage, writer);
            }
        }

        void SendBuildState(ulong clientId)
        {
            var point = FindBuildPoint();
            if (point == null) return;
            using (var writer = new FastBufferWriter(16, Allocator.Temp))
            {
                writer.WriteValueSafe(point.DepositedWood);
                writer.WriteValueSafe(point.DepositedRope);
                writer.WriteValueSafe(point.IsBuilt);
                networkManager.CustomMessagingManager.SendNamedMessage(BuildStateMessage, clientId, writer);
            }
        }

        void SendBuildReceipt(ulong clientId, int acceptedWood, int acceptedRope, int totalWood, int totalRope,
            bool isBuilt)
        {
            using (var writer = new FastBufferWriter(32, Allocator.Temp))
            {
                writer.WriteValueSafe(acceptedWood);
                writer.WriteValueSafe(acceptedRope);
                writer.WriteValueSafe(totalWood);
                writer.WriteValueSafe(totalRope);
                writer.WriteValueSafe(isBuilt);
                networkManager.CustomMessagingManager.SendNamedMessage(BuildReceiptMessage, clientId, writer);
            }
        }

        void SendLootReceipt(ulong clientId, string path, bool collected)
        {
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(path);
                writer.WriteValueSafe(collected);
                networkManager.CustomMessagingManager.SendNamedMessage(LootReceiptMessage, clientId, writer);
            }
        }

        void SendPickupReceipt(ulong clientId, string path, bool collected)
        {
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(path);
                writer.WriteValueSafe(collected);
                networkManager.CustomMessagingManager.SendNamedMessage(PickupReceiptMessage, clientId, writer);
            }
        }

        void SendPathMessage(string messageName, ulong clientId, string path)
        {
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(path);
                networkManager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer);
            }
        }

        void BroadcastPathMessage(string messageName, string path)
        {
            using (var writer = new FastBufferWriter(4096, Allocator.Temp))
            {
                writer.WriteValueSafe(path);
                networkManager.CustomMessagingManager.SendNamedMessageToAll(messageName, writer);
            }
        }

        static void ConsumeAcceptedMaterials(PlayerController player, int wood, int rope)
        {
            var inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (inventory == null) return;
            if (wood > 0) inventory.Consume(ItemId.Wood, wood);
            if (rope > 0) inventory.Consume(ItemId.Rope, rope);
        }

        static RaftBuildPoint ApplyBuildState(int wood, int rope, bool isBuilt)
        {
            var point = FindBuildPoint();
            point?.ApplyNetworkState(wood, rope, isBuilt);
            return point;
        }

        static RaftBuildPoint FindBuildPoint()
        {
            return FindFirstObjectByType<RaftBuildPoint>(FindObjectsInactive.Include);
        }

        static LootCrate FindLootCrate(string path)
        {
            foreach (var crate in FindObjectsByType<LootCrate>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (GetHierarchyPath(crate.transform) == path) return crate;
            }
            return null;
        }

        static PickupItem FindPickup(string path)
        {
            foreach (var pickup in FindObjectsByType<PickupItem>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (IsSharedMapPickup(pickup) && GetHierarchyPath(pickup.transform) == path) return pickup;
            }
            return null;
        }

        static bool IsSharedMapPickup(PickupItem pickup)
        {
            return pickup != null && pickup.GetComponent<ThrownPickupMotion>() == null &&
                   pickup.GetComponentInParent<EndlessOceanItemSpawner>() == null;
        }

        static string GetHierarchyPath(Transform target)
        {
            var path = target.name + "#" + target.GetSiblingIndex();
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "#" + target.GetSiblingIndex() + "/" + path;
            }
            return path;
        }

        static void ShowBuildProgress(int wood, int rope, int woodRequired, int ropeRequired, bool isBuilt)
        {
            GameUI.Instance?.ShowNotice(isBuilt
                ? "Raft built for every connected player."
                : $"Shared raft materials: Wood {wood}/{woodRequired}, Rope {rope}/{ropeRequired}", 2.5f);
        }

        void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != RaftMultiplayerBootstrap.MainScenePath) return;
            initialStateRequested = false;
            initialStateReceived = networkManager != null && networkManager.IsServer;
            depositPending = false;
            nextStateRequestAt = 0f;
            pendingLootRequests.Clear();
            pendingPickupRequests.Clear();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            if (RaftMultiplayerHooks.TryDepositRaftMaterials == TryDepositRaftMaterials)
                RaftMultiplayerHooks.TryDepositRaftMaterials = null;
            if (RaftMultiplayerHooks.TryOpenLootCrate == TryOpenLootCrate)
                RaftMultiplayerHooks.TryOpenLootCrate = null;
            if (RaftMultiplayerHooks.TryCollectPickup == TryCollectPickup)
                RaftMultiplayerHooks.TryCollectPickup = null;

            if (!handlersRegistered || networkManager == null || networkManager.CustomMessagingManager == null)
                return;

            var messages = networkManager.CustomMessagingManager;
            messages.UnregisterNamedMessageHandler(StateRequestMessage);
            messages.UnregisterNamedMessageHandler(BuildRequestMessage);
            messages.UnregisterNamedMessageHandler(BuildReceiptMessage);
            messages.UnregisterNamedMessageHandler(BuildStateMessage);
            messages.UnregisterNamedMessageHandler(LootRequestMessage);
            messages.UnregisterNamedMessageHandler(LootReceiptMessage);
            messages.UnregisterNamedMessageHandler(LootStateMessage);
            messages.UnregisterNamedMessageHandler(PickupRequestMessage);
            messages.UnregisterNamedMessageHandler(PickupReceiptMessage);
            messages.UnregisterNamedMessageHandler(PickupStateMessage);
        }
    }
}
