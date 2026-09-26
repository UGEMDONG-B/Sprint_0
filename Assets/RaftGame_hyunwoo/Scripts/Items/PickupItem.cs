using UnityEngine;

namespace RaftSharkDive
{
    public sealed class PickupItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemId item;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField] private bool rotate = true;

        public ItemId Item => item;
        public int Amount => amount;
        public bool Rotates => rotate;

        private void Update()
        {
            if (rotate) transform.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
        }

        public string GetInteractionPrompt(PlayerController player) => $"E - Pick Up {item} x{amount}";
        public bool CanInteract(PlayerController player) => player != null;

        public void Interact(PlayerController player)
        {
            if (RaftMultiplayerHooks.TryCollectPickup != null &&
                RaftMultiplayerHooks.TryCollectPickup(this, player))
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
            if (!inventory.TryAdd(item, amount))
            {
                GameUI.Instance?.ShowNotice($"Inventory full ({inventory.MaxSlots} slots).", 2f);
                return false;
            }
            GameUI.Instance?.ShowNotice($"Picked up {item} x{amount}", 2f);
            gameObject.SetActive(false);
            return true;
        }

        public void ApplyCollectedState(bool collected) => gameObject.SetActive(!collected);

        public void Configure(ItemId newItem, int newAmount)
        {
            item = newItem;
            amount = Mathf.Max(1, newAmount);
        }

        public void ConfigureUnderwater()
        {
            rotate = false;
        }

        public void ConfigureThrown()
        {
            rotate = false;
        }
    }
}
