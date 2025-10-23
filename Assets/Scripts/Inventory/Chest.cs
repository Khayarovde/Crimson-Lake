using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Chest : MonoBehaviour
{
    [Header("Настройки сундука")]
    public InventoryData chestData;
    public int maxSlots = 16;
    
    [Header("Визуальные эффекты")]
    public ParticleSystem storageEffect;
    public AudioClip storageSound;
    
    [Header("Сохранение")]
    public string chestId = "default_chest";
    
    private AudioSource audioSource;
    private string savePath;
    private bool isQuitting = false;

    private void Start()
    {
        InitializeChest();
        LoadChestData();
    }

    private void InitializeChest()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (chestData == null)
        {
            chestData = ScriptableObject.CreateInstance<InventoryData>();
            chestData.maxSlots = maxSlots;
        }

        savePath = Path.Combine(Application.persistentDataPath, $"{chestId}_chest.json");
        
        SetupCollider();
        
        // Подписываемся на события смены сцены
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnSceneChanged(UnityEngine.SceneManagement.Scene current, UnityEngine.SceneManagement.Scene next)
    {
        // Сохраняем данные при смене сцены
        SaveChestData();
        
        // Очищаем ссылки на этот сундук у игрока
        ClearPlayerReferences();
    }

    private void ClearPlayerReferences()
    {
        // Находим все объекты PlayerInventory и очищаем ссылки на этот сундук
        PlayerInventory[] allPlayers = FindObjectsOfType<PlayerInventory>();
        foreach (PlayerInventory player in allPlayers)
        {
            player.ClearChestReference(this);
        }
        
        // Находим все объекты InventoryUI и очищаем ссылки на этот сундук
        InventoryUI[] allInventoryUIs = FindObjectsOfType<InventoryUI>();
        foreach (InventoryUI inventoryUI in allInventoryUIs)
        {
            inventoryUI.ClearChestReference(this);
        }
    }

    private void SetupCollider()
    {
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(2f, 2f, 2f);
        }
        else if (!collider.isTrigger)
        {
            collider.isTrigger = true;
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        // Отписываемся от события
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        
        if (!isQuitting)
        {
            SaveChestData();
            ClearPlayerReferences();
        }
    }

    private void LoadChestData()
    {
        if (File.Exists(savePath))
        {
            try
            {
                string json = File.ReadAllText(savePath);
                ChestSaveData saveData = JsonUtility.FromJson<ChestSaveData>(json);
                
                chestData.items.Clear();
                
                foreach (var itemSave in saveData.items)
                {
                    InventoryItem item = LoadItemFromResources(itemSave.itemName);
                    if (item != null)
                    {
                        chestData.items.Add(item);
                    }
                }
                
                Debug.Log($"[Chest] Данные сундука загружены: {chestData.items.Count} предметов");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Chest] Ошибка загрузки данных сундука: {e.Message}");
            }
        }
    }

    private void SaveChestData()
    {
        try
        {
            ChestSaveData saveData = new ChestSaveData();
            
            foreach (var item in chestData.items)
            {
                if (item != null)
                {
                    saveData.items.Add(new ItemSaveData { itemName = item.itemName });
                }
            }
            
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(savePath, json);
            
            Debug.Log($"[Chest] Данные сундука сохранены: {chestData.items.Count} предметов");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Chest] Ошибка сохранения данных сундука: {e.Message}");
        }
    }

    private InventoryItem LoadItemFromResources(string itemName)
    {
        InventoryItem item = Resources.Load<InventoryItem>($"Items/{itemName}");
        if (item == null)
        {
            InventoryItem[] allItems = Resources.FindObjectsOfTypeAll<InventoryItem>();
            item = allItems.FirstOrDefault(i => i.itemName == itemName);
        }
        
        if (item == null)
        {
            Debug.LogWarning($"[Chest] Не удалось загрузить предмет: {itemName}");
        }
        
        return item;
    }

    public bool TakeItemFromChest(InventoryItem item, PlayerInventory playerInventory)
    {
        if (chestData == null || item == null || playerInventory == null) return false;

        bool added = playerInventory.AddItemToInventory(item);
        if (added)
        {
            chestData.RemoveItem(item);
            PlayStorageEffects();
            SaveChestData();
            return true;
        }
        return false;
    }

    public void DestroyItemInChest(InventoryItem item)
    {
        if (chestData == null || item == null) return;
        
        chestData.RemoveItem(item);
        SaveChestData();
    }

    public bool AddItemToChest(InventoryItem item)
    {
        if (chestData == null || item == null) return false;

        bool added = chestData.AddItem(item);
        if (added)
        {
            PlayStorageEffects();
            SaveChestData();
            return true;
        }
        return false;
    }

    private void PlayStorageEffects()
    {
        if (storageEffect != null)
        {
            Instantiate(storageEffect, transform.position, Quaternion.identity);
        }

        if (storageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(storageSound);
        }
    }

    public bool IsFull { get { return chestData != null && chestData.items.Count >= chestData.maxSlots; } }
    public int ItemCount { get { return chestData != null ? chestData.items.Count : 0; } }
    public List<InventoryItem> GetChestItems() { return chestData != null ? chestData.items : new List<InventoryItem>(); }
}

[System.Serializable]
public class ItemSaveData
{
    public string itemName;
}

[System.Serializable]
public class ChestSaveData
{
    public List<ItemSaveData> items = new List<ItemSaveData>();
}