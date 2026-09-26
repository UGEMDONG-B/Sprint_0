using UnityEngine;

namespace RaftSharkDive
{
    [RequireComponent(typeof(Collider))]
    public sealed class RaftBuildPoint : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject raftToActivate;
        [SerializeField, Min(1)] private int woodRequired = 5;
        [SerializeField, Min(1)] private int ropeRequired = 2;

        private bool built;
        private int depositedWood;
        private int depositedRope;

        public GameObject RaftToActivate => raftToActivate;
        public bool IsBuilt => built;
        public int DepositedWood => depositedWood;
        public int DepositedRope => depositedRope;
        public int WoodRequired => woodRequired;
        public int RopeRequired => ropeRequired;

        public void Configure(GameObject raft)
        {
            raftToActivate = raft;
        }

        public void ApplyBuiltState(bool value)
        {
            built = value;
            if (value)
            {
                depositedWood = woodRequired;
                depositedRope = ropeRequired;
            }
            if (raftToActivate != null) raftToActivate.SetActive(value);
            gameObject.SetActive(!value);
        }

        public void ApplyDeposits(int wood, int rope)
        {
            depositedWood = Mathf.Clamp(wood, 0, woodRequired);
            depositedRope = Mathf.Clamp(rope, 0, ropeRequired);
        }

        public string GetInteractionPrompt(PlayerController player)
        {
            if (built || player == null) return string.Empty;
            return $"E - Deposit materials  Wood {depositedWood}/{woodRequired}  Rope {depositedRope}/{ropeRequired}";
        }

        public bool CanInteract(PlayerController player) => !built && player != null;

        public void Interact(PlayerController player)
        {
            if (built || player == null) return;

            if (RaftMultiplayerHooks.TryDepositRaftMaterials != null &&
                RaftMultiplayerHooks.TryDepositRaftMaterials(this, player))
            {
                return;
            }

            DepositLocal(player);
        }

        public void ApplyNetworkState(int wood, int rope, bool isBuilt)
        {
            ApplyDeposits(wood, rope);
            ApplyBuiltState(isBuilt);
            if (isBuilt) GameManager.Instance?.NotifyRaftBuilt();
        }

        private void DepositLocal(PlayerController player)
        {
            Inventory inventory = player.GetComponent<Inventory>();
            if (inventory == null) return;

            int woodToDeposit = Mathf.Min(woodRequired - depositedWood, inventory.GetCount(ItemId.Wood));
            int ropeToDeposit = Mathf.Min(ropeRequired - depositedRope, inventory.GetCount(ItemId.Rope));
            if (woodToDeposit > 0)
            {
                inventory.Consume(ItemId.Wood, woodToDeposit);
                depositedWood += woodToDeposit;
            }
            if (ropeToDeposit > 0)
            {
                inventory.Consume(ItemId.Rope, ropeToDeposit);
                depositedRope += ropeToDeposit;
            }

            if (depositedWood < woodRequired || depositedRope < ropeRequired)
            {
                GameUI.Instance?.ShowNotice(
                    $"Raft materials: Wood {depositedWood}/{woodRequired}, Rope {depositedRope}/{ropeRequired}", 2.5f);
                return;
            }

            if (raftToActivate != null) raftToActivate.SetActive(true);
            built = true;
            GameManager.Instance?.NotifyRaftBuilt();
            gameObject.SetActive(false);
        }
    }
}
