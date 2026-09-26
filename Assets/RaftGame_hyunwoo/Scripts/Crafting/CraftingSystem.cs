using UnityEngine;

namespace RaftSharkDive
{
    [RequireComponent(typeof(Inventory))]
    public sealed class CraftingSystem : MonoBehaviour
    {
        [SerializeField, Min(1)] private int scrapRequired = 3;
        [SerializeField, Min(1)] private int rubberRequired = 2;
        [SerializeField, Min(1)] private int filterRequired = 1;
        [SerializeField, Min(1)] private int rescueRopeCost = 2;
        [SerializeField, Min(1)] private int storageBoxWoodCost = 5;
        [SerializeField] private GameObject equippedTankVisual;

        private Inventory inventory;
        public int ScrapRequired => scrapRequired;
        public int RubberRequired => rubberRequired;
        public int FilterRequired => filterRequired;
        public int RescueRopeCost => rescueRopeCost;
        public int StorageBoxWoodCost => storageBoxWoodCost;

        private void Awake() => inventory = GetComponent<Inventory>();

        public bool CanCraftTank => inventory != null && GetAvailableCount(ItemId.OxygenTank) == 0 &&
                                    GetAvailableCount(ItemId.Scrap) >= scrapRequired &&
                                    GetAvailableCount(ItemId.Rubber) >= rubberRequired &&
                                    GetAvailableCount(ItemId.Filter) >= filterRequired &&
                                    HasOutputSlotAfterConsuming(ItemId.Scrap, ItemId.Rubber, ItemId.Filter);
        public bool CanCraftRescueRope => inventory != null && GetAvailableCount(ItemId.Rope) >= rescueRopeCost &&
                                          HasOutputSlotAfterConsuming(ItemId.Rope);
        public bool CanCraftStorageBox => inventory != null && inventory.Has(ItemId.Wood, storageBoxWoodCost) &&
                                          GameManager.Instance != null && GameManager.Instance.RaftBuilt &&
                                          RaftStorageBox.FindExisting() == null &&
                                          !inventory.Has(ItemId.RaftStorageBox);

        public void TryCraftOxygenTank()
        {
            if (GetAvailableCount(ItemId.OxygenTank) > 0)
            {
                GameUI.Instance?.ShowNotice("Oxygen Tank already crafted.", 2f);
                return;
            }
            if (!CanCraftTank)
            {
                GameUI.Instance?.ShowNotice("Not enough materials.", 2f);
                return;
            }

            ConsumeAvailable(ItemId.Scrap, scrapRequired);
            ConsumeAvailable(ItemId.Rubber, rubberRequired);
            ConsumeAvailable(ItemId.Filter, filterRequired);
            inventory.TryAdd(ItemId.OxygenTank, 1, true);
            GetComponent<PlayerOxygen>()?.OnTankCrafted();
            GameManager.Instance?.OnOxygenTankCrafted();
        }

        public void TryCraftRescueRope()
        {
            if (!CanCraftRescueRope)
            {
                GameUI.Instance?.ShowNotice($"Need Rope x{rescueRopeCost}.", 2f);
                return;
            }
            ConsumeAvailable(ItemId.Rope, rescueRopeCost);
            if (!inventory.TryAdd(ItemId.RescueRope, 1, true))
            {
                GameUI.Instance?.ShowNotice("Inventory needs a free slot for the Rescue Rope.", 2f);
                return;
            }
            GameUI.Instance?.ShowNotice("Rescue Rope crafted. Press R to pull back a distant teammate.", 3f);
        }

        public GameObject EquippedTankVisual => equippedTankVisual;

        public int GetAvailableCount(ItemId item)
        {
            RaftStorageBox storage = RaftStorageBox.FindExisting();
            return (inventory != null ? inventory.GetCount(item) : 0) + (storage != null ? storage.GetCount(item) : 0);
        }

        private bool HasOutputSlotAfterConsuming(params ItemId[] ingredients)
        {
            if (inventory == null) return false;
            if (inventory.OccupiedSlotCount < inventory.MaxSlots) return true;
            for (int i = 0; i < ingredients.Length; i++)
                if (inventory.GetCount(ingredients[i]) > 0) return true;
            return false;
        }

        private void ConsumeAvailable(ItemId item, int amount)
        {
            int fromInventory = Mathf.Min(amount, inventory != null ? inventory.GetCount(item) : 0);
            if (fromInventory > 0) inventory.Consume(item, fromInventory);
            int remaining = amount - fromInventory;
            if (remaining > 0) RaftStorageBox.FindExisting()?.Consume(item, remaining);
        }

        public void TryCraftStorageBox()
        {
            if (RaftStorageBox.FindExisting() != null || inventory.Has(ItemId.RaftStorageBox))
            {
                GameUI.Instance?.ShowNotice("Raft Storage already exists or is in your inventory.", 2f);
                return;
            }
            if (GameManager.Instance == null || !GameManager.Instance.RaftBuilt)
            {
                GameUI.Instance?.ShowNotice("Build the raft before crafting storage.", 2f);
                return;
            }
            if (inventory == null || !inventory.Has(ItemId.Wood, storageBoxWoodCost))
            {
                GameUI.Instance?.ShowNotice($"Need Wood x{storageBoxWoodCost} for Raft Storage.", 2f);
                return;
            }
            inventory.Consume(ItemId.Wood, storageBoxWoodCost);
            if (!inventory.TryAdd(ItemId.RaftStorageBox, 1, true))
            {
                inventory.TryAdd(ItemId.Wood, storageBoxWoodCost);
                return;
            }
            GameUI.Instance?.ShowNotice("Raft Storage crafted. Select it and press F on the raft to install.", 4f);
        }
    }
}
