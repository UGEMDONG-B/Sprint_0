using UnityEngine;
using UnityEngine.InputSystem;

namespace RaftSharkDive
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(Inventory))]
    public sealed class HeldItemController : MonoBehaviour
    {
        [SerializeField] private Vector3 heldLocalPosition = new Vector3(0.42f, -0.34f, 0.72f);
        [SerializeField] private Vector3 heldLocalEuler = new Vector3(8f, -18f, -8f);
        [SerializeField, Min(0.1f)] private float heldVisualSize = 0.58f;
        [SerializeField, Min(0.1f)] private float minimumHoldTime = 0.3f;
        [SerializeField, Min(0.2f)] private float fullChargeTime = 1.5f;
        [SerializeField, Min(1f)] private float minimumThrowSpeed = 6f;
        [SerializeField, Min(1f)] private float maximumThrowSpeed = 15f;
        [SerializeField, Min(1f)] private float palmHealAmount = 25f;

        private PlayerController player;
        private Inventory inventory;
        private Transform handAnchor;
        private GameObject heldVisual;
        private ItemId? displayedItem;
        private float chargeStartedAt;

        public bool IsCharging { get; private set; }
        public float ThrowCharge => IsCharging
            ? Mathf.Clamp01((Time.time - chargeStartedAt) / fullChargeTime)
            : 0f;
        public ItemId? DisplayedItem => displayedItem;
        public GameObject LastReleasedObject { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<Inventory>();
        }

        private void Start()
        {
            EnsureHandAnchor();
            inventory.Changed += RefreshHeldItem;
            RefreshHeldItem();
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.Changed -= RefreshHeldItem;
        }

        private void Update()
        {
            if (!IsLocallyControlled() || Keyboard.current == null) return;
            HandleSlotSelection();
            if (Keyboard.current.fKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
                TryUseSelectedItem();

            if (Keyboard.current.qKey.wasPressedThisFrame && inventory.SelectedItem.HasValue &&
                Cursor.lockState == CursorLockMode.Locked)
            {
                chargeStartedAt = Time.time;
                IsCharging = true;
            }

            if (!IsCharging || !Keyboard.current.qKey.wasReleasedThisFrame) return;
            float heldDuration = Time.time - chargeStartedAt;
            float charge = Mathf.Clamp01(heldDuration / fullChargeTime);
            IsCharging = false;
            if (heldDuration < minimumHoldTime)
            {
                DropSelected();
                return;
            }
            ThrowSelected(charge);
        }

        public bool ThrowSelected(float charge) => ReleaseSelected(charge, false);

        public bool DropSelected() => ReleaseSelected(0f, true);

        private bool ReleaseSelected(float charge, bool gentleDrop)
        {
            if (RaftMultiplayerHooks.TryReleaseHeldItem != null &&
                RaftMultiplayerHooks.TryReleaseHeldItem(this, charge, gentleDrop)) return true;
            return ReleaseSelectedLocal(charge, gentleDrop);
        }

        public bool ReleaseSelectedLocal(float charge, bool gentleDrop)
        {
            ItemId? selected = inventory != null ? inventory.SelectedItem : null;
            if (!selected.HasValue || heldVisual == null || handAnchor == null) return false;

            ItemId item = selected.Value;
            float speed = gentleDrop
                ? 0.8f
                : Mathf.Lerp(minimumThrowSpeed, maximumThrowSpeed, Mathf.Clamp01(charge));
            Vector3 velocity = handAnchor.forward * speed + Vector3.up * (gentleDrop ? 0.1f : 1.5f);
            GameObject dropped = CreateDroppedReplica(item,
                handAnchor.position + handAnchor.forward * 0.45f, handAnchor.rotation, velocity, true);
            if (dropped == null) return false;

            CharacterController character = GetComponent<CharacterController>();
            SphereCollider collider = dropped.GetComponent<SphereCollider>();
            if (character != null && collider != null) Physics.IgnoreCollision(collider, character, true);
            if (!inventory.ConsumeSelected(out ItemId consumed) || consumed != item)
            {
                Destroy(dropped);
                return false;
            }

            LastReleasedObject = dropped;
            GameUI.Instance?.ShowNotice(gentleDrop ? $"Dropped {item}." : $"Threw {item}.", 1.5f);
            return true;
        }

        public GameObject CreateDroppedReplica(ItemId item, Vector3 position, Quaternion rotation,
            Vector3 velocity, bool simulatePhysics)
        {
            GameObject dropped = new GameObject($"Thrown_{item}");
            dropped.transform.SetPositionAndRotation(position, rotation);

            GameObject visual = new GameObject("DroppedVisual");
            visual.transform.SetParent(dropped.transform, false);
            GameObject source = FindVisualSource(item);
            if (source != null)
            {
                GameObject model = Instantiate(source, visual.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                SetLayerRecursively(model, 0);
                foreach (Collider childCollider in model.GetComponentsInChildren<Collider>(true))
                    childCollider.enabled = false;
            }
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = "Model";
                fallback.transform.SetParent(visual.transform, false);
                Destroy(fallback.GetComponent<Collider>());
            }
            CenterAndScaleVisual(visual, heldVisualSize * 1.15f);

            SphereCollider collider = dropped.AddComponent<SphereCollider>();
            collider.radius = 0.38f;
            collider.isTrigger = false;
            Rigidbody body = dropped.AddComponent<Rigidbody>();
            body.mass = 0.8f;
            body.linearDamping = 0.45f;
            body.angularDamping = 1.5f;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.isKinematic = !simulatePhysics;
            if (simulatePhysics) body.linearVelocity = velocity;

            PickupItem pickup = dropped.AddComponent<PickupItem>();
            pickup.Configure(item, 1);
            pickup.ConfigureThrown();
            dropped.AddComponent<ThrownPickupMotion>();
            return dropped;
        }

        public bool TryEatSelectedPalm()
        {
            if (inventory == null || inventory.SelectedItem != ItemId.Palm) return false;
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health == null || health.Current >= health.Max - 0.01f)
            {
                GameUI.Instance?.ShowNotice("Health is already full.", 1.5f);
                return false;
            }
            float restored = health.Heal(palmHealAmount);
            if (restored <= 0f || !inventory.ConsumeSelected(out ItemId consumed) || consumed != ItemId.Palm)
                return false;
            GameUI.Instance?.ShowNotice($"Ate Palm. Health +{restored:0}.", 2f);
            return true;
        }

        public bool TryUseSelectedItem()
        {
            if (inventory == null || !inventory.SelectedItem.HasValue) return false;
            if (inventory.SelectedItem == ItemId.Palm) return TryEatSelectedPalm();
            if (inventory.SelectedItem != ItemId.RaftStorageBox) return false;

            RaftUpgradeStation raft = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            if (raft == null || !raft.gameObject.activeInHierarchy)
            {
                GameUI.Instance?.ShowNotice("The storage box can only be installed on the built raft.", 2f);
                return false;
            }

            Transform view = player.CameraPivot;
            Vector3 desiredPosition = transform.position + transform.forward * 1.4f;
            RaycastHit hit = default;
            bool aimedAtRaft = view != null && Physics.Raycast(view.position, view.forward, out hit,
                5f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<RaftAutoMover>() != null;
            if (aimedAtRaft) desiredPosition = hit.point;

            BoxCollider deck = raft.GetComponent<BoxCollider>();
            Vector3 localPlayer = raft.transform.InverseTransformPoint(transform.position);
            bool standingNearDeck = deck != null &&
                                    localPlayer.x >= deck.center.x - deck.size.x * 0.5f - 0.6f &&
                                    localPlayer.x <= deck.center.x + deck.size.x * 0.5f + 0.6f &&
                                    localPlayer.z >= deck.center.z - deck.size.z * 0.5f - 0.6f &&
                                    localPlayer.z <= deck.center.z + deck.size.z * 0.5f + 0.6f;
            if (!aimedAtRaft && !standingNearDeck)
            {
                GameUI.Instance?.ShowNotice("Stand on the raft or aim at its deck to install storage.", 2f);
                return false;
            }
            return InstallSelectedStorage(raft.transform, desiredPosition);
        }

        public bool InstallSelectedStorage(Transform raft, Vector3 worldPosition)
        {
            if (RaftMultiplayerHooks.TryInstallStorage != null &&
                RaftMultiplayerHooks.TryInstallStorage(this, raft, worldPosition)) return true;
            return InstallSelectedStorageLocal(raft, worldPosition);
        }

        public bool InstallSelectedStorageLocal(Transform raft, Vector3 worldPosition)
        {
            if (raft == null || inventory == null || inventory.SelectedItem != ItemId.RaftStorageBox ||
                RaftStorageBox.FindExisting() != null) return false;
            RaftStorageBox storage = RaftStorageBox.CreateOnRaft(raft, worldPosition);
            if (storage == null) return false;
            if (!inventory.ConsumeSelected(out ItemId consumed) || consumed != ItemId.RaftStorageBox)
            {
                Destroy(storage.gameObject);
                return false;
            }
            GameUI.Instance?.ShowNotice("Raft Storage installed. Press E to open it.", 3f);
            return true;
        }

        private void HandleSlotSelection()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard.digit1Key.wasPressedThisFrame) inventory.SelectSlot(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) inventory.SelectSlot(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) inventory.SelectSlot(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) inventory.SelectSlot(3);
            else if (keyboard.digit5Key.wasPressedThisFrame) inventory.SelectSlot(4);
        }

        private bool IsLocallyControlled()
        {
            return player != null && player.CameraPivot != null && player.CameraPivot.IsChildOf(transform);
        }

        private void EnsureHandAnchor()
        {
            if (handAnchor != null) return;
            Transform pivot = player != null ? player.CameraPivot : null;
            if (pivot == null) return;
            Transform existing = pivot.Find("HeldItemAnchor");
            handAnchor = existing != null ? existing : new GameObject("HeldItemAnchor").transform;
            handAnchor.SetParent(pivot, false);
            handAnchor.localPosition = heldLocalPosition;
            handAnchor.localRotation = Quaternion.Euler(heldLocalEuler);
        }

        private void RefreshHeldItem()
        {
            EnsureHandAnchor();
            ItemId? selected = inventory != null ? inventory.SelectedItem : null;
            if (selected == displayedItem && heldVisual != null) return;
            if (heldVisual != null) Destroy(heldVisual);
            heldVisual = null;
            displayedItem = selected;
            if (!selected.HasValue || handAnchor == null) return;

            GameObject source = FindVisualSource(selected.Value);
            heldVisual = new GameObject($"Held_{selected.Value}");
            heldVisual.transform.SetParent(handAnchor, false);
            if (source != null)
            {
                GameObject model = Instantiate(source, heldVisual.transform);
                model.name = "Model";
                model.transform.localPosition = Vector3.zero;
                SetLayerRecursively(model, 0);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            }
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.name = "Model";
                fallback.transform.SetParent(heldVisual.transform, false);
                Destroy(fallback.GetComponent<Collider>());
                if (selected.Value == ItemId.RaftStorageBox)
                {
                    Renderer renderer = fallback.GetComponent<Renderer>();
                    if (renderer != null) renderer.material.color = new Color(0.36f, 0.18f, 0.06f);
                }
            }
            CenterAndScaleHeldVisual();
        }

        private GameObject FindVisualSource(ItemId item)
        {
            if (item == ItemId.OxygenTank)
            {
                CraftingSystem crafting = GetComponent<CraftingSystem>();
                Transform tank = crafting != null && crafting.EquippedTankVisual != null
                    ? crafting.EquippedTankVisual.transform
                    : null;
                if (tank != null && tank.childCount > 0) return tank.GetChild(0).gameObject;
            }

            ItemId sourceItem = item == ItemId.RescueRope ? ItemId.Rope : item;
            foreach (PickupItem pickup in FindObjectsByType<PickupItem>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (pickup.Item != sourceItem || !pickup.gameObject.scene.IsValid()) continue;
                Transform original = pickup.transform.Find("PickupOriginal");
                if (original != null) return original.gameObject;
                for (int i = 0; i < pickup.transform.childCount; i++)
                {
                    Transform child = pickup.transform.GetChild(i);
                    if (child.name == "PickupOutline") continue;
                    if (child.GetComponentInChildren<Renderer>(true) != null) return child.gameObject;
                }
            }
            return null;
        }

        private void CenterAndScaleHeldVisual()
        {
            CenterAndScaleVisual(heldVisual, heldVisualSize);
        }

        private static void CenterAndScaleVisual(GameObject visualRoot, float targetSize)
        {
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > 0.0001f) visualRoot.transform.localScale *= targetSize / largest;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 localCenter = visualRoot.transform.InverseTransformPoint(bounds.center);
            if (visualRoot.transform.childCount > 0)
                visualRoot.transform.GetChild(0).localPosition -= localCenter;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
