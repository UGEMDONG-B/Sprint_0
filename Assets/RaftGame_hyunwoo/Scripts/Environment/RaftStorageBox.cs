using System;
using System.Collections.Generic;
using UnityEngine;

namespace RaftSharkDive
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaftStorageBox : MonoBehaviour, IInteractable
    {
        [SerializeField, Range(1, 30)] private int capacity = 10;
        private readonly List<ItemId> slots = new List<ItemId>(10);

        public event Action Changed;
        public int Capacity => capacity;
        public int Count => slots.Count;
        public int GetCount(ItemId item)
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == item) count++;
            return count;
        }

        public string GetInteractionPrompt(PlayerController player) =>
            $"E - Open Raft Storage ({slots.Count}/{capacity})";

        public bool CanInteract(PlayerController player) => player != null;

        public void Interact(PlayerController player)
        {
            GameUI.Instance?.OpenStorage(this);
        }

        public ItemId? GetSlotItem(int index) => index >= 0 && index < slots.Count ? slots[index] : null;

        public bool TryStoreFrom(Inventory inventory, int inventorySlot)
        {
            if (RaftMultiplayerHooks.TryStoreItem != null &&
                RaftMultiplayerHooks.TryStoreItem(this, inventory, inventorySlot)) return true;
            return TryStoreFromLocal(inventory, inventorySlot);
        }

        public bool TryStoreFromLocal(Inventory inventory, int inventorySlot)
        {
            if (inventory == null || slots.Count >= capacity) return false;
            if (!inventory.TryRemoveSlot(inventorySlot, out ItemId item)) return false;
            slots.Add(item);
            Changed?.Invoke();
            return true;
        }

        public bool TryTakeTo(Inventory inventory, int storageSlot)
        {
            if (RaftMultiplayerHooks.TryTakeItem != null &&
                RaftMultiplayerHooks.TryTakeItem(this, inventory, storageSlot)) return true;
            return TryTakeToLocal(inventory, storageSlot);
        }

        public bool TryTakeToLocal(Inventory inventory, int storageSlot)
        {
            ItemId? item = GetSlotItem(storageSlot);
            if (inventory == null || !item.HasValue || !inventory.TryAdd(item.Value)) return false;
            slots.RemoveAt(storageSlot);
            Changed?.Invoke();
            return true;
        }

        public bool Consume(ItemId item, int amount)
        {
            if (amount <= 0 || GetCount(item) < amount) return false;
            for (int i = slots.Count - 1; i >= 0 && amount > 0; i--)
            {
                if (slots[i] != item) continue;
                slots.RemoveAt(i);
                amount--;
            }
            Changed?.Invoke();
            return true;
        }

        public int[] GetSerializableSlots()
        {
            int[] result = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++) result[i] = (int)slots[i];
            return result;
        }

        public void RestoreSlots(int[] savedSlots)
        {
            slots.Clear();
            int itemTypeCount = Enum.GetValues(typeof(ItemId)).Length;
            if (savedSlots != null)
                for (int i = 0; i < savedSlots.Length && slots.Count < capacity; i++)
                    if (savedSlots[i] >= 0 && savedSlots[i] < itemTypeCount)
                        slots.Add((ItemId)savedSlots[i]);
            Changed?.Invoke();
        }

        public static RaftStorageBox FindExisting()
        {
            return FindFirstObjectByType<RaftStorageBox>(FindObjectsInactive.Include);
        }

        public static RaftStorageBox CreateOnRaft(Transform raft, Vector3? desiredWorldPosition = null)
        {
            RaftStorageBox existing = FindExisting();
            if (existing != null) return existing;
            if (raft == null) return null;

            GameObject root = new GameObject("RaftStorageBox");
            root.transform.SetParent(raft, false);
            BoxCollider deck = raft.GetComponent<BoxCollider>();
            Vector3 deckCenter = deck != null ? deck.center : Vector3.zero;
            Vector3 deckSize = deck != null ? deck.size : new Vector3(8f, 0.35f, 8f);
            Vector3 localPosition = desiredWorldPosition.HasValue
                ? raft.InverseTransformPoint(desiredWorldPosition.Value)
                : new Vector3(deckCenter.x + deckSize.x * 0.5f - 1.15f, 0f,
                    deckCenter.z - deckSize.z * 0.25f);
            localPosition.x = Mathf.Clamp(localPosition.x, deckCenter.x - deckSize.x * 0.5f + 0.9f,
                deckCenter.x + deckSize.x * 0.5f - 0.9f);
            localPosition.z = Mathf.Clamp(localPosition.z, deckCenter.z - deckSize.z * 0.5f + 0.7f,
                deckCenter.z + deckSize.z * 0.5f - 0.7f);
            localPosition.y = deckCenter.y + deckSize.y * 0.5f + 0.55f;
            root.transform.localPosition = localPosition;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 1.05f, 1.15f);
            collider.center = Vector3.zero;
            RaftStorageBox storage = root.AddComponent<RaftStorageBox>();
            CreateVisual(root.transform, "Body", new Vector3(0f, -0.08f, 0f), new Vector3(1.5f, 0.82f, 1.05f),
                new Color(0.30f, 0.15f, 0.055f));
            CreateVisual(root.transform, "Lid", new Vector3(0f, 0.46f, 0f), new Vector3(1.62f, 0.18f, 1.16f),
                new Color(0.42f, 0.23f, 0.08f));
            return storage;
        }

        private static void CreateVisual(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = visual.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (renderer != null && shader != null)
            {
                renderer.material = new Material(shader);
                renderer.material.color = color;
            }
        }
    }
}
