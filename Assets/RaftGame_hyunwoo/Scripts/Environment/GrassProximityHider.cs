using UnityEngine;

namespace RaftSharkDive
{
    public sealed class GrassProximityHider : MonoBehaviour
    {
        [SerializeField, Range(0.5f, 3f)] private float grassHideDistance = 1.25f;

        private Renderer[] renderers;
        private bool hidden;

        public float GrassHideDistance => grassHideDistance;
        public bool IsHidden => hidden;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        public bool ShouldHideAt(Vector3 worldPosition)
        {
            return (worldPosition - transform.position).sqrMagnitude <= grassHideDistance * grassHideDistance;
        }

        public void SetHidden(bool shouldHide)
        {
            if (shouldHide == hidden) return;
            hidden = shouldHide;
            foreach (Renderer grassRenderer in renderers)
                if (grassRenderer != null) grassRenderer.enabled = !hidden;
        }
    }
}
