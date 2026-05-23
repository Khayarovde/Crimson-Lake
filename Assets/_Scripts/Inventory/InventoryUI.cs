using UnityEngine;
using UnityEngine.UI;
using UIOutline = UnityEngine.UI.Outline;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;

public class InventoryUI : MonoBehaviour
{
    [System.Serializable]
    public class ItemCombineRecipe
    {
        public InventoryItem firstItem;
        public InventoryItem secondItem;
        public InventoryItem resultItem;
    }

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

    [Header("Подсветка")]
    public Color outlineColor = Color.green;

    [Header("HP Видео (опционально)")]
    [Tooltip("VideoPlayer, который выводит mp4 в RawImage")]
    public VideoPlayer hpVideoPlayer;

    [Tooltip("RawImage для отображения текстуры VideoPlayer (если VideoPlayer работает в API Only)")]
    public RawImage hpVideoRawImage;

    [Tooltip("Клип для HP <= 25")]
    public VideoClip hp25Clip;

    [Tooltip("Клип для HP <= 50")]
    public VideoClip hp50Clip;

    [Tooltip("Клип для HP > 50 (обычно 100 HP)")]
    public VideoClip hp100Clip;

    [SerializeField] private bool loopHpVideo = true;

    [Header("Позиции кнопок в слотах")]
    [Tooltip("Локальная позиция кнопки Store в каждом слоте")]
    public Vector2 storeButtonPosition = new Vector2(30, -30);
    
    [Tooltip("Локальная позиция кнопки Destroy в каждом слоте")]
    public Vector2 destroyButtonPosition = new Vector2(-30, -30);
    
    [Tooltip("Локальная позиция кнопки Take в слотах сундука")]
    public Vector2 takeButtonPosition = new Vector2(30, 30);

    [Header("Контекстное меню (ПКМ)")]
    [Tooltip("Опционально: готовая панель контекстного меню. Если не задана, будет создана автоматически")]
    public GameObject contextMenuPanel;
    public Button contextSelectButton;
    public Button contextCombineButton;
    public Button contextDestroyButton;
    public Button contextCancelButton;

    [Header("Рецепты соединения")]
    [Tooltip("Пара предметов -> результирующий предмет")]
    public List<ItemCombineRecipe> combineRecipes = new List<ItemCombineRecipe>();

    private Image[] slotIcons;
    private GameObject[] slotObjects;
    private Button[] storeButtons;
    private Button[] destroyButtons;
    private UIOutline[] outlines;
    private PlayerInventory playerInventory;
    private PlayerHealth playerHealth;
    private Chest currentChest;
    private bool wasInventoryOpenBeforeChest;
    private int lastClickedSlotIndex = -1;
    private float lastClickTime = -10f;
    [SerializeField] private float medkitDoubleClickThreshold = 0.3f;
    [Header("Анимация текста (DOTween)")]
    [SerializeField] private float craftFailShakeDistance = 20f;
    [SerializeField] private float craftFailShakeDuration = 0.35f;
    [SerializeField] private int craftFailShakeVibrato = 14;
    private bool isCombineSelectionMode;
    private int combineSourceSlotIndex = -1;
    private int contextMenuSlotIndex = -1;
    private int contextMenuSelectionIndex;
    private readonly List<Button> contextMenuButtons = new List<Button>();
    private readonly Color contextMenuButtonColor = Color.white;
    private readonly Color contextMenuButtonHoverColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    private Tween craftFailTextTween;

    [Header("Пагинация инвентаря")]
    [SerializeField] private bool useInventoryPagination = true;
    [SerializeField] private int slotsPerPage = 3;
    private int currentInventoryPage;

    // UI элементы сундука
    private Image[] chestSlotIcons;
    private Button[] chestTakeButtons;
    private Button[] chestDestroyButtons;
    private UIOutline[] chestOutlines;
    private int selectedChestIndex = 0;
    private float lastChestNav = 0f;
    private float chestNavCooldown = 0.12f;
    private Vector2 chestGamepadCursorPosition;
    private bool chestGamepadCursorInitialized;
    private bool chestCursorWasVisible;
    private CursorLockMode chestCursorWasLocked;
    [SerializeField] private float chestGamepadCursorSpeed = 1200f;
    [SerializeField] private float triggerSwitchThreshold = 0.5f;
    private bool leftTriggerWasPressed;
    private bool rightTriggerWasPressed;

    private enum HpVideoState
    {
        None,
        Hp25,
        Hp50,
        Hp100
    }

    private HpVideoState currentHpVideoState = HpVideoState.None;
    private VideoClip currentHpClip;

    private string lastActiveItemInfoText;
    private int lastActiveItemIndex = -1;
    private bool lastNearChest;

    private void Start()
    {
        playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogError("[InventoryUI] PlayerInventory не найден!");
            return;
        }

        playerHealth = playerInventory.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();

        // Fallback: bind buttons by name if not assigned in inspector.
        if (toChestButton == null)
            toChestButton = FindButtonByName("BtnNextChest");
        if (backFromChestButton == null)
            backFromChestButton = FindButtonByName("BtnNextInventory");

