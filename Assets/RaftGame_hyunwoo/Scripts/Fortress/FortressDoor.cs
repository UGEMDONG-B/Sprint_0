using System.Collections;
using UnityEngine;

namespace RaftSharkDive
{
    public sealed class FortressDoor : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1f)] private float openHeight = 5f;
        [SerializeField, Min(0.1f)] private float openDuration = 1.2f;
        [SerializeField] private bool requiresOxygenTank = true;
        private bool open;
        public bool IsOpen => open;

        public string GetInteractionPrompt(PlayerController player) => open ? string.Empty : "E - Open Fortress Door";
        public bool CanInteract(PlayerController player) => !open && player != null;

        public void Interact(PlayerController player)
        {
            if (open) return;
            if (requiresOxygenTank && player.GetComponent<Inventory>()?.Has(ItemId.OxygenTank) != true)
            {
                GameUI.Instance?.ShowNotice("An Oxygen Tank is required for the deep fortress.", 3f);
                return;
            }
            if (RaftMultiplayerHooks.TryOpenFortressDoor != null &&
                RaftMultiplayerHooks.TryOpenFortressDoor(this, player))
            {
                return;
            }
            OpenLocal();
        }

        public void OpenLocal()
        {
            if (open) return;
            open = true;
            GameManager.Instance?.EnterFortress();
            StartCoroutine(OpenRoutine());
        }

        public void ApplyOpenState(bool isOpen)
        {
            if (isOpen) OpenLocal();
        }

        private IEnumerator OpenRoutine()
        {
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.up * openHeight;
            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / openDuration));
                yield return null;
            }
            transform.position = end;
        }
    }
}
