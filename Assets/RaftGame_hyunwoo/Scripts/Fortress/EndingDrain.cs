using UnityEngine;

namespace RaftSharkDive
{
    public sealed class EndingDrain : MonoBehaviour, IInteractable
    {
        private bool used;
        public bool Used => used;

        public string GetInteractionPrompt(PlayerController player) => used
            ? string.Empty
            : "Aim at the orange plug and press E - Pull Ocean Plug";
        public bool CanInteract(PlayerController player) => !used && player != null;

        public void Interact(PlayerController player)
        {
            if (used) return;
            if (GameManager.Instance != null && GameManager.Instance.Stage < GameStage.Fortress)
            {
                GameUI.Instance?.ShowNotice("Open the fortress door first.", 2f);
                return;
            }
            if (RaftMultiplayerHooks.TryDrainOcean != null &&
                RaftMultiplayerHooks.TryDrainOcean(this, player))
            {
                return;
            }
            CompleteLocal();
        }

        public void CompleteLocal()
        {
            if (used) return;
            used = true;
            GameManager.Instance?.CompleteEndingLocal();
        }

        public void ApplyUsedState(bool isUsed)
        {
            if (isUsed) CompleteLocal();
        }
    }
}
