using UnityEngine;

namespace RaftSharkDive
{
    public sealed class UnderwaterWoodSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject woodPickupPrefab;
        [SerializeField, Range(10, 20)] private int spawnCount = 15;
        [SerializeField] private float seaFloorY = -15.45f;
        [SerializeField, Min(5f)] private float innerRadius = 24f;
        [SerializeField, Min(10f)] private float outerRadius = 72f;

        public int SpawnCount => spawnCount;
        public GameObject WoodPickupPrefab => woodPickupPrefab;

        public void Configure(GameObject prefab, int count, float floorY)
        {
            woodPickupPrefab = prefab;
            spawnCount = Mathf.Clamp(count, 10, 20);
            seaFloorY = floorY;
        }

        private void Start()
        {
            int currentCount = GetComponentsInChildren<PickupItem>(true).Length;
            for (int i = currentCount; i < spawnCount; i++)
            {
                GameObject instance = Instantiate(woodPickupPrefab, transform);
                instance.name = $"UnderwaterWood_{i + 1:00}";
                PlaceInstance(instance, i);
            }
            for (int i = currentCount - 1; i >= spawnCount; i--)
                Destroy(transform.GetChild(i).gameObject);
        }

        public void PlaceInstance(GameObject instance, int index)
        {
            float t = spawnCount <= 1 ? 0f : index / (float)(spawnCount - 1);
            float angle = index * 137.50776f * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(innerRadius, outerRadius, Mathf.Repeat(t * 2.37f, 1f));
            float tilt = Mathf.Sin(index * 12.9898f) * 9f;
            instance.transform.SetParent(transform, false);
            instance.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, seaFloorY,
                Mathf.Sin(angle) * radius);
            instance.transform.localRotation = Quaternion.Euler(tilt, index * 137.50776f, 90f + Mathf.Cos(index * 4.37f) * 8f);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            instance.transform.position += Vector3.up * (seaFloorY - bounds.min.y + 0.04f);
            instance.GetComponent<PickupItem>()?.ConfigureUnderwater();
            FitInteractionCollider(instance, renderers);
        }

        private static void FitInteractionCollider(GameObject instance, Renderer[] renderers)
        {
            BoxCollider interaction = instance.GetComponent<BoxCollider>();
            if (interaction == null) interaction = instance.AddComponent<BoxCollider>();
            bool initialized = false;
            Bounds localBounds = default;
            foreach (Renderer renderer in renderers)
            {
                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z);
                            Vector3 local = instance.transform.InverseTransformPoint(corner);
                            if (!initialized) { localBounds = new Bounds(local, Vector3.zero); initialized = true; }
                            else localBounds.Encapsulate(local);
                        }
            }
            interaction.center = localBounds.center;
            interaction.size = localBounds.size + new Vector3(0.8f, 0.8f, 0.8f);
            interaction.isTrigger = true;
            interaction.enabled = true;
        }
    }
}
