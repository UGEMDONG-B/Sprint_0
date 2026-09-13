using System.Collections.Generic;
using UnityEngine;

namespace RaftSharkDive
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaftUpgradeStation : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(1)] private int woodPerPlank = 1;
        [SerializeField, Range(1, 10)] private int maxExpansionLevel = 5;
        [SerializeField, Min(1)] private int repairWoodCost = 2;
        [SerializeField, Min(1f)] private float maxDurability = 100f;
        [SerializeField, Min(1f)] private float repairAmount = 35f;
        [SerializeField] private Transform raftVisual;
        [SerializeField] private BoxCollider deckCollider;
        [SerializeField] private GameObject extensionLogPrefab;
        [SerializeField] private Material extensionLogMaterial;
        [SerializeField, Min(0.25f)] private float extensionDepth = 1.1f;
        [SerializeField, Min(0.05f)] private float extensionHeight = 0.7f;

        private Vector3 originalVisualScale;
        private Vector3 originalDeckSize;
        private Vector3 originalDeckCenter;
        private Bounds originalVisualBounds;
        private Transform extensionRoot;
        private readonly List<Transform> extensionPlanks = new List<Transform>();
        private bool initialized;

        public bool IsExpanded => ExpansionLevel > 0;
        public int ExpansionLevel { get; private set; }
        public int MaxExpansionLevel => maxExpansionLevel;
        public int WoodPerPlank => woodPerPlank;
        public int RepairWoodCost => repairWoodCost;
        public float RepairAmount => repairAmount;
        public float Durability { get; private set; }
        public float MaxDurability => maxDurability;
        public bool ExtensionPlankVisible => ExpansionLevel > 0 && extensionPlanks.Count >= ExpansionLevel &&
                                             extensionPlanks[ExpansionLevel - 1].gameObject.activeSelf;

        public void Configure(Transform visual, BoxCollider deck, GameObject logPrefab = null,
            Material logMaterial = null)
        {
            raftVisual = visual;
            deckCollider = deck;
            extensionLogPrefab = logPrefab;
            extensionLogMaterial = logMaterial;
        }

        private void Awake()
        {
            EnsureInitialized();
            Durability = maxDurability;
        }

        private void OnEnable()
        {
            EnsureInitialized();
            ApplyExpansionVisual();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                EnsureExtensionPlanks();
                return;
            }
            if (raftVisual == null) raftVisual = transform.Find("RaftVisual_reft");
            if (deckCollider == null) deckCollider = GetComponent<BoxCollider>();
            originalVisualScale = raftVisual != null ? raftVisual.localScale : Vector3.one;
            originalDeckSize = deckCollider != null ? deckCollider.size : new Vector3(8f, 0.35f, 8f);
            originalDeckCenter = deckCollider != null ? deckCollider.center : Vector3.zero;
            if (raftVisual == null || !TryGetLocalRendererBounds(raftVisual, transform, out originalVisualBounds))
                originalVisualBounds = new Bounds(originalDeckCenter, originalDeckSize);
            initialized = true;
            EnsureExtensionPlanks();
        }

        private void EnsureExtensionPlanks()
        {
            Transform legacyPlank = transform.Find("RaftExtensionPlank");
            if (legacyPlank != null) legacyPlank.gameObject.SetActive(false);
            if (extensionRoot == null) extensionRoot = transform.Find("RaftExtensionPlanks");
            if (extensionRoot == null)
            {
                extensionRoot = new GameObject("RaftExtensionPlanks").transform;
                extensionRoot.SetParent(transform, false);
            }

            Renderer source = raftVisual != null ? raftVisual.GetComponentInChildren<Renderer>(true) : null;
            extensionPlanks.Clear();
            for (int i = 0; i < maxExpansionLevel; i++)
            {
                string plankName = $"RaftExtensionPlank_{i + 1:00}";
                Transform plank = extensionRoot.Find(plankName);
                if (plank == null)
                {
                    GameObject plankObject = extensionLogPrefab != null
                        ? Instantiate(extensionLogPrefab)
                        : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    plankObject.name = plankName;
                    plankObject.transform.SetParent(extensionRoot, false);
                    foreach (Collider generatedCollider in plankObject.GetComponentsInChildren<Collider>(true))
                        generatedCollider.enabled = false;
                    Material material = extensionLogMaterial != null
                        ? extensionLogMaterial
                        : source != null ? source.sharedMaterial : null;
                    if (material != null)
                        foreach (Renderer renderer in plankObject.GetComponentsInChildren<Renderer>(true))
                            renderer.sharedMaterial = material;
                    plank = plankObject.transform;
                }

                PositionExtensionLog(plank, i);
                extensionPlanks.Add(plank);
            }
        }

        private void PositionExtensionLog(Transform log, int index)
        {
            bool placeOnLeft = index % 2 == 0;
            int sideIndex = index / 2;
            float side = placeOnLeft ? -1f : 1f;
            Vector3 targetSize = new Vector3(extensionDepth * 0.9f, extensionHeight,
                originalVisualBounds.size.z * 0.98f);

            log.gameObject.SetActive(true);
            log.localPosition = Vector3.zero;
            log.localRotation = Quaternion.Euler(90f, 0f, 0f);
            log.localScale = Vector3.one;

            if (TryGetLocalRendererBounds(log, extensionRoot, out Bounds bounds))
            {
                Vector3 currentSize = bounds.size;
                log.localScale = new Vector3(
                    SafeRatio(targetSize.x, currentSize.x),
                    SafeRatio(targetSize.z, currentSize.z),
                    SafeRatio(targetSize.y, currentSize.y));
            }
            else
            {
                log.localScale = targetSize;
            }

            float attachmentOverlap = 0.08f;
            float visualEdge = placeOnLeft ? originalVisualBounds.min.x : originalVisualBounds.max.x;
            float x = visualEdge + side *
                (extensionDepth * (sideIndex + 0.5f) - attachmentOverlap);
            Vector3 desiredCenter = new Vector3(x,
                originalVisualBounds.max.y - extensionHeight * 0.5f,
                originalVisualBounds.center.z);
            if (TryGetLocalRendererBounds(log, extensionRoot, out bounds))
            {
                log.localPosition += desiredCenter - bounds.center;
            }
            else
            {
                log.localPosition = desiredCenter;
            }

            log.gameObject.SetActive(index < ExpansionLevel);
        }

        private static float SafeRatio(float target, float current)
        {
            return current > 0.0001f ? target / current : 1f;
        }

        private static bool TryGetLocalRendererBounds(Transform rendererRoot, Transform relativeTo,
            out Bounds bounds)
        {
            Renderer[] renderers = rendererRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = default;
            bool hasPoint = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds worldBounds = renderers[i].bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 worldPoint = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 localPoint = relativeTo.InverseTransformPoint(worldPoint);
                    if (!hasPoint)
                    {
                        bounds = new Bounds(localPoint, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localPoint);
                    }
                }
            }
            return true;
        }

        public string GetInteractionPrompt(PlayerController player)
        {
            if (player == null) return string.Empty;
            Inventory inventory = player.GetComponent<Inventory>();
            if (ExpansionLevel < maxExpansionLevel)
                return $"E - Add Raft Plank {ExpansionLevel + 1}/{maxExpansionLevel} (Wood {inventory?.GetCount(ItemId.Wood) ?? 0}/{woodPerPlank})";
            if (Durability < maxDurability - 0.1f)
                return $"E - Repair Raft (Wood {inventory?.GetCount(ItemId.Wood) ?? 0}/{repairWoodCost})  {Durability:0}/{maxDurability:0}";
            return "Raft fully upgraded and repaired";
        }

        public bool CanInteract(PlayerController player) => player != null;

        public void Interact(PlayerController player)
        {
            if (RaftMultiplayerHooks.TryUpgradeRaft != null &&
                RaftMultiplayerHooks.TryUpgradeRaft(this, player))
            {
                return;
            }

            InteractLocal(player);
        }

        public void InteractLocal(PlayerController player)
        {
            Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
            if (inventory == null) return;
            if (ExpansionLevel < maxExpansionLevel)
            {
                if (!inventory.Consume(ItemId.Wood, woodPerPlank))
                {
                    GameUI.Instance?.ShowNotice($"Need Wood x{woodPerPlank} to add a raft plank.", 2.5f);
                    return;
                }
                ApplyState(ExpansionLevel + 1, Durability);
                GameUI.Instance?.ShowNotice($"Raft plank added. Expansion {ExpansionLevel}/{maxExpansionLevel}.", 3f);
                return;
            }
            if (Durability >= maxDurability - 0.1f)
            {
                GameUI.Instance?.ShowNotice("Raft is already fully repaired.", 2f);
                return;
            }
            if (!inventory.Consume(ItemId.Wood, repairWoodCost))
            {
                GameUI.Instance?.ShowNotice($"Need Wood x{repairWoodCost} to repair the raft.", 2.5f);
                return;
            }
            Durability = Mathf.Min(maxDurability, Durability + repairAmount);
            GameUI.Instance?.ShowNotice($"Raft repaired: {Durability:0}/{maxDurability:0}", 2.5f);
        }

        public void Damage(float amount)
        {
            if (amount <= 0f) return;
            Durability = Mathf.Max(0f, Durability - amount);
            GameUI.Instance?.ShowNotice(Durability > 0f
                ? $"Shark damaged the raft! {Durability:0}/{maxDurability:0}"
                : "Raft critically damaged! Collect seabed Wood and repair it.", 3f);
        }

        public void ApplyState(bool expanded, float durability)
        {
            ApplyState(expanded ? 1 : 0, durability);
        }

        public void ApplyState(int expansionLevel, float durability)
        {
            EnsureInitialized();
            ExpansionLevel = Mathf.Clamp(expansionLevel, 0, maxExpansionLevel);
            Durability = Mathf.Clamp(durability, 0f, maxDurability);
            ApplyExpansionVisual();
        }

        private void ApplyExpansionVisual()
        {
            if (raftVisual != null) raftVisual.localScale = originalVisualScale;
            for (int i = 0; i < extensionPlanks.Count; i++)
                extensionPlanks[i].gameObject.SetActive(i < ExpansionLevel);
            if (deckCollider != null)
            {
                int leftLogs = (ExpansionLevel + 1) / 2;
                int rightLogs = ExpansionLevel / 2;
                float addedWidth = extensionDepth * (leftLogs + rightLogs);
                float centerShift = extensionDepth * (rightLogs - leftLogs) * 0.5f;
                deckCollider.size = new Vector3(originalDeckSize.x + addedWidth, originalDeckSize.y,
                    originalDeckSize.z);
                deckCollider.center = originalDeckCenter + Vector3.right * centerShift;
            }
            GetComponent<RaftAutoMover>()?.RefreshBoardingRamps();
        }
    }
}
