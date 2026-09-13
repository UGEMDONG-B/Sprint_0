using UnityEngine;
using UnityEngine.InputSystem;

namespace RaftSharkDive
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(Inventory))]
    public sealed class RescueRopeSystem : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float minimumRescueDistance = 15f;
        [SerializeField, Min(0.5f)] private float arrivalOffset = 1.5f;

        private PlayerController player;
        private Inventory inventory;

        public PlayerController LastRescuedPlayer { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<Inventory>();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame) return;
            if (player.CameraPivot == null || !player.CameraPivot.IsChildOf(transform)) return;
            TryUseRescueRope();
        }

        public bool TryUseRescueRope()
        {
            LastRescuedPlayer = null;
            if (inventory == null || !inventory.Has(ItemId.RescueRope))
            {
                GameUI.Instance?.ShowNotice("No Rescue Rope. Craft one with Rope x2.", 2f);
                return false;
            }

            PlayerController teammate = FindDistantTeammate();
            if (teammate == null)
            {
                GameUI.Instance?.ShowNotice($"No teammate is farther than {minimumRescueDistance:0}m.", 2f);
                return false;
            }

            Vector3 destination = transform.position + transform.right * arrivalOffset;
            if (Physics.Raycast(destination + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f,
                    ~0, QueryTriggerInteraction.Ignore))
                destination.y = hit.point.y + 0.05f;
            teammate.Teleport(destination, transform.rotation);
            inventory.Consume(ItemId.RescueRope);
            LastRescuedPlayer = teammate;
            GameUI.Instance?.ShowNotice($"Rescued {teammate.name}.", 2.5f);
            return true;
        }

        private PlayerController FindDistantTeammate()
        {
            PlayerController farthest = null;
            float farthestSqrDistance = minimumRescueDistance * minimumRescueDistance;
            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController candidate = players[i];
                if (candidate == null || candidate == player || !candidate.isActiveAndEnabled) continue;
                float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance <= farthestSqrDistance) continue;
                farthest = candidate;
                farthestSqrDistance = sqrDistance;
            }
            return farthest;
        }
    }
}
