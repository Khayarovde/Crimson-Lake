using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryData", menuName = "Scriptable Objects/InventoryData")]
public class InventoryData : ScriptableObject
{
    public List<InventoryItem> items = new List<InventoryItem>();
    public int maxSlots = 8;

    private void EnsureInitialized()
    {
        if (items == null)
            items = new List<InventoryItem>();

        if (items.Count > maxSlots)
            items.RemoveRange(maxSlots, items.Count - maxSlots);

        while (items.Count < maxSlots)
            items.Add(GetEmptyItem());

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null)
                items[i] = GetEmptyItem();
        }
    }

    public bool AddItem(InventoryItem item)
    {
        if (item == null)
            return false;

        EnsureInitialized();

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null || items[i].type == InventoryItem.ItemType.Empty)
            {
                items[i] = item;
                // Debug.Log($"Added item: {item.itemName}, Slot: {i}");
                return true;
            }
        }

        Debug.Log("Inventory full!");
        return false;
    }

    public void RemoveItem(InventoryItem item)
    {
        if (item == null)
            return;

        EnsureInitialized();

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == item)
            {
                items[i] = GetEmptyItem();
                return;
            }
        }
    }

    public int CountItemsByType(InventoryItem.ItemType type)
    {
        EnsureInitialized();

        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].type == type)
                count++;
        }

        return count;
    }

    public bool HasItemType(InventoryItem.ItemType type)
    {
        return CountItemsByType(type) > 0;
    }

    public bool ConsumeOneItemByType(InventoryItem.ItemType type)
    {
        EnsureInitialized();

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null || items[i].type != type)
                continue;

            items[i] = GetEmptyItem();
            return true;
        }

        return false;
    }

    // Новый метод: Своп предметов (indexA и indexB; -1 значит "пустой")
    public void SwapItems(int indexA, int indexB)
    {
        EnsureInitialized();

        if (indexA < 0 && indexB >= 0)
        {
            // Удалить из indexB
            if (indexB < items.Count)
                items[indexB] = GetEmptyItem();
        }
        else if (indexA >= 0 && indexB < 0)
        {
            // Вставить в indexA (но поскольку при drag мы убираем, это возврат)
            if (indexA < items.Count)
                items[indexA] = GetEmptyItem();
        }
        else if (indexA >= 0 && indexB >= 0 && indexA < items.Count && indexB < items.Count)
        {
            // Обмен
            InventoryItem temp = items[indexA];
            items[indexA] = items[indexB];
            items[indexB] = temp;
        }
    }

    public List<InventoryItem> GetSlots()
    {
        EnsureInitialized();
        return new List<InventoryItem>(items);
    }

    public int GetSlotCount()
    {
        EnsureInitialized();
        return items.Count;
    }

    public InventoryItem GetItemAt(int index)
    {
        EnsureInitialized();

        if (index < 0 || index >= items.Count)
            return null;

        return items[index];
    }

    public void SetItemAt(int index, InventoryItem item)
    {
        EnsureInitialized();

        if (index < 0 || index >= items.Count)
            return;

        items[index] = item != null ? item : GetEmptyItem();
    }

    public void ClearSlot(int index)
    {
        EnsureInitialized();

        if (index < 0 || index >= items.Count)
            return;

        items[index] = GetEmptyItem();
    }

  private InventoryItem GetEmptyItem() {
    InventoryItem empty = ScriptableObject.CreateInstance<InventoryItem>();
    empty.itemName = "Empty";
    empty.type = InventoryItem.ItemType.Empty;
    return empty;
  }
    
    // Новый метод: Очистка инвентаря
    public void Clear()
    {
        items.Clear();
        EnsureInitialized();
    }
}