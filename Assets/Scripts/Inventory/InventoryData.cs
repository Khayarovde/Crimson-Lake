using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryData", menuName = "Scriptable Objects/InventoryData")]
public class InventoryData : ScriptableObject
{
    public List<InventoryItem> items = new List<InventoryItem>();
    public int maxSlots = 8;

    public bool AddItem(InventoryItem item)
    {
          if (items.Count < maxSlots)
          {
          items.Add(item);
          Debug.Log($"Added item: {item.itemName}, Total items: {items.Count}");
          return true;
          }
      Debug.Log("Inventory full!");
      return false;
    }

    public void RemoveItem(InventoryItem item)
    {
        items.Remove(item);
    }

    // Новый метод: Своп предметов (indexA и indexB; -1 значит "пустой")
    public void SwapItems(int indexA, int indexB)
    {
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

        // Удаляем null, если нужно (для чистоты)
        items.RemoveAll(i => i == null);
    }

    public List<InventoryItem> GetSlots()
    {
        List<InventoryItem> slots = new List<InventoryItem>(items);
        while (slots.Count < maxSlots)
        {
            slots.Add(GetEmptyItem());
        }
        return slots;
    }

    private InventoryItem GetEmptyItem()
    {
        InventoryItem empty = ScriptableObject.CreateInstance<InventoryItem>();
        empty.itemName = "Empty";
        empty.type = InventoryItem.ItemType.Empty;
        return empty;
    }
}