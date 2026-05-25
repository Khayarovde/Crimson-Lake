using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    
    [Header("Настройки взаимодействия")]
    [Tooltip("Можно ли открыть сундук, если инвентарь уже открыт")]
    public bool canOpenWhenInventoryOpen = false;

    [Header("Gamepad")]
    [SerializeField] private float gamepadCursorSpeed = 1200f;
    [SerializeField, Range(0.05f, 0.75f)] private float triggerThreshold = 0.5f;
    
    private AudioSource audioSource;
    private bool isChestOpen = false;
    private Vector2 gamepadCursorPosition;
    private bool gamepadCursorInitialized;
    private bool cursorWasVisible;
    private CursorLockMode cursorWasLockState;
    private bool leftTriggerWasPressed;
    private bool rightTriggerWasPressed;

    private void OnEnable()
    {
        SaveManager.RegisterChest(this);
    }

    private void OnDisable()
    {
        SaveManager.UnregisterChest(this);
    }

    private void Start()
    {
        InitializeChest();
    }

    private void Update()
    {
        // Проверяем, не был ли закрыт UI сундука другим способом
        if (isChestOpen)
        {
            InventoryUI inventoryUI = FindFirstObjectByType<InventoryUI>();
            if (inventoryUI != null && !inventoryUI.IsChestUIOpen())
            {
                isChestOpen = false;
                RestoreCursorState();
            }

            if (inventoryUI != null)
            {
                HandleGamepadInput(inventoryUI);
            }
        }
    }

    public void HandleGamepadInput(InventoryUI inventoryUI)
    {
        if (!isChestOpen || inventoryUI == null)
            return;

        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
            return;

        ApplyCursorState(true);
        UpdateGamepadCursor(gamepad);

        bool leftTriggerPressed = gamepad.leftTrigger.ReadValue() >= triggerThreshold;
        bool rightTriggerPressed = gamepad.rightTrigger.ReadValue() >= triggerThreshold;

        if (leftTriggerPressed && !leftTriggerWasPressed)
        {
            inventoryUI.GoToInventoryFromButton();
            leftTriggerWasPressed = leftTriggerPressed;
            rightTriggerWasPressed = rightTriggerPressed;
            return;
        }

        if (gamepad.rightShoulder.wasPressedThisFrame)
        {
            ClickButtonUnderCursor();
        }

        if (gamepad.buttonEast.wasPressedThisFrame)
        {
            inventoryUI.CloseChestUI();
        }

        leftTriggerWasPressed = leftTriggerPressed;
        rightTriggerWasPressed = rightTriggerPressed;
    }

    private void UpdateGamepadCursor(Gamepad gamepad)
    {
        if (!gamepadCursorInitialized)
        {
            gamepadCursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            gamepadCursorInitialized = true;
        }

        Vector2 stick = gamepad.rightStick.ReadValue();
        if (stick.sqrMagnitude < 0.01f)
            return;

        gamepadCursorPosition += stick * gamepadCursorSpeed * Time.unscaledDeltaTime;
        gamepadCursorPosition.x = Mathf.Clamp(gamepadCursorPosition.x, 0f, Screen.width - 1f);
        gamepadCursorPosition.y = Mathf.Clamp(gamepadCursorPosition.y, 0f, Screen.height - 1f);

        if (Mouse.current != null)
        {
            Mouse.current.WarpCursorPosition(gamepadCursorPosition);
        }
    }

    private void ClickButtonUnderCursor()
    {
        if (EventSystem.current == null)
            return;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current != null ? Mouse.current.position.ReadValue() : gamepadCursorPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null)
                continue;

            Button button = result.gameObject.GetComponentInParent<Button>();
            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
                return;
            }
        }
    }

    private void ApplyCursorState(bool active)
    {
        if (active)
        {
            cursorWasVisible = Cursor.visible;
            cursorWasLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        RestoreCursorState();
    }

    private void RestoreCursorState()
    {
        Cursor.visible = cursorWasVisible;
        Cursor.lockState = cursorWasLockState;
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

        SetupCollider();
        
        // Подписываемся на события смены сцены
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnSceneChanged(UnityEngine.SceneManagement.Scene current, UnityEngine.SceneManagement.Scene next)
    {
        // Очищаем ссылки на этот сундук у игрока
        ClearPlayerReferences();
    }

    private void ClearPlayerReferences()
    {
        // Находим все объекты PlayerInventory и очищаем ссылки на этот сундук
        PlayerInventory[] allPlayers = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.InstanceID);
        foreach (PlayerInventory player in allPlayers)
        {
            player.ClearChestReference(this);
        }
        
        // Находим все объекты InventoryUI и очищаем ссылки на этот сундук
        InventoryUI[] allInventoryUIs = FindObjectsByType<InventoryUI>(FindObjectsSortMode.InstanceID);
        foreach (InventoryUI inventoryUI in allInventoryUIs)
        {
            inventoryUI.ClearChestReference(this);
        }
        
        isChestOpen = false;
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

    private void OnDestroy()
    {
        // Отписываемся от события
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
        ClearPlayerReferences();
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
            SaveManager.Instance?.MarkUnsaved();
            return true;
        }
        return false;
    }

    public void DestroyItemInChest(InventoryItem item)
    {
        if (chestData == null || item == null) return;
        
        chestData.RemoveItem(item);
        SaveManager.Instance?.MarkUnsaved();
    }

    public bool AddItemToChest(InventoryItem item)
    {
        if (chestData == null || item == null) return false;

        bool added = chestData.AddItem(item);
        if (added)
        {
            PlayStorageEffects();
            SaveManager.Instance?.MarkUnsaved();
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

    public void SetOpenState(bool open)
    {
        isChestOpen = open;
        if (!open)
        {
            leftTriggerWasPressed = false;
            rightTriggerWasPressed = false;
            gamepadCursorInitialized = false;
            RestoreCursorState();
        }
    }

    public bool IsFull { get { return chestData != null && chestData.items.Count >= chestData.maxSlots; } }
    public int ItemCount { get { return chestData != null ? chestData.items.Count : 0; } }
    public List<InventoryItem> GetChestItems() { return chestData != null ? chestData.items : new List<InventoryItem>(); }

    public List<string> GetChestItemNamesSnapshot()
    {
        List<string> result = new List<string>();
        if (chestData == null || chestData.items == null) return result;

        foreach (var item in chestData.items)
        {
            if (item != null && !string.IsNullOrEmpty(item.itemName))
                result.Add(item.itemName);
        }

        return result;
    }

    public void ApplyChestItemNamesSnapshot(List<string> itemNames)
    {
        if (chestData == null)
        {
            chestData = ScriptableObject.CreateInstance<InventoryData>();
            chestData.maxSlots = maxSlots;
        }

        chestData.Clear(); // Используем метод Clear из InventoryData
        if (itemNames == null) return;

        for (int i = 0; i < itemNames.Count; i++)
        {
            string itemName = itemNames[i];
            if (string.IsNullOrEmpty(itemName) || itemName == "Empty")
                continue;

            InventoryItem item = LoadItemFromResources(itemName);
            if (item != null && i < chestData.items.Count)
                chestData.items[i] = item;
            else if (item != null)
                chestData.items.Add(item);
        }
    }
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