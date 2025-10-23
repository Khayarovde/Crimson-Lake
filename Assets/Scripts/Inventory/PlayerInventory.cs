using UnityEngine;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{
    public InventoryData inventoryData;
    public InventoryUI inventoryUI;
    public int activeItemIndex = -1;

    [System.Serializable]
    public struct ItemPrefabMapping
    {
        public InventoryItem item;
        public GameObject prefab;
    }
    public ItemPrefabMapping[] itemPrefabs;

    private Chest nearbyChest;
    private List<Chest> nearbyChests = new List<Chest>();

    private void Start()
    {
        if (inventoryData == null) Debug.LogError("[PlayerInventory] InventoryData не назначен!");
        if (inventoryUI == null) Debug.LogError("[PlayerInventory] InventoryUI не назначен!");
        
        if (itemPrefabs == null || itemPrefabs.Length == 0)
            Debug.LogError("[PlayerInventory] Массив itemPrefabs пуст!");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            inventoryUI.ToggleInventory();
        }
        
        if (IsNearChest() && Input.GetKeyDown(KeyCode.E) && !inventoryUI.IsChestUIOpen())
        {
            OpenChest();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Chest chest = other.GetComponent<Chest>();
        if (chest != null && !nearbyChests.Contains(chest))
        {
            nearbyChests.Add(chest);
            UpdateNearbyChest();
            Debug.Log($"[PlayerInventory] Игрок рядом с сундуком {chest.gameObject.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Chest chest = other.GetComponent<Chest>();
        if (chest != null)
        {
            nearbyChests.Remove(chest);
            UpdateNearbyChest();
            Debug.Log($"[PlayerInventory] Игрок покинул сундук {chest.gameObject.name}");
        }
    }

    private void UpdateNearbyChest()
    {
        // Удаляем уничтоженные сундуки из списка
        nearbyChests.RemoveAll(chest => chest == null);

        if (nearbyChests.Count > 0)
        {
            float minDistance = float.MaxValue;
            Chest closestChest = null;
            
            foreach (Chest chest in nearbyChests)
            {
                if (chest == null) continue;
                
                float distance = Vector3.Distance(transform.position, chest.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestChest = chest;
                }
            }
            
            nearbyChest = closestChest;
        }
        else
        {
            nearbyChest = null;
        }
    }

    // Новый метод для очистки ссылок на сундук
    public void ClearChestReference(Chest chest)
    {
        if (nearbyChests.Contains(chest))
        {
            nearbyChests.Remove(chest);
        }
        
        if (nearbyChest == chest)
        {
            nearbyChest = null;
        }
        
        UpdateNearbyChest();
    }

    public bool AddItemToInventory(InventoryItem item)
    {
        if (inventoryData == null) return false;

        bool added = inventoryData.AddItem(item);
        if (added)
        {
            Debug.Log($"[PlayerInventory] Предмет {item.itemName} добавлен.");
            inventoryUI.UpdateInventoryUI();
            AutoSelectActiveItem();
            return true;
        }
        return false;
    }

    public void StoreItemInChest(InventoryItem item, int slotIndex)
    {
        if (item == null || item.type == InventoryItem.ItemType.Empty) return;

        if (nearbyChest != null)
        {
            bool addedToChest = nearbyChest.AddItemToChest(item);
            if (addedToChest)
            {
                Debug.Log($"[PlayerInventory] Предмет {item.itemName} перемещен в сундук.");
                
                inventoryData.RemoveItem(item);
                if (activeItemIndex == slotIndex) activeItemIndex = -1;
                AutoSelectActiveItem();
                inventoryUI.UpdateInventoryUI();
            }
            else
            {
                Debug.Log($"[PlayerInventory] Не удалось положить предмет в сундук - сундук полон.");
            }
        }
    }

    public void DestroyItem(InventoryItem item, int slotIndex)
    {
        if (item == null || item.type == InventoryItem.ItemType.Empty) return;

        Debug.Log($"[PlayerInventory] Предмет {item.itemName} уничтожен.");
        inventoryData.RemoveItem(item);
        if (activeItemIndex == slotIndex) activeItemIndex = -1;
        AutoSelectActiveItem();
        inventoryUI.UpdateInventoryUI();
    }

    public void AutoSelectActiveItem()
    {
        if (activeItemIndex != -1) return;

        var slots = inventoryData.GetSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && (slots[i].type == InventoryItem.ItemType.Gun || slots[i].type == InventoryItem.ItemType.Pistol))
            {
                activeItemIndex = i;
                Debug.Log($"[PlayerInventory] Автоматически выбрано: {slots[i].itemName} (индекс {i}, тип {slots[i].type})");
                break;
            }
        }
        inventoryUI.UpdateInventoryUI();
    }

    public void SwitchActiveItem(int direction)
    {
        var slots = inventoryData.GetSlots();
        int itemCount = slots.Count;

        if (itemCount == 0) return;

        int newIndex = activeItemIndex;
        int attempts = itemCount;
        do
        {
            newIndex = (newIndex + direction + itemCount) % itemCount;
            attempts--;
            if (attempts <= 0) return;
        } while (slots[newIndex] == null || slots[newIndex].type == InventoryItem.ItemType.Empty || 
                 (slots[newIndex].type != InventoryItem.ItemType.Gun && slots[newIndex].type != InventoryItem.ItemType.Pistol));

        activeItemIndex = newIndex;
        Debug.Log($"[PlayerInventory] Переключено на: {slots[newIndex].itemName} (индекс {newIndex}, тип {slots[newIndex].type})");
        inventoryUI.UpdateInventoryUI();
    }

    public bool IsNearChest()
    {
        return nearbyChest != null;
    }

    public Chest GetNearbyChest()
    {
        return nearbyChest;
    }

    public void OpenChest()
    {
        if (nearbyChest != null)
        {
            inventoryUI.OpenChestUI(nearbyChest);
        }
    }

    public void CloseChest()
    {
        inventoryUI.CloseChestUI();
    }
}