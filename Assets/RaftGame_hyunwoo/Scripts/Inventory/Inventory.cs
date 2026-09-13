using System;
using System.Collections.Generic;
using UnityEngine;

namespace RaftSharkDive
{
    public sealed class Inventory : MonoBehaviour
    {
        [SerializeField, Range(1, 10)] private int maxSlots = 5;

        private readonly List<ItemId> slots = new List<ItemId>(5);

        public event Action Changed;

        public int MaxSlots => maxSlots;
        public int OccupiedSlotCount => slots.Count;
        public int FreeSlotCount => maxSlots - slots.Count;
        public int SelectedSlotIndex { get; private set; }
        public ItemId? SelectedItem => SelectedSlotIndex >= 0 && SelectedSlotIndex < slots.Count
            ? slots[SelectedSlotIndex]
            : null;

        public int GetCount(ItemId item)
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == item) count++;
            return count;
        }

        public bool Has(ItemId item, int amount = 1) => GetCount(item) >= amount;

        public bool CanAdd(ItemId item) => slots.Count < maxSlots;

        public ItemId? GetSlotItem(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < slots.Count ? slots[slotIndex] : null;
        }

        public bool TryAdd(ItemId item, int amount = 1, bool selectAdded = false)
        {
            if (amount <= 0 || slots.Count + amount > maxSlots) return false;
            int firstAddedIndex = slots.Count;
            for (int i = 0; i < amount; i++) slots.Add(item);
            if (selectAdded) SelectedSlotIndex = firstAddedIndex;
            Changed?.Invoke();
            return true;
        }

        public void Add(ItemId item, int amount = 1) => TryAdd(item, amount);

        public bool Consume(ItemId item, int amount = 1)
        {
            if (!Has(item, amount)) return false;
            int remainingToRemove = amount;
            for (int i = slots.Count - 1; i >= 0 && remainingToRemove > 0; i--)
            {
                if (slots[i] != item || i == SelectedSlotIndex) continue;
                RemoveSlotInternal(i);
                remainingToRemove--;
            }
            if (remainingToRemove > 0 && SelectedItem == item)
            {
                RemoveSlotInternal(SelectedSlotIndex);
                remainingToRemove--;
            }
            Changed?.Invoke();
            return true;
        }

        public bool ConsumeSelected(out ItemId item)
        {
            ItemId? selected = SelectedItem;
            if (!selected.HasValue)
            {
                item = default;
                return false;
            }
            item = selected.Value;
            RemoveSlotInternal(SelectedSlotIndex);
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveSlot(int slotIndex, out ItemId item)
        {
            ItemId? selected = GetSlotItem(slotIndex);
            if (!selected.HasValue)
            {
                item = default;
                return false;
            }
            item = selected.Value;
            RemoveSlotInternal(slotIndex);
            Changed?.Invoke();
            return true;
        }

        private void RemoveSlotInternal(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count) return;
            slots.RemoveAt(slotIndex);
            if (slots.Count == 0) SelectedSlotIndex = 0;
            else if (slotIndex < SelectedSlotIndex) SelectedSlotIndex--;
            else SelectedSlotIndex = Mathf.Min(SelectedSlotIndex, slots.Count - 1);
        }

        public void SelectSlot(int slotIndex)
        {
            int next = Mathf.Clamp(slotIndex, 0, maxSlots - 1);
            if (SelectedSlotIndex == next) return;
            SelectedSlotIndex = next;
            Changed?.Invoke();
        }

        public int[] GetSerializableCounts()
        {
            ItemId[] values = (ItemId[])Enum.GetValues(typeof(ItemId));
            int[] counts = new int[values.Length];
            for (int i = 0; i < values.Length; i++) counts[i] = GetCount(values[i]);
            return counts;
        }

        public int[] GetSerializableSlots()
        {
            int[] result = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++) result[i] = (int)slots[i];
            return result;
        }

        public void RestoreSerializableSlots(int[] savedSlots)
        {
            slots.Clear();
            int itemTypeCount = Enum.GetValues(typeof(ItemId)).Length;
            if (savedSlots != null)
                for (int i = 0; i < savedSlots.Length && slots.Count < maxSlots; i++)
                    if (savedSlots[i] >= 0 && savedSlots[i] < itemTypeCount)
                        slots.Add((ItemId)savedSlots[i]);
            SelectedSlotIndex = Mathf.Clamp(SelectedSlotIndex, 0, Mathf.Max(0, slots.Count - 1));
            Changed?.Invoke();
        }

        public void RestoreSerializableCounts(int[] counts)
        {
            slots.Clear();
            ItemId[] values = (ItemId[])Enum.GetValues(typeof(ItemId));
            if (counts != null)
                for (int i = 0; i < values.Length && i < counts.Length; i++)
                    for (int amount = 0; amount < counts[i] && slots.Count < maxSlots; amount++)
                        slots.Add(values[i]);
            SelectedSlotIndex = Mathf.Clamp(SelectedSlotIndex, 0, Mathf.Max(0, slots.Count - 1));
            Changed?.Invoke();
        }
    }
}
