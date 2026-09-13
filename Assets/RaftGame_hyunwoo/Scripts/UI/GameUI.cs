using UnityEngine;
using UnityEngine.InputSystem;

namespace RaftSharkDive
{
    [DefaultExecutionOrder(-50)]
    public sealed class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        private PlayerHealth health;
        private PlayerController player;
        private PlayerOxygen oxygen;
        private Inventory inventory;
        private CraftingSystem crafting;
        private HeldItemController heldItems;
        private RaftUpgradeStation raftUpgrade;
        private RaftBuildPoint raftBuildPoint;
        private RaftAutoMover raftMover;
        private EndlessOceanItemSpawner oceanItems;
        private SharkAI shark;
        private string interactionPrompt = string.Empty;
        private string notice = string.Empty;
        private float noticeUntil;
        private bool inventoryOpen;
        private bool storageOpen;
        private RaftStorageBox openStorage;
        private bool ending;

        private GUIStyle titleStyle;
        private GUIStyle centerStyle;
        private GUIStyle labelStyle;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
            health = player.GetComponent<PlayerHealth>();
            oxygen = player.GetComponent<PlayerOxygen>();
            inventory = player.GetComponent<Inventory>();
            crafting = player.GetComponent<CraftingSystem>();
            heldItems = player.GetComponent<HeldItemController>();
            raftUpgrade = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            raftBuildPoint = FindFirstObjectByType<RaftBuildPoint>(FindObjectsInactive.Include);
            raftMover = raftUpgrade != null ? raftUpgrade.GetComponent<RaftAutoMover>() : null;
            oceanItems = FindFirstObjectByType<EndlessOceanItemSpawner>();
            shark = FindFirstObjectByType<SharkAI>();
        }

        private void Update()
        {
            if (Keyboard.current == null || ending) return;
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                storageOpen = false;
                openStorage = null;
                SetInventoryOpen(!inventoryOpen);
            }
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (inventoryOpen || storageOpen)
                {
                    storageOpen = false;
                    openStorage = null;
                    SetInventoryOpen(false);
                }
                else
                {
                    bool locked = Cursor.lockState == CursorLockMode.Locked;
                    Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                    Cursor.visible = locked;
                }
            }
        }

        private void SetInventoryOpen(bool value)
        {
            inventoryOpen = value;
            if (value)
            {
                storageOpen = false;
                openStorage = null;
            }
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }

        public void OpenStorage(RaftStorageBox storage)
        {
            if (storage == null) return;
            openStorage = storage;
            storageOpen = true;
            inventoryOpen = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SetInteractionPrompt(string prompt) => interactionPrompt = prompt ?? string.Empty;

        public void ShowNotice(string message, float seconds)
        {
            notice = message;
            noticeUntil = Time.time + seconds;
        }

        public void ShowEnding()
        {
            ending = true;
            inventoryOpen = false;
            storageOpen = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void EnsureStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            centerStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHud();
            DrawHotbar();
            if (inventoryOpen) DrawInventory();
            if (storageOpen && openStorage != null) DrawStorage();
            if (ending) DrawEnding();
        }

        private void DrawHud()
        {
            bool stageZero = GameManager.Instance?.Stage == GameStage.IslandLoot;
            if (stageZero)
            {
                GUI.Box(new Rect(18, 18, 230, 122), "SINKING ISLAND");
                GUI.Label(new Rect(32, 48, 200, 26), $"TIME: {GameManager.Instance.LootTimeRemaining:00}", labelStyle);
                int depositedWood = raftBuildPoint != null ? raftBuildPoint.DepositedWood : 0;
                int depositedRope = raftBuildPoint != null ? raftBuildPoint.DepositedRope : 0;
                GUI.Label(new Rect(32, 76, 200, 26),
                    $"WOOD: {depositedWood}+{inventory?.GetCount(ItemId.Wood) ?? 0} / {GameManager.Instance.Stage0WoodRequired}", labelStyle);
                GUI.Label(new Rect(32, 104, 200, 26),
                    $"ROPE: {depositedRope}+{inventory?.GetCount(ItemId.Rope) ?? 0} / {GameManager.Instance.Stage0RopeRequired}", labelStyle);
            }
            else
            {
                GUI.Box(new Rect(18, 18, 330, 244), "SURVIVAL STATUS");
                if (health != null) GUI.Label(new Rect(32, 48, 210, 24), $"Health  {health.Current:0} / {health.Max:0}", labelStyle);
                if (oxygen != null) GUI.Label(new Rect(32, 74, 210, 24), $"Oxygen {oxygen.Current:0} / {oxygen.Max:0}", labelStyle);
                if (GameManager.Instance != null)
                    GUI.Label(new Rect(32, 100, 210, 24), $"Stage: {GameManager.Instance.Stage}", labelStyle);
                if (raftUpgrade != null)
                    GUI.Label(new Rect(32, 126, 300, 24),
                        $"Raft: {raftUpgrade.Durability:0}/{raftUpgrade.MaxDurability:0}  Planks {raftUpgrade.ExpansionLevel}/{raftUpgrade.MaxExpansionLevel}", labelStyle);
                GUI.Label(new Rect(32, 152, 300, 24), $"Voyage: {raftMover?.DistanceTravelled ?? 0f:0}m", labelStyle);
                float raftDistance = player != null && raftUpgrade != null
                    ? Vector3.Distance(player.transform.position, raftUpgrade.transform.position) : 0f;
                GUI.Label(new Rect(32, 178, 300, 24), $"Raft distance: {raftDistance:0}m", labelStyle);
                GUI.Label(new Rect(32, 204, 300, 24), GetNearestItemStatus(), labelStyle);
                GUI.Label(new Rect(32, 230, 300, 24), GetSharkStatus(), labelStyle);
            }

            GUI.Label(new Rect(Screen.width / 2f - 10, Screen.height / 2f - 14, 20, 28), "+", centerStyle);
            if (!string.IsNullOrEmpty(interactionPrompt))
                GUI.Box(new Rect(Screen.width / 2f - 160, Screen.height - 132, 320, 36), interactionPrompt);
            if (Time.time < noticeUntil)
                GUI.Box(new Rect(Screen.width / 2f - 260, 76, 520, 40), notice);
            if (GameManager.Instance?.Stage == GameStage.Fortress)
                GUI.Box(new Rect(Screen.width / 2f - 285, 22, 570, 42),
                    "OBJECTIVE: Find the orange ocean plug, aim at it, and press E to pull it");
            string controls = stageZero
                ? "WASD Move | Shift Run\nMouse Look | Space Jump\nE Pick Up / Build"
                : "WASD Move | Mouse Look\nSpace Up/Jump | Ctrl Dive\nE Interact | R Rescue Rope\nTab Craft | F5 Save | F9 Load";
            controls += "\n1-5 Select | Tap Q Drop / Hold Q Throw\nF Use / Eat / Install";
            GUI.Label(new Rect(Screen.width - 275, 18, 255, 135), controls, labelStyle);
        }

        private void DrawHotbar()
        {
            if (inventory == null) return;
            const float slotWidth = 108f;
            const float gap = 5f;
            const float slotHeight = 58f;
            float totalWidth = inventory.MaxSlots * slotWidth + (inventory.MaxSlots - 1) * gap;
            float x = (Screen.width - totalWidth) * 0.5f;
            float y = Screen.height - slotHeight - 12f;
            for (int i = 0; i < inventory.MaxSlots; i++)
            {
                bool selected = inventory.SelectedSlotIndex == i;
                Rect rect = new Rect(x + i * (slotWidth + gap), y, slotWidth, slotHeight);
                GUI.Box(rect, selected ? $"> {i + 1} <" : $"{i + 1}");
                ItemId? item = inventory.GetSlotItem(i);
                string content = item.HasValue ? item.Value.ToString() : "EMPTY";
                GUI.Label(new Rect(rect.x + 4f, rect.y + 25f, rect.width - 8f, 24f), content, centerStyle);
            }

            if (heldItems != null && heldItems.IsCharging)
            {
                float width = totalWidth * heldItems.ThrowCharge;
                GUI.Box(new Rect(x, y - 23f, totalWidth, 18f), string.Empty);
                GUI.Box(new Rect(x + 2f, y - 21f, Mathf.Max(0f, width - 4f), 14f), "THROW");
            }
        }

        private void DrawInventory()
        {
            const float panelHeight = 620f;
            Rect panel = new Rect(Screen.width / 2f - 230, Screen.height / 2f - panelHeight * 0.5f, 460, panelHeight);
            GUI.Box(panel, "INVENTORY & CRAFTING");
            for (int i = 0; i < inventory.MaxSlots; i++)
            {
                ItemId? item = inventory.GetSlotItem(i);
                string content = item.HasValue ? item.Value.ToString() : "Empty";
                GUI.Label(new Rect(panel.x + 28 + (i % 2) * 205, panel.y + 42 + (i / 2) * 27,
                    195, 26), $"[{i + 1}] {content}", labelStyle);
            }
            float y = panel.y + 158;
            GUI.Label(new Rect(panel.x + 28, y, 380, 30), "OXYGEN TANK RECIPE", labelStyle);
            y += 34;
            GUI.Label(new Rect(panel.x + 28, y, 380, 56),
                $"Scrap {crafting?.GetAvailableCount(ItemId.Scrap) ?? 0}/{crafting?.ScrapRequired ?? 3}   " +
                $"Rubber {crafting?.GetAvailableCount(ItemId.Rubber) ?? 0}/{crafting?.RubberRequired ?? 2}   " +
                $"Filter {crafting?.GetAvailableCount(ItemId.Filter) ?? 0}/{crafting?.FilterRequired ?? 1}", labelStyle);
            y += 62;
            GUI.enabled = crafting != null && crafting.CanCraftTank;
            if (GUI.Button(new Rect(panel.x + 28, y, 384, 45), "CRAFT OXYGEN TANK")) crafting.TryCraftOxygenTank();
            GUI.enabled = true;
            y += 60;
            GUI.Label(new Rect(panel.x + 28, y, 380, 30), "RESCUE ROPE RECIPE", labelStyle);
            y += 32;
            GUI.Label(new Rect(panel.x + 28, y, 380, 28),
                $"Rope {crafting?.GetAvailableCount(ItemId.Rope) ?? 0}/{crafting?.RescueRopeCost ?? 2}  |  Pulls a teammate from 15m+", labelStyle);
            y += 34;
            GUI.enabled = crafting != null && crafting.CanCraftRescueRope;
            if (GUI.Button(new Rect(panel.x + 28, y, 384, 42), "CRAFT RESCUE ROPE")) crafting.TryCraftRescueRope();
            GUI.enabled = true;
            y += 54;
            GUI.Label(new Rect(panel.x + 28, y, 380, 28), "RAFT STORAGE RECIPE", labelStyle);
            y += 30;
            GUI.Label(new Rect(panel.x + 28, y, 380, 26),
                $"Wood {inventory?.GetCount(ItemId.Wood) ?? 0}/{crafting?.StorageBoxWoodCost ?? 5}  |  Capacity 10", labelStyle);
            y += 32;
            GUI.enabled = crafting != null && crafting.CanCraftStorageBox;
            if (GUI.Button(new Rect(panel.x + 28, y, 384, 42), "CRAFT STORAGE ITEM (then select + F)"))
                crafting.TryCraftStorageBox();
            GUI.enabled = true;
            GUI.Label(new Rect(panel.x + 28, panel.yMax - 38, 384, 24), "Press Tab to close", centerStyle);
        }

        private void DrawStorage()
        {
            Rect panel = new Rect(Screen.width * 0.5f - 360f, Screen.height * 0.5f - 245f, 720f, 490f);
            GUI.Box(panel, "RAFT STORAGE - click an item to transfer");
            GUI.Label(new Rect(panel.x + 28f, panel.y + 38f, 300f, 28f),
                $"PLAYER  {inventory.OccupiedSlotCount}/{inventory.MaxSlots}", labelStyle);
            GUI.Label(new Rect(panel.x + 390f, panel.y + 38f, 300f, 28f),
                $"STORAGE  {openStorage.Count}/{openStorage.Capacity}", labelStyle);

            for (int i = 0; i < inventory.MaxSlots; i++)
            {
                ItemId? item = inventory.GetSlotItem(i);
                GUI.enabled = item.HasValue && openStorage.Count < openStorage.Capacity;
                if (GUI.Button(new Rect(panel.x + 28f, panel.y + 74f + i * 58f, 285f, 46f),
                        item.HasValue ? $"{i + 1}. {item.Value}  >" : $"{i + 1}. Empty"))
                    openStorage.TryStoreFrom(inventory, i);
            }

            for (int i = 0; i < openStorage.Capacity; i++)
            {
                ItemId? item = openStorage.GetSlotItem(i);
                int column = i / 5;
                int row = i % 5;
                GUI.enabled = item.HasValue && inventory.OccupiedSlotCount < inventory.MaxSlots;
                if (GUI.Button(new Rect(panel.x + 390f + column * 150f, panel.y + 74f + row * 58f,
                        140f, 46f), item.HasValue ? $"< {item.Value}" : "Empty"))
                    openStorage.TryTakeTo(inventory, i);
            }
            GUI.enabled = true;
            GUI.Label(new Rect(panel.x + 25f, panel.yMax - 42f, panel.width - 50f, 26f),
                "Esc to close", centerStyle);
        }

        private string GetNearestItemStatus()
        {
            if (player == null || oceanItems == null ||
                !oceanItems.TryGetNearestPickup(player.transform.position, out ItemId item, out Vector3 position))
                return "Nearest loot: scanning...";
            Vector3 direction = position - player.transform.position;
            direction.y = 0f;
            float angle = Vector3.SignedAngle(player.transform.forward, direction, Vector3.up);
            string side = Mathf.Abs(angle) < 35f ? "AHEAD" : Mathf.Abs(angle) > 145f ? "BEHIND" : angle < 0f ? "LEFT" : "RIGHT";
            return $"Nearest loot: {item} {side} {direction.magnitude:0}m";
        }

        private string GetSharkStatus()
        {
            if (shark == null || shark.CurrentTarget == null) return "Shark: PATROL";
            return shark.CurrentTarget == player ? "Shark: TARGETING YOU" : $"Shark: targeting {shark.CurrentTarget.name}";
        }

        private void DrawEnding()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), string.Empty);
            GUI.Label(new Rect(0, Screen.height / 2f - 100, Screen.width, 60), "SEA DRAINED", titleStyle);
            GUI.Label(new Rect(0, Screen.height / 2f - 35, Screen.width, 40), "MVP COMPLETE", centerStyle);
            if (GUI.Button(new Rect(Screen.width / 2f - 90, Screen.height / 2f + 40, 180, 46), "RESTART"))
                GameManager.Instance?.Restart();
        }
    }
}
