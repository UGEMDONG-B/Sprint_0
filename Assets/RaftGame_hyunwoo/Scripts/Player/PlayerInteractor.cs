using UnityEngine;
using UnityEngine.InputSystem;

namespace RaftSharkDive
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float interactionDistance = 4f;
        [SerializeField] private LayerMask interactionMask = ~0;

        private PlayerController player;
        private IInteractable current;
        private readonly Collider[] nearbyResults = new Collider[32];

        public IInteractable CurrentInteractable => current;

        private void Awake() => player = GetComponent<PlayerController>();

        private void Update()
        {
            current = FindBestInteractable();
            string prompt = current != null && current.CanInteract(player) ? current.GetInteractionPrompt(player) : string.Empty;
            GameUI.Instance?.SetInteractionPrompt(prompt);
            if (current != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                current.Interact(player);
        }

        public IInteractable FindBestInteractable()
        {
            Transform view = player.CameraPivot;
            if (view == null) return null;
            if (Physics.SphereCast(view.position, 0.24f, view.forward, out RaycastHit hit,
                    interactionDistance, interactionMask, QueryTriggerInteraction.Collide))
            {
                IInteractable aimed = GetInteractable(hit.collider);
                if (aimed != null) return aimed;
            }

            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f, 2.6f,
                nearbyResults, interactionMask, QueryTriggerInteraction.Collide);
            IInteractable closest = null;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider candidateCollider = nearbyResults[i];
                IInteractable candidate = GetInteractable(candidateCollider);
                if (candidate == null || !candidate.CanInteract(player)) continue;
                Vector3 closestPoint = candidateCollider.ClosestPoint(view.position);
                float distance = (closestPoint - view.position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closest = candidate;
            }
            return closest;
        }

        private static IInteractable GetInteractable(Collider target)
        {
            if (target == null) return null;
            foreach (MonoBehaviour component in target.GetComponentsInParent<MonoBehaviour>(true))
                if (component is IInteractable interactable) return interactable;
            return null;
        }
    }
}
