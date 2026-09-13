using System.Collections.Generic;
using UnityEngine;

namespace RaftSharkDive
{
    public sealed class GrassVisibilityManager : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float cellSize = 4f;

        private readonly Dictionary<Vector2Int, List<GrassProximityHider>> cells =
            new Dictionary<Vector2Int, List<GrassProximityHider>>();
        private HashSet<GrassProximityHider> hidden = new HashSet<GrassProximityHider>();
        private HashSet<GrassProximityHider> nextHidden = new HashSet<GrassProximityHider>();
        private PlayerController player;

        public int RegisteredGrassCount { get; private set; }
        public int LastCandidateChecks { get; private set; }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            RebuildSpatialIndex();
        }

        public void RebuildSpatialIndex()
        {
            cells.Clear();
            GrassProximityHider[] patches = FindObjectsByType<GrassProximityHider>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (GrassProximityHider patch in patches)
            {
                Vector2Int cell = GetCell(patch.transform.position);
                if (!cells.TryGetValue(cell, out List<GrassProximityHider> bucket))
                {
                    bucket = new List<GrassProximityHider>();
                    cells.Add(cell, bucket);
                }
                bucket.Add(patch);
            }
            RegisteredGrassCount = patches.Length;
        }

        private void Update()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;

            nextHidden.Clear();
            LastCandidateChecks = 0;
            Vector2Int center = GetCell(player.transform.position);
            const int cellReach = 1;
            for (int x = -cellReach; x <= cellReach; x++)
            {
                for (int y = -cellReach; y <= cellReach; y++)
                {
                    if (!cells.TryGetValue(center + new Vector2Int(x, y), out List<GrassProximityHider> bucket))
                        continue;
                    foreach (GrassProximityHider patch in bucket)
                    {
                        if (patch == null || !patch.isActiveAndEnabled) continue;
                        LastCandidateChecks++;
                        if (!patch.ShouldHideAt(player.transform.position)) continue;
                        nextHidden.Add(patch);
                    }
                }
            }

            foreach (GrassProximityHider patch in hidden)
                if (patch != null && !nextHidden.Contains(patch)) patch.SetHidden(false);
            foreach (GrassProximityHider patch in nextHidden)
                if (patch != null && !hidden.Contains(patch)) patch.SetHidden(true);

            HashSet<GrassProximityHider> swap = hidden;
            hidden = nextHidden;
            nextHidden = swap;
        }

        private void OnDisable()
        {
            foreach (GrassProximityHider patch in hidden)
                if (patch != null) patch.SetHidden(false);
            hidden.Clear();
            nextHidden.Clear();
        }

        private Vector2Int GetCell(Vector3 worldPosition)
        {
            return new Vector2Int(Mathf.FloorToInt(worldPosition.x / cellSize),
                Mathf.FloorToInt(worldPosition.z / cellSize));
        }
    }
}
