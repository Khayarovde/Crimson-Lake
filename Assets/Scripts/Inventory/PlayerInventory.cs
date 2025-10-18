using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public InventoryData inventoryData; // Ссылка на ScriptableObject инвентаря
    public InventoryUI inventoryUI; // Ссылка на UI инвентаря
    public Transform dropPoint; // Точка спавна предметов
    public int activeItemIndex = -1; // Индекс активного предмета (-1 значит ничего не выбрано)

    [System.Serializable]
    public struct ItemPrefabMapping
    {
        public InventoryItem item; // ScriptableObject предмета
        public GameObject prefab; // Префаб для предмета в мире
    }
    public ItemPrefabMapping[] itemPrefabs; // Массив соответствий предметов и их префабов

    private void Start()
    {
        if (inventoryData == null) Debug.LogError("[PlayerInventory] InventoryData не назначен!");
        if (inventoryUI == null) Debug.LogError("[PlayerInventory] InventoryUI не назначен!");
        if (dropPoint == null) Debug.LogError("[PlayerInventory] DropPoint не назначен!");
        if (itemPrefabs == null || itemPrefabs.Length == 0)
            Debug.LogError("[PlayerInventory] Массив itemPrefabs пуст!");
        else
        {
            foreach (var mapping in itemPrefabs)
            {
                Debug.Log($"[PlayerInventory] Item: {mapping.item?.itemName}, Prefab: {mapping.prefab?.name}");
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            inventoryUI.ToggleInventory();
        }
    }

    public bool AddItemToInventory(InventoryItem item)
    {
        if (inventoryData == null) return false;

        bool added = inventoryData.AddItem(item);
        if (added)
        {
            Debug.Log($"[PlayerInventory] Предмет {item.itemName} добавлен.");
            inventoryUI.UpdateInventoryUI();
            AutoSelectActiveItem(); // Обновляем активный после добавления
            return true;
        }
        return false;
    }

    public void DropItem(InventoryItem item, int slotIndex)
    {
        if (item == null || item.type == InventoryItem.ItemType.Empty) return;

        // Находим префаб для предмета
        GameObject prefabToSpawn = null;
        foreach (var mapping in itemPrefabs)
        {
            if (mapping.item == item)
            {
                prefabToSpawn = mapping.prefab;
                break;
            }
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError($"[PlayerInventory] Не найден префаб для предмета {item.itemName}!");
            return;
        }

        // Спавним предмет в мире
        Vector3 spawnPosition = dropPoint.position + dropPoint.forward * 1f;
        GameObject spawnedItem = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        ItemPickup itemPickup = spawnedItem.GetComponent<ItemPickup>();
        if (itemPickup != null)
        {
            itemPickup.item = item; // Назначаем тот же InventoryItem
        }
        else
        {
            Debug.LogError($"[PlayerInventory] Сброшенный объект {spawnedItem.name} не имеет компонента ItemPickup!");
        }

        // Добавляем Rigidbody для физического падения (если отсутствует)
        Rigidbody rb = spawnedItem.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = spawnedItem.AddComponent<Rigidbody>();
            rb.useGravity = true;
        }

        // Удаляем предмет из инвентаря
        inventoryData.RemoveItem(item);
        Debug.Log($"[PlayerInventory] Предмет {item.itemName} сброшен из слота {slotIndex}.");
        if (activeItemIndex == slotIndex) activeItemIndex = -1; // Сбрасываем активный, если дропнули его
        AutoSelectActiveItem(); // Обновляем активный после дропа
        inventoryUI.UpdateInventoryUI();
    }

    // Автоматический выбор активного предмета (первого оружия или пистолета, если ничего не выбрано)
    public void AutoSelectActiveItem()
    {
        if (activeItemIndex != -1) return; // Если уже выбран, не меняем

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
        inventoryUI.UpdateInventoryUI(); // Обновляем UI после выбора
    }

    // Переключение активного предмета (direction: 1 вправо, -1 влево)
    public void SwitchActiveItem(int direction)
    {
        var slots = inventoryData.GetSlots();
        int itemCount = slots.Count; // Количество слотов (до maxSlots)

        if (itemCount == 0) return; // Нет предметов

        // Циклическое переключение среди оружия (Gun или Pistol)
        int newIndex = activeItemIndex;
        int attempts = itemCount; // Ограничиваем попытки, чтобы избежать бесконечного цикла
        do
        {
            newIndex = (newIndex + direction + itemCount) % itemCount;
            attempts--;
            if (attempts <= 0) return; // Выходим, если не нашли подходящий предмет
        } while (slots[newIndex] == null || slots[newIndex].type == InventoryItem.ItemType.Empty || 
                 (slots[newIndex].type != InventoryItem.ItemType.Gun && slots[newIndex].type != InventoryItem.ItemType.Pistol)); // Пропускаем неподходящие предметы

        activeItemIndex = newIndex;
        Debug.Log($"[PlayerInventory] Переключено на: {slots[newIndex].itemName} (индекс {newIndex}, тип {slots[newIndex].type})");
        inventoryUI.UpdateInventoryUI();
    }
}