        if (activeItemInfoText == null)
        {
            Debug.LogError("[InventoryUI] Text для информации об активном предмете не назначен!");
        }

        if (inventoryCanvas != null) inventoryCanvas.SetActive(false);
        if (chestCanvas != null) chestCanvas.SetActive(false);

        InitializeInventorySlots();
        InitializeButtons();
        InitializeContextMenu();
        InitializeHpVideoPlayer();
        UpdateHpVideoByCurrentHealth(force: true);
        
        // В WebGL UI элементы инициализируются асинхронно
        // Инициализируем слоты сундука со стартовой задержкой
        if (playerInventory != null)
        {
            playerInventory.StartCoroutine(DelayedChestInitialization());
        }
    }

    private System.Collections.IEnumerator DelayedChestInitialization()
    {
        yield return new WaitForEndOfFrame();
        InitializeChestSlots();
    }

    private void Update()
    {
        if (IsInventoryOpen())
            UpdateHpVideoByCurrentHealth();

        if (IsInventoryOpen() && !isCombineSelectionMode)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                SelectNextInventoryItem(scroll > 0f ? -1 : 1);
        }

        if (contextMenuPanel != null && contextMenuPanel.activeSelf && Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    contextMenuPanel.GetComponent<RectTransform>(), Input.mousePosition, null))
            {
                HideContextMenu();
            }
        }

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

        // Gamepad handling for quick chest/inventory toggle via gamepad
        if (Gamepad.current != null)
        {
            if (!IsChestUIOpen() && IsInventoryOpen())
            {
                bool rightTriggerPressed = Gamepad.current.rightTrigger.ReadValue() >= triggerSwitchThreshold;
                if (rightTriggerPressed && !rightTriggerWasPressed && playerInventory != null && playerInventory.IsNearChest())
                {
                    OpenChestUI(playerInventory.GetNearbyChest());
                }

                rightTriggerWasPressed = rightTriggerPressed;
                leftTriggerWasPressed = Gamepad.current.leftTrigger.ReadValue() >= triggerSwitchThreshold;
            }
        }
    }

    private void ApplyChestCursorState(bool active)
    {
        if (active)
        {
            chestCursorWasVisible = Cursor.visible;
            chestCursorWasLocked = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            chestGamepadCursorInitialized = false;
            return;
        }

        Cursor.visible = chestCursorWasVisible;
        Cursor.lockState = chestCursorWasLocked;
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
        slotObjects = new GameObject[inventoryData.maxSlots];
        storeButtons = new Button[inventoryData.maxSlots];
        destroyButtons = new Button[inventoryData.maxSlots];
        outlines = new UIOutline[inventoryData.maxSlots];

        for (int i = 0; i < inventoryData.maxSlots; i++)
        {
            GameObject slot = Instantiate(slotPrefab, gridTransform);
            slot.name = $"Slot_{i}";
            slotObjects[i] = slot;

            Button slotButton = slot.GetComponent<Button>();
            if (slotButton == null)
                slotButton = slot.AddComponent<Button>();

            slotButton.transition = Selectable.Transition.None;
            int slotIndex = i;

            InventorySlotPointerHandler pointerHandler = slot.GetComponent<InventorySlotPointerHandler>();
            if (pointerHandler == null)
                pointerHandler = slot.AddComponent<InventorySlotPointerHandler>();

            pointerHandler.SlotIndex = slotIndex;
            pointerHandler.OnSlotPointerClick = OnInventorySlotPointerClicked;
            
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
                int storeSlotIndex = i;
                storeButton.onClick.AddListener(() => OnStoreButtonClicked(storeSlotIndex));
                
                // Устанавливаем позицию кнопки Store
                RectTransform storeRect = storeButton.GetComponent<RectTransform>();
                if (storeRect != null)
                {
                    storeRect.anchoredPosition = storeButtonPosition;
                }
                
                storeButton.gameObject.SetActive(false);
            }

            Button destroyButton = FindOrCreateButton(slot, "DestroyButton", destroyButtonPrefab);
            destroyButtons[i] = destroyButton;
            if (destroyButton != null)
            {
                int destroySlotIndex = i;
                destroyButton.onClick.AddListener(() => OnDestroyButtonClicked(destroySlotIndex));
                
                // Устанавливаем позицию кнопки Destroy
                RectTransform destroyRect = destroyButton.GetComponent<RectTransform>();
                if (destroyRect != null)
                {
                    destroyRect.anchoredPosition = destroyButtonPosition;
                }
                
                destroyButton.gameObject.SetActive(false);
            }

            UIOutline outline = icon?.GetComponent<UIOutline>();
            if (outline != null)
            {
                outlines[i] = outline;
                outline.effectColor = outlineColor;
                outline.enabled = false;
            }
        }

        UpdateInventoryUI();
    }

    private void InitializeContextMenu()
    {
        if (inventoryCanvas == null)
            return;

        if (contextMenuPanel == null)
            contextMenuPanel = CreateRuntimeContextMenuPanel();

        if (contextMenuPanel == null)
            return;

        if (contextSelectButton == null)
            contextSelectButton = FindButtonInContextMenu("SelectButton");
        if (contextCombineButton == null)
            contextCombineButton = FindButtonInContextMenu("CombineButton");
        if (contextDestroyButton == null)
            contextDestroyButton = FindButtonInContextMenu("DestroyButton");
        if (contextCancelButton == null)
            contextCancelButton = FindButtonInContextMenu("CancelButton");

        if (contextSelectButton != null)
        {
            contextSelectButton.onClick.RemoveAllListeners();
            contextSelectButton.onClick.AddListener(OnContextSelectClicked);
        }

        if (contextCombineButton != null)
        {
            contextCombineButton.onClick.RemoveAllListeners();
            contextCombineButton.onClick.AddListener(OnContextCombineClicked);
        }

        if (contextDestroyButton != null)
        {
            contextDestroyButton.onClick.RemoveAllListeners();
            contextDestroyButton.onClick.AddListener(OnContextDestroyClicked);
        }

        if (contextCancelButton != null)
        {
            contextCancelButton.onClick.RemoveAllListeners();
            contextCancelButton.onClick.AddListener(HideContextMenu);
        }

        RebuildContextMenuButtons();

        contextMenuPanel.SetActive(false);
    }

    private void RebuildContextMenuButtons()
    {
        contextMenuButtons.Clear();

        if (contextSelectButton != null)
            contextMenuButtons.Add(contextSelectButton);
        if (contextCombineButton != null)
            contextMenuButtons.Add(contextCombineButton);
        if (contextDestroyButton != null)
            contextMenuButtons.Add(contextDestroyButton);
        if (contextCancelButton != null)
            contextMenuButtons.Add(contextCancelButton);

        if (contextMenuButtons.Count > 0)
            contextMenuSelectionIndex = Mathf.Clamp(contextMenuSelectionIndex, 0, contextMenuButtons.Count - 1);
        else
            contextMenuSelectionIndex = 0;
    }

    private GameObject CreateRuntimeContextMenuPanel()
    {
        GameObject panelObj = new GameObject("InventoryContextMenu");
        panelObj.transform.SetParent(inventoryCanvas.transform, false);

        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.85f);

        VerticalLayoutGroup layout = panelObj.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;

        ContentSizeFitter fitter = panelObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);

        contextSelectButton = CreateContextMenuButton(panelObj.transform, "SelectButton", "Выбрать");
        contextCombineButton = CreateContextMenuButton(panelObj.transform, "CombineButton", "Соединить");
        contextDestroyButton = CreateContextMenuButton(panelObj.transform, "DestroyButton", "Уничтожить");
        contextCancelButton = CreateContextMenuButton(panelObj.transform, "CancelButton", "Отмена");

        return panelObj;
    }

    private Button CreateContextMenuButton(Transform parent, string buttonName, string label)
    {
        GameObject buttonObj = new GameObject(buttonName);
        buttonObj.transform.SetParent(parent, false);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = contextMenuButtonColor;

        Button button = buttonObj.AddComponent<Button>();

        LayoutElement element = buttonObj.AddComponent<LayoutElement>();
        element.preferredWidth = 180f;
        element.preferredHeight = 34f;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private Button FindButtonInContextMenu(string buttonName)
    {
        if (contextMenuPanel == null)
            return null;

        return contextMenuPanel.transform.Find(buttonName)?.GetComponent<Button>();
    }

    private void OnInventorySlotPointerClicked(int slotIndex, PointerEventData.InputButton mouseButton)
    {
        var slots = inventoryData != null ? inventoryData.GetSlots() : null;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return;

        InventoryItem clickedItem = slots[slotIndex];
        bool hasItem = clickedItem != null && clickedItem.type != InventoryItem.ItemType.Empty;

        if (isCombineSelectionMode)
        {
            if (mouseButton == PointerEventData.InputButton.Left || mouseButton == PointerEventData.InputButton.Right)
            {
                TryCombineWithSecondSlot(slotIndex);
            }
            return;
        }

        if (!hasItem)
        {
            HideContextMenu();
            return;
        }

        if (mouseButton == PointerEventData.InputButton.Right)
        {
            ShowContextMenuForSlot(slotIndex);
            return;
        }

        if (mouseButton == PointerEventData.InputButton.Left)
        {
            HideContextMenu();
            OnInventorySlotClicked(slotIndex);
        }
    }

    private void ShowContextMenuForSlot(int slotIndex, bool centerOnScreen = false)
    {
        if (contextMenuPanel == null)
            return;

        contextMenuSlotIndex = slotIndex;
        contextMenuSelectionIndex = 0;
        contextMenuPanel.SetActive(true);

        RectTransform panelRect = contextMenuPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.position = centerOnScreen
                ? new Vector3(Screen.width * 0.5f - 120f, Screen.height * 0.5f, 0f)
                : Input.mousePosition;
        }

        UpdateContextMenuSelectionVisual();
    }

    private void HideContextMenu()
    {
        if (contextMenuPanel != null)
            contextMenuPanel.SetActive(false);

        contextMenuSlotIndex = -1;
    }

    public bool IsContextMenuOpen()
    {
        return contextMenuPanel != null && contextMenuPanel.activeSelf;
    }

    public bool ShowContextMenuForActiveItem()
    {
        return ShowContextMenuForActiveItem(false);
    }

    public bool ShowContextMenuForActiveItem(bool centerOnScreen)
    {
        if (playerInventory == null || inventoryData == null)
            return false;

        int slotIndex = playerInventory.activeItemIndex;
        var slots = inventoryData.GetSlots();
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count)
            return false;

        InventoryItem activeItem = slots[slotIndex];
        if (activeItem == null || activeItem.type == InventoryItem.ItemType.Empty)
            return false;

        ShowContextMenuForSlot(slotIndex, centerOnScreen);
        return true;
    }

    public void MoveContextMenuSelection(int direction)
    {
        if (!IsContextMenuOpen() || contextMenuButtons.Count == 0)
            return;

        if (direction == 0)
            return;

        contextMenuSelectionIndex = (contextMenuSelectionIndex + direction + contextMenuButtons.Count) % contextMenuButtons.Count;
        UpdateContextMenuSelectionVisual();
    }

    public bool IsCombineSelectionMode()
    {
        return isCombineSelectionMode;
    }

    public void TriggerContextMenuSelect()
    {
        TriggerSelectedContextMenuAction();
    }

    public void TriggerContextMenuCombine()
    {
        OnContextCombineClicked();
    }

    public void TriggerContextMenuDestroy()
    {
        OnContextDestroyClicked();
    }

    public void TriggerContextMenuCancel()
    {
        HideContextMenu();
    }

    public bool TryCombineWithActiveItem()
    {
        if (!isCombineSelectionMode || playerInventory == null || inventoryData == null)
            return false;

        int activeIndex = playerInventory.activeItemIndex;
        var slots = inventoryData.GetSlots();
        if (slots == null || activeIndex < 0 || activeIndex >= slots.Count)
            return false;

        InventoryItem activeItem = slots[activeIndex];
        if (activeItem == null || activeItem.type == InventoryItem.ItemType.Empty)
            return false;

        if (activeIndex == combineSourceSlotIndex)
        {
            if (activeItemInfoText != null)
                activeItemInfoText.text = "Выберите другой предмет для соединения";
            return false;
        }

        TryCombineWithSecondSlot(activeIndex);
        return true;
    }

    public void CancelCombineSelectionMode()
    {
        isCombineSelectionMode = false;
        combineSourceSlotIndex = -1;

        if (activeItemInfoText != null)
            activeItemInfoText.text = "Соединение отменено";

        UpdateInventoryUI();
    }

    private void TriggerSelectedContextMenuAction()
    {
        if (!IsContextMenuOpen() || contextMenuButtons.Count == 0)
            return;

        contextMenuSelectionIndex = Mathf.Clamp(contextMenuSelectionIndex, 0, contextMenuButtons.Count - 1);

        Button selectedButton = contextMenuButtons[contextMenuSelectionIndex];
        if (selectedButton == contextSelectButton)
        {
            OnContextSelectClicked();
            return;
        }

        if (selectedButton == contextCombineButton)
        {
            OnContextCombineClicked();
            return;
        }

        if (selectedButton == contextDestroyButton)
        {
            OnContextDestroyClicked();
            return;
        }

        HideContextMenu();
    }

    private void OnContextSelectClicked()
    {
        if (contextMenuSlotIndex < 0)
            return;

        OnInventorySlotClicked(contextMenuSlotIndex);
        HideContextMenu();
    }

    private void OnContextCombineClicked()
    {
        if (contextMenuSlotIndex < 0)
            return;

        isCombineSelectionMode = true;
        combineSourceSlotIndex = contextMenuSlotIndex;
        HideContextMenu();

        if (activeItemInfoText != null)
            activeItemInfoText.text = "Режим соединения: выберите второй предмет";
    }

    private void UpdateContextMenuSelectionVisual()
    {
        if (contextMenuButtons.Count == 0)
            return;

        for (int i = 0; i < contextMenuButtons.Count; i++)
        {
            Button button = contextMenuButtons[i];
            if (button == null)
                continue;

            Image image = button.GetComponent<Image>();
            if (image == null)
                continue;

            image.color = i == contextMenuSelectionIndex
                ? contextMenuButtonHoverColor
                : contextMenuButtonColor;
        }
    }

    private void OnContextDestroyClicked()
    {
        if (contextMenuSlotIndex < 0)
            return;

        OnDestroyButtonClicked(contextMenuSlotIndex);
        HideContextMenu();
    }

    private void TryCombineWithSecondSlot(int secondSlotIndex)
    {
        if (!isCombineSelectionMode)
            return;

        int sourceSlot = combineSourceSlotIndex;
        if (sourceSlot < 0 || secondSlotIndex < 0 || sourceSlot == secondSlotIndex)
        {
            if (activeItemInfoText != null)
                activeItemInfoText.text = "Соединение отменено";
            UpdateInventoryUI();
            return;
        }

        if (inventoryData == null)
            return;

        InventoryItem firstItem = inventoryData.GetItemAt(sourceSlot);
        InventoryItem secondItem = inventoryData.GetItemAt(secondSlotIndex);

        if (firstItem == null || secondItem == null ||
            firstItem.type == InventoryItem.ItemType.Empty || secondItem.type == InventoryItem.ItemType.Empty)
        {
            UpdateInventoryUI();
            return;
        }

        ItemCombineRecipe recipe = FindCombineRecipe(firstItem, secondItem);
        if (recipe == null || recipe.resultItem == null)
        {
            if (activeItemInfoText != null)
                activeItemInfoText.text = $"Нельзя соединить: {firstItem.itemName} + {secondItem.itemName}";

            AnimateCraftFailText();
            UpdateInventoryUI();
            return;
        }

        isCombineSelectionMode = false;
        combineSourceSlotIndex = -1;

        inventoryData.ClearSlot(sourceSlot);
        inventoryData.ClearSlot(secondSlotIndex);
        inventoryData.SetItemAt(sourceSlot, recipe.resultItem);

        playerInventory.SetActiveItemByIndex(sourceSlot);
        UpdateInventoryUI();

        if (activeItemInfoText != null)
            activeItemInfoText.text = $"Создано: {recipe.resultItem.itemName}";
    }

    private ItemCombineRecipe FindCombineRecipe(InventoryItem first, InventoryItem second)
    {
        if (combineRecipes == null)
            return null;

        for (int i = 0; i < combineRecipes.Count; i++)
        {
            ItemCombineRecipe recipe = combineRecipes[i];
            if (recipe == null)
                continue;

            bool directMatch = recipe.firstItem == first && recipe.secondItem == second;
            bool reverseMatch = recipe.firstItem == second && recipe.secondItem == first;

            if (directMatch || reverseMatch)
                return recipe;
        }

        return null;
    }

    private void AnimateCraftFailText()
    {
        if (activeItemInfoText == null)
            return;

        RectTransform textRect = activeItemInfoText.rectTransform;
        if (textRect == null)
            return;

        if (craftFailTextTween != null && craftFailTextTween.IsActive())
            craftFailTextTween.Kill();

        textRect.DOKill();
        Vector2 startPos = textRect.anchoredPosition;

        craftFailTextTween = textRect
            .DOShakeAnchorPos(
                Mathf.Max(0.05f, craftFailShakeDuration),
                new Vector2(Mathf.Max(1f, craftFailShakeDistance), 0f),
                Mathf.Max(2, craftFailShakeVibrato),
                90f,
                false,
                true)
            .SetUpdate(true)
            .OnComplete(() => textRect.anchoredPosition = startPos)
            .OnKill(() => textRect.anchoredPosition = startPos);
    }

    public void SetOutlineColor(Color color)
    {
        outlineColor = color;
        ApplyOutlineColor();
    }

    private void ApplyOutlineColor()
    {
        if (outlines == null) return;

        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null)
            {
                outlines[i].effectColor = outlineColor;
            }
        }
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
        chestOutlines = new UIOutline[16];

        for (int i = 0; i < 16; i++)
        {
            GameObject slot = Instantiate(slotPrefab, chestGridTransform);
            slot.name = $"ChestSlot_{i}";
            
            Image icon = slot.transform.Find("ItemIcon")?.GetComponent<Image>();
            chestSlotIcons[i] = icon;
            if (icon != null)
            {
                icon.enabled = false;
                chestOutlines[i] = icon.GetComponent<UIOutline>();
                if (chestOutlines[i] != null)
                {
                    chestOutlines[i].effectColor = Color.red;
                    chestOutlines[i].enabled = false;
                }
            }

            Button takeButton = FindOrCreateButton(slot, "TakeButton", takeButtonPrefab);
            chestTakeButtons[i] = takeButton;
            if (takeButton != null)
            {
                int slotIndex = i;
                takeButton.onClick.AddListener(() => OnChestTakeButtonClicked(slotIndex));
                
                // Устанавливаем позицию кнопки Take
                RectTransform takeRect = takeButton.GetComponent<RectTransform>();
                if (takeRect != null)
                {
                    takeRect.anchoredPosition = takeButtonPosition;
                }
                
                takeButton.gameObject.SetActive(false);
            }

            Button destroyButton = FindOrCreateButton(slot, "DestroyButton", destroyButtonPrefab);
            chestDestroyButtons[i] = destroyButton;
            if (destroyButton != null)
            {
                int slotIndex = i;
                destroyButton.onClick.AddListener(() => OnChestDestroyButtonClicked(slotIndex));
                
                // Устанавливаем позицию кнопки Destroy в сундуке
                RectTransform destroyRect = destroyButton.GetComponent<RectTransform>();
                if (destroyRect != null)
                {
                    destroyRect.anchoredPosition = destroyButtonPosition;
                }
                
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
            leftArrowButton.onClick.RemoveAllListeners();
            leftArrowButton.onClick.AddListener(PreviousInventoryPage);
        }
        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.RemoveAllListeners();
            rightArrowButton.onClick.AddListener(NextInventoryPage);
        }

        if (toChestButton != null)
        {
            toChestButton.onClick.AddListener(GoToChestFromButton);
            toChestButton.gameObject.SetActive(false);
        }

        if (backFromChestButton != null)
        {
            backFromChestButton.onClick.AddListener(GoToInventoryFromButton);
        }
    }

    private Button FindButtonByName(string buttonName)
    {
        GameObject obj = GameObject.Find(buttonName);
        if (obj == null)
        {
            Debug.LogWarning($"[InventoryUI] Кнопка {buttonName} не найдена в сцене");
            return null;
        }

        Button button = obj.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"[InventoryUI] Объект {buttonName} найден, но Button компонента нет");
            return null;
        }

        return button;
    }

    public void UpdateInventoryUI()
    {
        if (inventoryData == null || slotIcons == null) return;

        var slots = inventoryData.GetSlots();
        int activeIndex = playerInventory.activeItemIndex;
        bool nearChest = playerInventory.IsNearChest();
        if (useInventoryPagination && activeIndex >= 0)
        {
            int perPage = Mathf.Max(1, slotsPerPage);
            currentInventoryPage = activeIndex / perPage;
        }

        if (activeItemInfoText != null)
        {
            string nextInfoText;
            if (activeIndex >= 0 && activeIndex < slots.Count && slots[activeIndex] != null && slots[activeIndex].type != InventoryItem.ItemType.Empty)
            {
                var activeItem = slots[activeIndex];
                if (activeItem.useCustomDescription && !string.IsNullOrWhiteSpace(activeItem.customDescription))
                {
                    nextInfoText = $"Тип: {activeItem.itemName}\n{activeItem.customDescription}";
                }
                else
                {
                    switch (activeItem.type)
                    {
                        case InventoryItem.ItemType.Gun:
                            nextInfoText = $"Тип: {activeItem.itemName}\nВинтовка, что использует патроны 10мм. Рабочая лошадка. Без модификаций.";
                            break;
                        case InventoryItem.ItemType.Pistol:
                            nextInfoText = $"Тип: {activeItem.itemName}\nЛёгкий пистолет с патронами 9мм. Быстрая перезарядка, низкий урон.";
                            break;
                        case InventoryItem.ItemType.Disketa:
                        case InventoryItem.ItemType.Cassette:
                            nextInfoText = $"Тип: {activeItem.itemName}\nНоситель данных для терминалов - самый большой компьютер в комплексе";
                            break;
                        case InventoryItem.ItemType.PistolAmmo:
                            nextInfoText = $"Тип: {activeItem.itemName}\nБоеприпасы для пистолета. Доступно: <b>{PlayerAmmoData.pistolReserve}</b>.";
                            break;
                        case InventoryItem.ItemType.ShotgunAmmo:
                            nextInfoText = $"Тип: {activeItem.itemName}\nБоеприпасы для дробовика. Доступно: <b>{PlayerAmmoData.gunReserve}</b>.";
                            break;
                        case InventoryItem.ItemType.Medkit:
                            int healAmount = activeItem.medkitProfile != null ? activeItem.medkitProfile.HealAmount : 0;
                            if (healAmount > 0)
                                nextInfoText = $"Тип: {activeItem.itemName}\nАптечка. Восстанавливает <b>{healAmount}</b> HP.";
                            else
                                nextInfoText = $"Тип: {activeItem.itemName}\nАптечка. Профиль лечения не назначен.";
                            break;
                        default:
                            nextInfoText = $"Активный предмет: {activeItem.itemName}\nТип: {activeItem.type}";
                            break;
                    }
                }
            }
            else
            {
                nextInfoText = "Ничего не выбрано";
            }

            if (nextInfoText != lastActiveItemInfoText || activeIndex != lastActiveItemIndex)
            {
                activeItemInfoText.text = nextInfoText;
                lastActiveItemInfoText = nextInfoText;
                lastActiveItemIndex = activeIndex;
            }
        }

        int totalSlots = slotIcons.Length;
        int pageStart = GetInventoryPageStart(totalSlots);
        int pageEnd = Mathf.Min(totalSlots, pageStart + Mathf.Max(1, slotsPerPage));

        for (int i = 0; i < slotIcons.Length; i++)
        {
            bool inPage = !useInventoryPagination || (i >= pageStart && i < pageEnd);
            if (slotObjects != null && slotObjects[i] != null)
                slotObjects[i].SetActive(inPage);

            if (!inPage)
            {
                if (outlines[i] != null)
                    outlines[i].enabled = false;
                continue;
            }

            if (i < slots.Count && slots[i] != null && slots[i].type != InventoryItem.ItemType.Empty)
            {
                if (slotIcons[i] != null)
                {
                    slotIcons[i].sprite = slots[i].icon;
                    slotIcons[i].enabled = true;
                }

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
            if (nearChest != lastNearChest)
                toChestButton.gameObject.SetActive(nearChest);
        }

        lastNearChest = nearChest;
        UpdateInventoryPaginationButtons(totalSlots);
    }

    private int GetInventoryPageStart(int totalSlots)
    {
        if (!useInventoryPagination)
            return 0;

        int perPage = Mathf.Max(1, slotsPerPage);
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalSlots / perPage));
        currentInventoryPage = Mathf.Clamp(currentInventoryPage, 0, totalPages - 1);
        return currentInventoryPage * perPage;
    }

    private void UpdateInventoryPaginationButtons(int totalSlots)
    {
        if (!useInventoryPagination)
        {
            if (leftArrowButton != null) leftArrowButton.gameObject.SetActive(false);
            if (rightArrowButton != null) rightArrowButton.gameObject.SetActive(false);
            return;
        }

        int perPage = Mathf.Max(1, slotsPerPage);
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalSlots / perPage));
        bool showArrows = totalPages > 1;

        if (leftArrowButton != null)
            leftArrowButton.gameObject.SetActive(showArrows);
        if (rightArrowButton != null)
            rightArrowButton.gameObject.SetActive(showArrows);
    }

    private void NextInventoryPage()
    {
        if (!useInventoryPagination || inventoryData == null)
            return;

        int perPage = Mathf.Max(1, slotsPerPage);
        int totalSlots = inventoryData.maxSlots;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalSlots / perPage));
        currentInventoryPage = (currentInventoryPage + 1) % totalPages;
        UpdateInventoryUI();
    }

    private void PreviousInventoryPage()
    {
        if (!useInventoryPagination || inventoryData == null)
            return;

        int perPage = Mathf.Max(1, slotsPerPage);
        int totalSlots = inventoryData.maxSlots;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalSlots / perPage));
        currentInventoryPage = (currentInventoryPage - 1 + totalPages) % totalPages;
        UpdateInventoryUI();
    }

    private void SelectNextInventoryItem(int direction)
    {
        if (inventoryData == null || playerInventory == null)
            return;

        var slots = inventoryData.GetSlots();
        if (slots == null || slots.Count == 0)
            return;

        int count = slots.Count;
        int startIndex = Mathf.Clamp(playerInventory.activeItemIndex, 0, count - 1);
        int index = startIndex;

        for (int i = 0; i < count; i++)
        {
            index = (index + direction + count) % count;
            if (slots[index] != null && slots[index].type != InventoryItem.ItemType.Empty)
            {
                playerInventory.SetActiveItemByIndex(index);

                if (useInventoryPagination)
                {
                    int perPage = Mathf.Max(1, slotsPerPage);
                    currentInventoryPage = index / perPage;
                }

                UpdateInventoryUI();
                return;
            }
        }
    }

    private void InitializeHpVideoPlayer()
    {
        if (hpVideoPlayer == null)
            return;

        hpVideoPlayer.playOnAwake = false;
        hpVideoPlayer.waitForFirstFrame = true;
        hpVideoPlayer.isLooping = loopHpVideo;
        hpVideoPlayer.source = VideoSource.VideoClip;

        hpVideoPlayer.prepareCompleted -= OnHpVideoPrepared;
        hpVideoPlayer.errorReceived -= OnHpVideoError;
        hpVideoPlayer.prepareCompleted += OnHpVideoPrepared;
        hpVideoPlayer.errorReceived += OnHpVideoError;
    }

    private void UpdateHpVideoByCurrentHealth(bool force = false)
    {
        if (hpVideoPlayer == null)
            return;

        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth == null)
            return;

        int hp = Mathf.Clamp(playerHealth.CurrentHealth, 0, Mathf.Max(1, playerHealth.MaxHealth));

        HpVideoState targetState;
        VideoClip targetClip;

        // Фиксированные диапазоны: 100..51, 50..26, 25..0.
        if (hp >= 51)
        {
            targetState = HpVideoState.Hp100;
            targetClip = hp100Clip;
        }
        else if (hp >= 26)
        {
            targetState = HpVideoState.Hp50;
            targetClip = hp50Clip;
        }
        else
        {
            targetState = HpVideoState.Hp25;
            targetClip = hp25Clip;
        }

        if (!force && targetState == currentHpVideoState)
        {
            // Состояние не изменилось, но если плеер по какой-то причине не играет,
            // пробуем восстановить воспроизведение текущего клипа.
            if (hpVideoPlayer.clip == targetClip && targetClip != null && !hpVideoPlayer.isPlaying)
            {
                if (hpVideoPlayer.isPrepared)
                    hpVideoPlayer.Play();
                else
                    hpVideoPlayer.Prepare();
            }
            return;
        }

        currentHpVideoState = targetState;

        if (targetClip == null)
        {
            Debug.LogWarning($"[InventoryUI] Не назначен HP-клип для состояния {targetState}");
            if (hpVideoPlayer.isPlaying)
                hpVideoPlayer.Stop();
            currentHpClip = null;
            return;
        }

        if (!force && currentHpClip == targetClip)
            return;

        currentHpClip = targetClip;
        hpVideoPlayer.isLooping = loopHpVideo;
        hpVideoPlayer.source = VideoSource.VideoClip;
        hpVideoPlayer.clip = targetClip;
        hpVideoPlayer.Stop();
        hpVideoPlayer.Prepare();
    }

    private void OnHpVideoPrepared(VideoPlayer source)
    {
        if (source == null)
            return;

        if (hpVideoRawImage != null)
            hpVideoRawImage.texture = source.texture;

        source.Play();
    }

    private void OnHpVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[InventoryUI] Ошибка проигрывания HP-видео: {message}");
    }

    private void OnDestroy()
    {
        if (craftFailTextTween != null && craftFailTextTween.IsActive())
            craftFailTextTween.Kill();

        if (hpVideoPlayer == null)
            return;

        hpVideoPlayer.prepareCompleted -= OnHpVideoPrepared;
        hpVideoPlayer.errorReceived -= OnHpVideoError;
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

    private void OnInventorySlotClicked(int slotIndex)
    {
        if (playerInventory == null || inventoryData == null)
            return;

        var slots = inventoryData.GetSlots();
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return;

        if (slots[slotIndex] == null || slots[slotIndex].type == InventoryItem.ItemType.Empty)
            return;

        InventoryItem clickedItem = slots[slotIndex];
        bool isDoubleClick = lastClickedSlotIndex == slotIndex && (Time.unscaledTime - lastClickTime) <= Mathf.Max(0.05f, medkitDoubleClickThreshold);

        playerInventory.SetActiveItemByIndex(slotIndex);

        // Только аптечка использует двойной ЛКМ.
        if (clickedItem.type == InventoryItem.ItemType.Medkit && isDoubleClick)
        {
            playerInventory.UseMedkitFromInventory();
        }

        lastClickedSlotIndex = slotIndex;
        lastClickTime = Time.unscaledTime;
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
        if (IsChestUIOpen())
        {
            CloseChestUI();
            return;
        }
        
        if (inventoryCanvas != null)
        {
            bool isActive = !inventoryCanvas.activeSelf;
            inventoryCanvas.SetActive(isActive);
            
            if (isActive)
            {
                playerInventory.AutoSelectActiveItem();
                UpdateInventoryUI();
                UpdateHpVideoByCurrentHealth(force: true);
                isCombineSelectionMode = false;
                combineSourceSlotIndex = -1;
                HideContextMenu();
                // Cursor.lockState = CursorLockMode.None;
                // Cursor.visible = true;
            }
            else
            {
                isCombineSelectionMode = false;
                combineSourceSlotIndex = -1;
                HideContextMenu();
                if (!IsChestUIOpen()) // Если сундук не открыт, сбрасываем флаг
                {
                    // Cursor.lockState = CursorLockMode.Locked;
                    // Cursor.visible = false;
                }
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

        if (IsChestUIOpen())
        {
            CloseChestUI();
        }

        isCombineSelectionMode = false;
        combineSourceSlotIndex = -1;
        HideContextMenu();

        wasInventoryOpenBeforeChest = IsInventoryOpen();
        if (inventoryCanvas != null && wasInventoryOpenBeforeChest)
        {
            inventoryCanvas.SetActive(false);
        }

        currentChest = chest;
        currentChest.SetOpenState(true);
        leftTriggerWasPressed = false;
        rightTriggerWasPressed = false;
        if (chestCanvas != null)
        {
            chestCanvas.SetActive(true);
            UpdateChestUI();
            ApplyChestCursorState(true);
        }
    }

    public void OpenChestUIFromInventory()
    {
        if (playerInventory.IsNearChest())
        {
            Chest chest = playerInventory.GetNearbyChest();
            OpenChestUI(chest);
        }
    }

    public void GoToChestFromButton()
    {
        if (!playerInventory.IsNearChest())
            return;

        OpenChestUI(playerInventory.GetNearbyChest());
    }

    public void GoToInventoryFromButton()
    {
        SwitchToInventoryUI();
    }

    private void SwitchToInventoryUI()
    {
        if (chestCanvas != null)
        {
            chestCanvas.SetActive(false);
        }

        if (currentChest != null)
        {
            currentChest.SetOpenState(false);
        }

        if (inventoryCanvas != null)
        {
            inventoryCanvas.SetActive(true);
            UpdateInventoryUI();
            UpdateHpVideoByCurrentHealth(force: true);
        }

        if (currentChest != null)
            currentChest.SetOpenState(false);

        ApplyChestCursorState(false);

        isCombineSelectionMode = false;
        combineSourceSlotIndex = -1;
        HideContextMenu();

        wasInventoryOpenBeforeChest = false;
    }

    public void CloseChestUI()
    {
        if (chestCanvas != null)
        {
            chestCanvas.SetActive(false);
        }

        if (currentChest != null)
        {
            currentChest.SetOpenState(false);
        }

        currentChest = null;
        leftTriggerWasPressed = false;
        rightTriggerWasPressed = false;

        isCombineSelectionMode = false;
        combineSourceSlotIndex = -1;
        HideContextMenu();

        if (inventoryCanvas != null && wasInventoryOpenBeforeChest)
        {
            inventoryCanvas.SetActive(true);
            UpdateInventoryUI();
            UpdateHpVideoByCurrentHealth(force: true);
        }
        else
        {
            if (inventoryCanvas != null)
            {
                inventoryCanvas.SetActive(false);
            }
            ApplyChestCursorState(false);
        }

        wasInventoryOpenBeforeChest = false;
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