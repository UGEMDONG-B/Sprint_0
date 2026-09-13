using UnityEngine;

namespace RaftSharkDive
{
    public sealed class LootCrate : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(0)] private int scrap = 2;
        [SerializeField, Min(0)] private int rubber = 1;
        [SerializeField, Min(0)] private int rope = 1;

        public int Scrap => scrap;
        public int Rubber => rubber;
        public int Rope => rope;
        public int TotalItems => scrap + rubber + rope;

        public string GetInteractionPrompt(PlayerController player) => "E - Open Supply Box";
        public bool CanInteract(PlayerController player) => player != null;

        public void Interact(PlayerController player)
        {
            if (RaftMultiplayerHooks.TryOpenLootCrate != null &&
                RaftMultiplayerHooks.TryOpenLootCrate(this, player))
            {
                return;
            }

            TryCollectLocal(player);
        }

        public bool TryCollectLocal(PlayerController player)
        {
            if (player == null) return false;
            Inventory inventory = player.GetComponent<Inventory>();
            if (inventory == null) return false;
            if (inventory.FreeSlotCount < TotalItems)
            {
                GameUI.Instance?.ShowNotice($"Need {TotalItems} empty inventory slots.", 2f);
                return false;
            }
            inventory.Add(ItemId.Scrap, scrap);
            inventory.Add(ItemId.Rubber, rubber);
            inventory.Add(ItemId.Rope, rope);
            GameUI.Instance?.ShowNotice("Supply box collected.", 2f);
            gameObject.SetActive(false);
            return true;
        }

        public void ApplyCollectedState(bool collected) => gameObject.SetActive(!collected);
    }
}
