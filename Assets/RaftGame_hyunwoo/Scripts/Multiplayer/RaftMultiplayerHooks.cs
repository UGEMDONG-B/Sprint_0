using System;

namespace RaftSharkDive
{
    /// <summary>
    /// Optional hooks implemented by the Raft multiplayer adapter.
    /// Single-player keeps using the original local behavior when no hook is installed.
    /// </summary>
    public static class RaftMultiplayerHooks
    {
        public static Func<RaftBuildPoint, PlayerController, bool> TryDepositRaftMaterials;
        public static Func<LootCrate, PlayerController, bool> TryOpenLootCrate;
        public static Func<PickupItem, PlayerController, bool> TryCollectPickup;
        public static Func<RaftUpgradeStation, PlayerController, bool> TryUpgradeRaft;
        public static Func<HeldItemController, float, bool, bool> TryReleaseHeldItem;
        public static Func<HeldItemController, UnityEngine.Transform, UnityEngine.Vector3, bool> TryInstallStorage;
        public static Func<RaftStorageBox, Inventory, int, bool> TryStoreItem;
        public static Func<RaftStorageBox, Inventory, int, bool> TryTakeItem;
        public static Func<PickupItem, PlayerController, bool> TryCollectDroppedItem;
        public static Func<GameManager, bool> TryDiscoverFortress;
        public static Func<FortressDoor, PlayerController, bool> TryOpenFortressDoor;
        public static Func<EndingDrain, PlayerController, bool> TryDrainOcean;
        public static Func<GameManager, bool> TryRestartGame;
    }
}
