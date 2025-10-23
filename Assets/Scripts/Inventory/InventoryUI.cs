using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("Основные ссылки")]
    public InventoryData inventoryData;
    public GameObject slotPrefab;
    public Transform gridTransform;
    public Button leftArrowButton;
    public Button rightArrowButton;
    public TMP_Text activeItemInfoText;

    [Header("Канвасы")]
    public GameObject inventoryCanvas;
    public GameObject chestCanvas;

    [Header("Сетки для слотов")]
    public Transform chestGridTransform;

    [Header("Кнопки")]
    public Button toChestButton;
    public Button backFromChestButton;

    [Header("Префабы кнопок (опционально)")]
    public GameObject storeButtonPrefab;
    public GameObject destroyButtonPrefab;
    public GameObject takeButtonPrefab;

    private Image[] slotIcons;
    private Button[] storeButtons;
    private Button[] destroyButtons;
    private Outline[] outlines;
    private PlayerInventory playerInventory;
    private Chest currentChest;

    // UI элементы сундука
    private Image[] chestSlotIcons;
    private Button[] chestTakeButtons;
    private Button[] chestDestroyButtons;

    private void Start()
    {
        playerInventory = FindObjectOfType<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogError("[InventoryUI] PlayerInventory не найден!");
            return;
        }

        if (activeItemInfoText == null)
        {
            Debug.LogError("[InventoryUI] Text для информации об активном предмете не назначен!");
        }

        if (inventoryCanvas != null) inventoryCanvas.SetActive(false);
        if (chestCanvas != null) chestCanvas.SetActive(false);

        InitializeInventorySlots();
        InitializeChestSlots();
        InitializeButtons();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsChestUIOpen())
            {
                CloseChestUI();
            }
            else if (IsInventoryOpen())
            {
                ToggleInventory();
            }
        }

        // Проверяем, не был ли уничтожен текущий сундук
        if (currentChest == null && IsChestUIOpen())
        {
            CloseChestUI();
        }
    }

    // Новый метод для очистки ссылок на сундук
    public void ClearChestReference(Chest chest)
    {
        if (currentChest == chest)
        {
            currentChest = null;
            if (IsChestUIOpen())
            {
                CloseChestUI();
            }
        }
    }

    private void InitializeInventorySlots()
    {
        if (inventoryData == null || slotPrefab == null || gridTransform == null)
        {
            Debug.LogError("[InventoryUI] Не назначены inventoryData, slotPrefab или gridTransform!");
            return;
        }

        foreach (Transform child in gridTransform)
        {
            Destroy(child.gameObject);
        }

        slotIcons = new Image[inventoryData.maxSlots];
        storeButtons = new Button[inventoryData.maxSlots];
        destroyButtons = new Button[inventoryData.maxSlots];
        outlines = new Outline[inventoryData.maxSlots];

        for (int i = 0; i < inventoryData.maxSlots; i++)
        {
            GameObject slot = Instantiate(slotPrefab, gridTransform);
            slot.name = $"Slot_{i}";
            
            Image icon = slot.transform.Find("ItemIcon")?.GetComponent<Image>();
            slotIcons[i] = icon;
            if (icon == null)
            {
                Debug.LogError($"[InventoryUI] Не найдена ItemIcon в слоте {i}");
            }

            Button storeButton = FindOrCreateButton(slot, "StoreButton", storeButtonPrefab);
            storeButtons[i] = storeButton;
            if (storeButton != null)
            {
                int slotIndex = i;
                storeButton.onClick.AddListener(() => OnStoreButtonClicked(slotIndex));
                storeButton.gameObject.SetActive(false);
            }

            Button destroyButton = FindOrCreateButton(slot, "DestroyButton", destroyButtonPrefab);
            destroyButtons[i] = destroyButton;
            if (destroyButton != null)
            {
                int slotIndex = i;
                destroyButton.onClick.AddListener(() => OnDestroyButtonClicked(slotIndex));
                destroyButton.gameObject.SetActive(false);
            }

            Outline outline = icon?.GetComponent<Outline>();
            if (outline != null)
            {
                outlines[i] = outline;
                outline.enabled = false;
            }
        }

        UpdateInventoryUI();
    }

    private void InitializeChestSlots()
    {
        if (chestCanvas == null)
        {
            Debug.LogWarning("[InventoryUI] Chest Canvas не назначен, слоты сундука не будут созданы");
            return;
        }

        if (chestGridTransform == null)
        {
            chestGridTransform = chestCanvas.transform.Find("Grid");
            if (chestGridTransform == null)
            {
                Debug.LogError("[InventoryUI] Не найден Grid в канвасе сундука и chestGridTransform не назначен!");
                return;
            }
        }

        foreach (Transform child in chestGridTransform)
        {
            Destroy(child.gameObject);
        }

        chestSlotIcons = new Image[16];
        chestTakeButtons = new Button[16];
        chestDestroyButtons = new Button[16];

        for (int i = 0; i < 16; i++)
        {
            GameObject slot = Instantiate(slotPrefab, chestGridTransform);
            slot.name = $"ChestSlot_{i}";
            
            Image icon = slot.transform.Find("ItemIcon")?.GetComponent<Image>();
            chestSlotIcons[i] = icon;
            if (icon != null)
            {
                icon.enabled = false;
            }

            Button takeButton = FindOrCreateButton(slot, "TakeButton", takeButtonPrefab);
            chestTakeButtons[i] = takeButton;
            if (takeButton != null)
            {
                int slotIndex = i;
                takeButton.onClick.AddListener(() => OnChestTakeButtonClicked(slotIndex));
                takeButton.gameObject.SetActive(false);
            }

            Button destroyButton = FindOrCreateButton(slot, "DestroyButton", destroyButtonPrefab);
            chestDestroyButtons[i] = destroyButton;
            if (destroyButton != null)
            {
                int slotIndex = i;
                destroyButton.onClick.AddListener(() => OnChestDestroyButtonClicked(slotIndex));
                destroyButton.gameObject.SetActive(false);
            }
        }
    }

    private Button FindOrCreateButton(GameObject slot, string buttonName, GameObject buttonPrefab)
    {
        Button button = slot.transform.Find(buttonName)?.GetComponent<Button>();
        
        if (button != null)
        {
            return button;
        }
        
        if (buttonPrefab != null)
        {
            GameObject buttonObj = Instantiate(buttonPrefab, slot.transform);
            buttonObj.name = buttonName;
            return buttonObj.GetComponent<Button>();
        }
        
        Debug.LogWarning($"[InventoryUI] Кнопка {buttonName} не найдена в слоте и префаб не назначен. Создаю базовую кнопку.");
        
        GameObject newButtonObj = new GameObject(buttonName);
        newButtonObj.transform.SetParent(slot.transform);
        
        Image buttonImage = newButtonObj.AddComponent<Image>();
        buttonImage.color = Color.gray;
        
        Button newButton = newButtonObj.AddComponent<Button>();
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(newButtonObj.transform);
        Text text = textObj.AddComponent<Text>();
        text.text = buttonName.Replace("Button", "");
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        return newButton;
    }

    private void InitializeButtons()
    {
        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.AddListener(() => playerInventory.SwitchActiveItem(-1));
        }
        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.AddListener(() => playerInventory.SwitchActiveItem(1));
        }

        if (toChestButton != null)
        {
            toChestButton.onClick.AddListener(OpenChestUIFromInventory);
            toChestButton.gameObject.SetActive(false);
        }

        if (backFromChestButton != null)
        {
            backFromChestButton.onClick.AddListener(CloseChestUI);
        }
    }

    public void UpdateInventoryUI()
    {
        if (inventoryData == null || slotIcons == null) return;

        var slots = inventoryData.GetSlots();
        int activeIndex = playerInventory.activeItemIndex;

        if (activeItemInfoText != null)
        {
            if (activeIndex >= 0 && activeIndex < slots.Count && slots[activeIndex] != null && slots[activeIndex].type != InventoryItem.ItemType.Empty)
            {
                var activeItem = slots[activeIndex];
                switch (activeItem.type)
                {
                    case InventoryItem.ItemType.Gun:
                        activeItemInfoText.text = $"Тип: {activeItem.itemName}\nВинтовка, что использует патроны 10мм. Рабочая лошадка. Без модификаций.";
                        break;
                    case InventoryItem.ItemType.Pistol:
                        activeItemInfoText.text = $"Тип: {activeItem.itemName}\nЛёгкий пистолет с патронами 9мм. Быстрая перезарядка, низкий урон.";
                        break;
                    default:
                        activeItemInfoText.text = $"Активный предмет: {activeItem.itemName}\nТип: {activeItem.type}";
                        break;
                }
            }
            else
            {
                activeItemInfoText.text = "Ничего не выбрано";
            }
        }

        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (i < slots.Count && slots[i] != null && slots[i].type != InventoryItem.ItemType.Empty)
            {
                if (slotIcons[i] != null)
                {
                    slotIcons[i].sprite = slots[i].icon;
                    slotIcons[i].enabled = true;
                }
                
                bool nearChest = playerInventory.IsNearChest();
                
                if (storeButtons[i] != null)
                    storeButtons[i].gameObject.SetActive(nearChest);
                
                if (destroyButtons[i] != null)
                    destroyButtons[i].gameObject.SetActive(true);
                
                if (outlines[i] != null)
                    outlines[i].enabled = (i == activeIndex);
            }
            else
            {
                if (slotIcons[i] != null)
                    slotIcons[i].enabled = false;
                
                if (storeButtons[i] != null)
                    storeButtons[i].gameObject.SetActive(false);
                
                if (destroyButtons[i] != null)
                    destroyButtons[i].gameObject.SetActive(false);
                
                if (outlines[i] != null)
                    outlines[i].enabled = false;
            }
        }

        if (toChestButton != null)
        {
            toChestButton.gameObject.SetActive(playerInventory.IsNearChest());
        }
    }

    private void UpdateChestUI()
    {
        if (currentChest == null || chestSlotIcons == null) return;

        var chestItems = currentChest.GetChestItems();

        for (int i = 0; i < chestSlotIcons.Length; i++)
        {
            if (i < chestItems.Count && chestItems[i] != null && chestItems[i].type != InventoryItem.ItemType.Empty)
            {
                if (chestSlotIcons[i] != null)
                {
                    chestSlotIcons[i].sprite = chestItems[i].icon;
                    chestSlotIcons[i].enabled = true;
                }
                
                if (chestTakeButtons[i] != null)
                    chestTakeButtons[i].gameObject.SetActive(true);
                
                if (chestDestroyButtons[i] != null)
                    chestDestroyButtons[i].gameObject.SetActive(true);
            }
            else
            {
                if (chestSlotIcons[i] != null)
                    chestSlotIcons[i].enabled = false;
                
                if (chestTakeButtons[i] != null)
                    chestTakeButtons[i].gameObject.SetActive(false);
                
                if (chestDestroyButtons[i] != null)
                    chestDestroyButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnStoreButtonClicked(int slotIndex)
    {
        var slots = inventoryData.GetSlots();
        if (slotIndex < slots.Count && slots[slotIndex] != null && slots[slotIndex].type != InventoryItem.ItemType.Empty)
        {
            playerInventory.StoreItemInChest(slots[slotIndex], slotIndex);
            UpdateInventoryUI();
        }
    }

    private void OnDestroyButtonClicked(int slotIndex)
    {
        var slots = inventoryData.GetSlots();
        if (slotIndex < slots.Count && slots[slotIndex] != null && slots[slotIndex].type != InventoryItem.ItemType.Empty)
        {
            playerInventory.DestroyItem(slots[slotIndex], slotIndex);
            UpdateInventoryUI();
        }
    }

    private void OnChestTakeButtonClicked(int slotIndex)
    {
        if (currentChest != null)
        {
            var chestItems = currentChest.GetChestItems();
            if (slotIndex < chestItems.Count && chestItems[slotIndex] != null)
            {
                currentChest.TakeItemFromChest(chestItems[slotIndex], playerInventory);
                UpdateChestUI();
                UpdateInventoryUI();
            }
        }
    }

    private void OnChestDestroyButtonClicked(int slotIndex)
    {
        if (currentChest != null)
        {
            var chestItems = currentChest.GetChestItems();
            if (slotIndex < chestItems.Count && chestItems[slotIndex] != null)
            {
                currentChest.DestroyItemInChest(chestItems[slotIndex]);
                UpdateChestUI();
            }
        }
    }

    public void ToggleInventory()
    {
        if (inventoryCanvas != null)
        {
            bool isActive = !inventoryCanvas.activeSelf;
            inventoryCanvas.SetActive(isActive);
            
            if (isActive)
            {
                playerInventory.AutoSelectActiveItem();
                UpdateInventoryUI();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public void OpenChestUI(Chest chest)
    {
        if (chest == null) 
        {
            Debug.LogWarning("[InventoryUI] Попытка открыть сундук, который равен null");
            return;
        }

        currentChest = chest;
        if (chestCanvas != null)
        {
            chestCanvas.SetActive(true);
            UpdateChestUI();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void OpenChestUIFromInventory()
    {
        if (playerInventory.IsNearChest())
        {
            OpenChestUI(playerInventory.GetNearbyChest());
            if (inventoryCanvas != null)
            {
                inventoryCanvas.SetActive(false);
            }
        }
    }

    public void CloseChestUI()
    {
        if (chestCanvas != null)
        {
            chestCanvas.SetActive(false);
        }
        currentChest = null;
        
        if (inventoryCanvas != null)
        {
            inventoryCanvas.SetActive(true);
            UpdateInventoryUI();
        }
    }

    public bool IsInventoryOpen()
    {
        return inventoryCanvas != null && inventoryCanvas.activeSelf;
    }

    public bool IsChestUIOpen()
    {
        return chestCanvas != null && chestCanvas.activeSelf;
    }
}