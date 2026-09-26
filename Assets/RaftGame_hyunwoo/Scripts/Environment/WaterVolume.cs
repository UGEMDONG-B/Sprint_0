using UnityEngine;

namespace RaftSharkDive
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WaterVolume : MonoBehaviour
    {
        private Collider volumeCollider;

        private void Awake()
        {
            volumeCollider = GetComponent<Collider>();
            if (volumeCollider != null) volumeCollider.isTrigger = true;
        }

        public bool Intersects(Bounds worldBounds)
        {
            if (volumeCollider == null) volumeCollider = GetComponent<Collider>();
            return volumeCollider != null && volumeCollider.enabled && volumeCollider.bounds.Intersects(worldBounds);
        }

        private void Reset()
        {
            Collider volume = GetComponent<Collider>();
            if (volume != null) volume.isTrigger = true;
        }
    }
}
