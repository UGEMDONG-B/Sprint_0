using UnityEngine;

namespace RaftSharkDive
{
    [RequireComponent(typeof(Collider))]
    public sealed class IslandSafeZone : MonoBehaviour
    {
        private bool permanentlyDisabled;

        private void OnTriggerExit(Collider other)
        {
            if (permanentlyDisabled || other.GetComponentInParent<PlayerController>() == null) return;
            GameManager.Instance?.PlayerLeftIsland();
        }

        public void DisablePermanently()
        {
            if (permanentlyDisabled) return;
            permanentlyDisabled = true;
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null) zoneCollider.enabled = false;
        }
    }
}
