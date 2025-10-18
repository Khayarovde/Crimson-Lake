using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public InventoryData inventoryData; // Ссылка на инвентарь
    public GameObject slotPrefab; // Префаб слота
    public Transform gridTransform; // Transform сетки (Grid Layout Group)
    public Button leftArrowButton; // Кнопка стрелки влево
    public Button rightArrowButton; // Кнопка стрелки вправо
    public TMPro.TextMeshProUGUI activeItemInfoText; // Ссылка на Text для отображения информации об активном предмете
    private GameObject inventoryPanel; // Панель инвентаря
    private Image[] slotIcons; // Массив иконок слотов
    private Button[] dropButtons; // Массив кнопок дропа
    private Outline[] outlines; // Массив контуров для выделения
    private PlayerInventory playerInventory; // Ссылка на PlayerInventory

    private void Start()
    {
        // Находим панель инвентаря
        inventoryPanel = gameObject;
        if (inventoryPanel == null)
        {
            Debug.LogError("[InventoryUI] Панель инвентаря не найдена!");
            return;
        }

        // Находим PlayerInventory
        playerInventory = FindObjectOfType<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogError("[InventoryUI] PlayerInventory не найден!");
            return;
        }

        // Проверяем, назначен ли activeItemInfoText
        if (activeItemInfoText == null)
        {
            Debug.LogError("[InventoryUI] Text для информации об активном предмете не назначен!");
        }

        // Скрываем инвентарь при старте
        inventoryPanel.SetActive(false);

        // Инициализируем слоты и стрелочки
        InitializeSlots();
        InitializeArrows();
    }

    private void InitializeSlots()
    {
        if (inventoryData == null || slotPrefab == null || gridTransform == null)
        {
            Debug.LogError("[InventoryUI] Не назначены inventoryData, slotPrefab или gridTransform!");
            return;
        }

        // Очищаем существующие слоты
        foreach (Transform child in gridTransform)
        {
            Destroy(child.gameObject);
        }

        // Создаём слоты
        slotIcons = new Image[inventoryData.maxSlots];
        dropButtons = new Button[inventoryData.maxSlots];
        outlines = new Outline[inventoryData.maxSlots];
        for (int i = 0; i < inventoryData.maxSlots; i++)
        {
            GameObject slot = Instantiate(slotPrefab, gridTransform);
            slot.name = $"Slot_{i}";
            Image icon = slot.transform.Find("ItemIcon").GetComponent<Image>();
            slotIcons[i] = icon;

            // Находим кнопку Drop и добавляем обработчик
            Button dropButton = slot.transform.Find("DropButton").GetComponent<Button>();
            dropButtons[i] = dropButton;
            int slotIndex = i;
            dropButton.onClick.AddListener(() => OnDropButtonClicked(slotIndex));
            dropButton.gameObject.SetActive(false);

            // Находим Outline (должен быть на ItemIcon или слоте)
            Outline outline = icon.GetComponent<Outline>(); // Или slot.GetComponent<Outline>()
            if (outline != null)
            {
                outlines[i] = outline;
                outline.enabled = false; // Отключаем по умолчанию
            }
            else
            {
                Debug.LogWarning($"[InventoryUI] Outline не найден на слоте {i}!");
            }
        }

        // Обновляем содержимое слотов
        UpdateInventoryUI();
    }

    private void InitializeArrows()
    {
        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.AddListener(() => playerInventory.SwitchActiveItem(-1)); // Переключение влево
        }
        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.AddListener(() => playerInventory.SwitchActiveItem(1)); // Переключение вправо
        }
    }

    public void UpdateInventoryUI()
    {
        if (inventoryData == null || slotIcons == null || dropButtons == null || outlines == null)
            return;

        // Получаем текущие слоты из инвентаря
        var slots = inventoryData.GetSlots();
        int activeIndex = playerInventory.activeItemIndex;

        // Обновляем текст активного предмета
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

        // Обновляем слоты
        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (i < slots.Count && slots[i] != null && slots[i].type != InventoryItem.ItemType.Empty)
            {
                slotIcons[i].sprite = slots[i].icon;
                slotIcons[i].enabled = true;
                dropButtons[i].gameObject.SetActive(true);
                outlines[i].enabled = (i == activeIndex); // Включаем контур только для активного слота
            }
            else
            {
                slotIcons[i].enabled = false;
                dropButtons[i].gameObject.SetActive(false);
                outlines[i].enabled = false;
            }
        }
    }

    private void OnDropButtonClicked(int slotIndex)
    {
        var slots = inventoryData.GetSlots();
        if (slotIndex < slots.Count && slots[slotIndex] != null && slots[slotIndex].type != InventoryItem.ItemType.Empty)
        {
            playerInventory.DropItem(slots[slotIndex], slotIndex);
            UpdateInventoryUI();
        }
    }

    public void ToggleInventory()
    {
        if (inventoryPanel != null)
        {
            bool isActive = !inventoryPanel.activeSelf;
            inventoryPanel.SetActive(isActive);
            if (isActive)
            {
                playerInventory.AutoSelectActiveItem(); // Автоматически определяем активное при открытии
                UpdateInventoryUI();
            }
        }
    }
}