namespace RaftSharkDive
{
    public interface IInteractable
    {
        string GetInteractionPrompt(PlayerController player);
        bool CanInteract(PlayerController player);
        void Interact(PlayerController player);
    }
}
