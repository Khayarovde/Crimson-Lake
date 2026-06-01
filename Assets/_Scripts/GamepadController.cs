using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class GamepadController : MonoBehaviour
{
    private enum DeviceLockState
    {
        Unselected,
        KeyboardMouse,
        Gamepad
    }

    [Header("References")]
    [SerializeField] private TankController tankController;
    [SerializeField] private WeaponHandler weaponHandler;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private InventoryUI inventoryUI;

    [Header("Input")]
    [SerializeField, Range(0.05f, 0.5f)] private float moveDeadzone = 0.2f;
    [SerializeField, Range(0.05f, 0.5f)] private float lookDeadzone = 0.2f;
    [SerializeField, Range(0.05f, 0.5f)] private float triggerThreshold = 0.2f;
    [SerializeField, Range(200f, 2500f)] private float uiCursorMoveSpeed = 1100f;
    [SerializeField, Range(0f, 80f)] private float uiCursorEdgePadding = 18f;

    [Header("Device Select Screen")]
    [SerializeField] private bool showDeviceSelectScreen = true;
    [SerializeField] private string deviceSelectMessage = "Нажмите любую клавишу или кнопку геймпада";
    [SerializeField] private string deviceSelectedKeyboardMessage = "Клавиатура и мышь";
    [SerializeField] private string deviceSelectedGamepadMessage = "Геймпад";
    [SerializeField, Range(0.5f, 3f)] private float deviceSelectedMessageDuration = 1.5f;

    [Header("HUD")]
    [SerializeField] private bool showInputModeLabel = true;
    [SerializeField] private string gamepadModeLabel = "Подключен геймпад";
    [SerializeField] private string keyboardMouseModeLabel = "Клава + мышь";
    [SerializeField, Range(18f, 42f)] private float inputModeLabelFontSize = 22f;
    [SerializeField, Range(0f, 80f)] private float inputModeLabelBottomOffset = 24f;
    [SerializeField, Range(180f, 420f)] private float inputModeLabelWidth = 280f;
    [SerializeField, Range(28f, 72f)] private float inputModeLabelHeight = 36f;
    [SerializeField] private TextMeshProUGUI inputModeLabelText;

    private DeviceLockState deviceLock = DeviceLockState.Unselected;
    private RectTransform inputModeLabelRect;
    private int externalCursorOverrideCount;
    private string deviceSelectedMessageOverride;
    private Coroutine deviceSelectedMessageRoutine;

    #region Gamepad UI Cursor
    // Used only while deviceLock == DeviceLockState.Gamepad.
    private Vector2 virtualCursorPosition;
    private bool virtualCursorInitialized;

    private void MoveUiCursor(Vector2 lookInput)
    {
        if (!virtualCursorInitialized)
        {
            virtualCursorPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
            virtualCursorInitialized = true;
        }

        if (lookInput.sqrMagnitude < lookDeadzone * lookDeadzone)
            return;

        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        Vector2 nextPosition = virtualCursorPosition + lookInput * uiCursorMoveSpeed * deltaTime;

        float maxX = Mathf.Max(uiCursorEdgePadding, Screen.width - uiCursorEdgePadding);
        float maxY = Mathf.Max(uiCursorEdgePadding, Screen.height - uiCursorEdgePadding);
        nextPosition.x = Mathf.Clamp(nextPosition.x, uiCursorEdgePadding, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, uiCursorEdgePadding, maxY);

        virtualCursorPosition = nextPosition;
    }
    #endregion

    private ItemPickup[] cachedItemPickups;
    private MedkitPickup[] cachedMedkitPickups;
    private AmmoPickup[] cachedAmmoPickups;
    private EnemyPickupInteraction[] cachedEnemyPickups;
    private Interact[] cachedInteractables;

    private void Awake()
    {
        ResolveReferences();
        deviceLock = showDeviceSelectScreen ? DeviceLockState.Unselected : DeviceLockState.KeyboardMouse;
        RefreshInteractables();
        EnsureInputModeLabel();
        UpdateInputModeLabel();
    }

    private void OnDisable()
    {
        externalCursorOverrideCount = 0;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ClearGamepadState();
    }

    private void Update()
    {
        bool uiOpen = inventoryUI != null && (inventoryUI.IsInventoryOpen() || inventoryUI.IsChestUIOpen() || inventoryUI.IsContextMenuOpen());
        bool inventoryOpen = inventoryUI != null && inventoryUI.IsInventoryOpen();
        bool chestOpen = inventoryUI != null && inventoryUI.IsChestUIOpen();
        bool contextMenuOpen = inventoryUI != null && inventoryUI.IsContextMenuOpen();

        if (deviceLock == DeviceLockState.Unselected)
        {
            HandleDeviceSelection(uiOpen);
            UpdateInputModeLabel();
            return;
        }

        UpdateCursorState(uiOpen);

        if (deviceLock == DeviceLockState.KeyboardMouse && uiOpen)
            SendMessage("HandleKeyboardUIInput", SendMessageOptions.DontRequireReceiver);

        Gamepad gamepad = Gamepad.current;
        bool gamepadAvailable = gamepad != null;
        bool gamepadActive = IsGamepadModeActive;
        Vector2 moveInput = Vector2.zero;
        Vector2 lookInput = Vector2.zero;
        bool runHeld = false;
        bool aimHeld = false;
        bool fireHeld = false;
        bool reloadHeld = false;

        if (gamepadAvailable)
        {
            moveInput = gamepad.leftStick.ReadValue();
            lookInput = gamepad.rightStick.ReadValue();
            runHeld = gamepad.leftShoulder.isPressed;
            aimHeld = gamepad.leftTrigger.ReadValue() >= triggerThreshold;
            fireHeld = gamepad.rightTrigger.ReadValue() >= triggerThreshold;
            reloadHeld = aimHeld;
        }

        if (tankController != null)
        {
            tankController.SetGamepadModeActive(gamepadActive);
            tankController.SetGamepadMoveInput(gamepadActive && moveInput.sqrMagnitude >= moveDeadzone * moveDeadzone ? moveInput : Vector2.zero);
            tankController.SetGamepadLookInput(gamepadActive && lookInput.sqrMagnitude >= lookDeadzone * lookDeadzone ? lookInput : Vector2.zero);
            tankController.SetGamepadRunHeld(gamepadActive && runHeld);
            tankController.SetGamepadAimHeld(gamepadActive && aimHeld && !inventoryOpen && !chestOpen && !contextMenuOpen);
        }

        if (weaponHandler != null)
        {
            weaponHandler.SetGamepadModeActive(gamepadActive);
            weaponHandler.SetGamepadAimHeld(gamepadActive && aimHeld && !inventoryOpen && !chestOpen && !contextMenuOpen);
            weaponHandler.SetGamepadFireHeld(gamepadActive && fireHeld && !inventoryOpen && !chestOpen && !contextMenuOpen);
        }

        HandleGameplayInput(gamepad, gamepadAvailable, gamepadActive, inventoryOpen, chestOpen, contextMenuOpen, reloadHeld, lookInput);
        UpdateInputModeLabel();
    }

    private void EnsureInputModeLabel()
    {
        if (!showInputModeLabel)
            return;

        if (inputModeLabelText == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject labelObject = new GameObject("InputModeLabel");
                labelObject.transform.SetParent(canvas.transform, false);
                inputModeLabelText = labelObject.AddComponent<TextMeshProUGUI>();
            }
        }

        if (inputModeLabelText == null)
            return;

        inputModeLabelRect = inputModeLabelText.rectTransform;
        inputModeLabelRect.anchorMin = new Vector2(0.5f, 0f);
        inputModeLabelRect.anchorMax = new Vector2(0.5f, 0f);
        inputModeLabelRect.pivot = new Vector2(0.5f, 0f);
        inputModeLabelRect.sizeDelta = new Vector2(inputModeLabelWidth, inputModeLabelHeight);
        inputModeLabelRect.anchoredPosition = new Vector2(0f, inputModeLabelBottomOffset);

        inputModeLabelText.alignment = TextAlignmentOptions.Center;
        inputModeLabelText.fontStyle = FontStyles.Bold;
        inputModeLabelText.fontSize = inputModeLabelFontSize;
        inputModeLabelText.color = Color.white;
    }

    private void UpdateInputModeLabel()
    {
        if (inputModeLabelText == null)
            return;

        if (!showInputModeLabel)
        {
            if (inputModeLabelText.gameObject.activeSelf)
                inputModeLabelText.gameObject.SetActive(false);
            return;
        }

        if (!inputModeLabelText.gameObject.activeSelf)
            inputModeLabelText.gameObject.SetActive(true);

        if (inputModeLabelRect == null)
            inputModeLabelRect = inputModeLabelText.rectTransform;

        inputModeLabelRect.sizeDelta = new Vector2(inputModeLabelWidth, inputModeLabelHeight);
        inputModeLabelRect.anchoredPosition = new Vector2(0f, inputModeLabelBottomOffset);
        inputModeLabelText.fontSize = inputModeLabelFontSize;
        if (!string.IsNullOrEmpty(deviceSelectedMessageOverride))
        {
            inputModeLabelText.text = deviceSelectedMessageOverride;
            return;
        }

        if (deviceLock == DeviceLockState.Unselected && showDeviceSelectScreen)
        {
            inputModeLabelText.text = deviceSelectMessage;
            return;
        }

        inputModeLabelText.text = IsGamepadModeActive ? gamepadModeLabel : keyboardMouseModeLabel;
    }

    private void ResolveReferences()
    {
        if (tankController == null)
            tankController = GetComponent<TankController>();

        if (weaponHandler == null)
            weaponHandler = GetComponent<WeaponHandler>();

        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();

        if (inventoryUI == null)
            inventoryUI = FindFirstObjectByType<InventoryUI>();
    }

    private void ClearGamepadState()
    {
        if (tankController != null)
        {
            tankController.SetGamepadModeActive(false);
            tankController.SetGamepadMoveInput(Vector2.zero);
            tankController.SetGamepadLookInput(Vector2.zero);
            tankController.SetGamepadRunHeld(false);
            tankController.SetGamepadAimHeld(false);
        }

        if (weaponHandler != null)
        {
            weaponHandler.SetGamepadModeActive(false);
            weaponHandler.SetGamepadAimHeld(false);
            weaponHandler.SetGamepadFireHeld(false);
        }
    }

    private void HandleGameplayInput(
        Gamepad gamepad,
        bool gamepadAvailable,
        bool gamepadActive,
        bool inventoryOpen,
        bool chestOpen,
        bool contextMenuOpen,
        bool reloadHeld,
        Vector2 lookInput)
    {
        if (gamepadAvailable && gamepad.startButton.wasPressedThisFrame)
        {
            ToggleInventory();
            return;
        }

        if (gamepadAvailable && !inventoryOpen && !chestOpen && !contextMenuOpen && reloadHeld && gamepad.buttonNorth.wasPressedThisFrame)
        {
            if (weaponHandler != null)
                weaponHandler.RequestGamepadReload();
            return;
        }

        if (HandleUIInput(gamepad, gamepadActive, inventoryOpen, chestOpen, contextMenuOpen, lookInput))
            return;

        if (gamepadActive && gamepad != null)
            HandleWorldInput(gamepad);
    }

    public bool IsGamepadModeActive => deviceLock == DeviceLockState.Gamepad;
    public bool IsDeviceSelected => deviceLock != DeviceLockState.Unselected;

    public void PushExternalCursorOverride()
    {
        externalCursorOverrideCount = Mathf.Max(0, externalCursorOverrideCount + 1);
    }

    public void PopExternalCursorOverride()
    {
        externalCursorOverrideCount = Mathf.Max(0, externalCursorOverrideCount - 1);
    }

    private void HandleDeviceSelection(bool uiOpen)
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && IsGamepadSelectionInput(gamepad))
        {
            SetDeviceLock(DeviceLockState.Gamepad, uiOpen);
            return;
        }

        if (IsKeyboardMouseSelectionInput())
        {
            SetDeviceLock(DeviceLockState.KeyboardMouse, uiOpen);
            return;
        }

        UpdateCursorState(uiOpen);
    }

    private bool IsGamepadSelectionInput(Gamepad gamepad)
    {
        Vector2 moveInput = gamepad.leftStick.ReadValue();
        Vector2 lookInput = gamepad.rightStick.ReadValue();

        if (moveInput.sqrMagnitude >= moveDeadzone * moveDeadzone)
            return true;

        if (lookInput.sqrMagnitude >= lookDeadzone * lookDeadzone)
            return true;

        if (gamepad.leftTrigger.ReadValue() >= triggerThreshold || gamepad.rightTrigger.ReadValue() >= triggerThreshold)
            return true;

        return gamepad.startButton.wasPressedThisFrame ||
               gamepad.selectButton.wasPressedThisFrame ||
               gamepad.buttonSouth.wasPressedThisFrame ||
               gamepad.buttonEast.wasPressedThisFrame ||
               gamepad.buttonWest.wasPressedThisFrame ||
               gamepad.buttonNorth.wasPressedThisFrame ||
               gamepad.leftShoulder.wasPressedThisFrame ||
               gamepad.rightShoulder.wasPressedThisFrame ||
               gamepad.leftStickButton.wasPressedThisFrame ||
               gamepad.rightStickButton.wasPressedThisFrame ||
               gamepad.dpad.left.wasPressedThisFrame ||
               gamepad.dpad.right.wasPressedThisFrame ||
               gamepad.dpad.up.wasPressedThisFrame ||
               gamepad.dpad.down.wasPressedThisFrame;
    }

    private bool IsKeyboardMouseSelectionInput()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current == null)
            return false;

        return Mouse.current.leftButton.wasPressedThisFrame ||
               Mouse.current.rightButton.wasPressedThisFrame ||
               Mouse.current.middleButton.wasPressedThisFrame ||
               Mouse.current.forwardButton.wasPressedThisFrame ||
               Mouse.current.backButton.wasPressedThisFrame;
    }

    private void SetDeviceLock(DeviceLockState nextState, bool uiOpen)
    {
        deviceLock = nextState;

        string message = deviceLock == DeviceLockState.Gamepad
            ? deviceSelectedGamepadMessage
            : deviceSelectedKeyboardMessage;

        StartDeviceSelectedMessage(message);
        UpdateCursorState(uiOpen);
    }

    private void StartDeviceSelectedMessage(string message)
    {
        if (deviceSelectedMessageRoutine != null)
            StopCoroutine(deviceSelectedMessageRoutine);

        deviceSelectedMessageRoutine = StartCoroutine(DeviceSelectedMessageRoutine(message));
    }

    private System.Collections.IEnumerator DeviceSelectedMessageRoutine(string message)
    {
        deviceSelectedMessageOverride = message;
        UpdateInputModeLabel();
        yield return new WaitForSecondsRealtime(deviceSelectedMessageDuration);
        deviceSelectedMessageOverride = null;
        deviceSelectedMessageRoutine = null;
        UpdateInputModeLabel();
    }

    private void ClearCombatInput()
    {
        if (tankController != null)
        {
            tankController.SetGamepadMoveInput(Vector2.zero);
            tankController.SetGamepadLookInput(Vector2.zero);
            tankController.SetGamepadRunHeld(false);
            tankController.SetGamepadAimHeld(false);
        }

        if (weaponHandler != null)
        {
            weaponHandler.SetGamepadAimHeld(false);
            weaponHandler.SetGamepadFireHeld(false);
        }
    }

    private void ToggleInventory()
    {
        if (inventoryUI == null)
            return;

        inventoryUI.ToggleInventory();
    }

    private void HandleWorldInput(Gamepad gamepad)
    {
        if (playerInventory == null)
            return;

        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            if (!TryInteractWithPickup())
            {
                if (playerInventory.IsNearChest())
                    playerInventory.OpenChest();
            }
        }
    }

    private void UpdateCursorState(bool uiOpen)
    {
        if (deviceLock == DeviceLockState.KeyboardMouse)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        // Добавить проверку паузы
        bool pauseOpen = PauseMenu.Instance != null && PauseMenu.Instance.IsPaused;
        bool cursorVisible = uiOpen || pauseOpen || externalCursorOverrideCount > 0;

        if (cursorVisible)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }


    private bool TryInteractWithPickup()
    {
        if (playerInventory == null)
            return false;

        Transform playerTransform = playerInventory.transform;
        if (playerTransform == null)
            return false;

        const float interactionRange = 2.5f;

        ItemPickup closestItemPickup = FindClosestInRange(cachedItemPickups, playerTransform.position, interactionRange);
        if (closestItemPickup != null && closestItemPickup.TryPickupFromGamepad())
            return true;

        MedkitPickup closestMedkitPickup = FindClosestInRange(cachedMedkitPickups, playerTransform.position, interactionRange);
        if (closestMedkitPickup != null && closestMedkitPickup.TryPickupFromGamepad())
            return true;

        AmmoPickup closestAmmoPickup = FindClosestInRange(cachedAmmoPickups, playerTransform.position, interactionRange);
        if (closestAmmoPickup != null && closestAmmoPickup.TryPickupFromGamepad())
            return true;

        EnemyPickupInteraction closestEnemyPickup = FindClosestInRange(cachedEnemyPickups, playerTransform.position, interactionRange);
        if (closestEnemyPickup != null && closestEnemyPickup.TryPickupFromGamepad())
            return true;

        Interact closestInteract = FindClosestInRange(cachedInteractables, playerTransform.position, interactionRange);
        if (closestInteract != null)
        {
            closestInteract.StartDialogue();
            return true;
        }

        return false;
    }

    private static T FindClosestInRange<T>(T[] candidates, Vector3 origin, float range) where T : MonoBehaviour
    {
        T closest = null;
        float bestDistance = range;

        if (candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null)
                continue;

            float distance = Vector3.Distance(origin, candidate.transform.position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private void RefreshInteractables()
    {
        cachedItemPickups = FindObjectsByType<ItemPickup>(FindObjectsSortMode.InstanceID);
        cachedMedkitPickups = FindObjectsByType<MedkitPickup>(FindObjectsSortMode.InstanceID);
        cachedAmmoPickups = FindObjectsByType<AmmoPickup>(FindObjectsSortMode.InstanceID);
        cachedEnemyPickups = FindObjectsByType<EnemyPickupInteraction>(FindObjectsSortMode.InstanceID);
        cachedInteractables = FindObjectsByType<Interact>(FindObjectsSortMode.InstanceID);
    }

    public void NotifyInteractablesChanged()
    {
        RefreshInteractables();
    }

    public void ResetDeviceLock()
    {
        deviceLock = DeviceLockState.Unselected;
        if (deviceSelectedMessageRoutine != null)
        {
            StopCoroutine(deviceSelectedMessageRoutine);
            deviceSelectedMessageRoutine = null;
        }

        deviceSelectedMessageOverride = null;
        UpdateInputModeLabel();
    }

    private void HandleChestInput(Gamepad gamepad)
    {
        if (inventoryUI == null)
            return;

        if (gamepad.dpad.left.wasPressedThisFrame)
            inventoryUI.MoveChestSelection(-1, 0);
        else if (gamepad.dpad.right.wasPressedThisFrame)
            inventoryUI.MoveChestSelection(1, 0);
        else if (gamepad.dpad.up.wasPressedThisFrame)
            inventoryUI.MoveChestSelection(0, -1);
        else if (gamepad.dpad.down.wasPressedThisFrame)
            inventoryUI.MoveChestSelection(0, 1);

        if (gamepad.buttonNorth.wasPressedThisFrame)
        {
            inventoryUI.ShowContextMenuForSelectedChestItem(true);
            return;
        }

        if (inventoryUI.IsCombineSelectionMode() && gamepad.buttonSouth.wasPressedThisFrame)
        {
            inventoryUI.TryCombineWithSelectedChestItem();
            return;
        }

        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            inventoryUI.TryTakeSelectedChestItem();
            return;
        }

        if (gamepad.buttonEast.wasPressedThisFrame)
            inventoryUI.CloseChestUI();
    }

    private void HandleInventoryInput(Gamepad gamepad, bool contextMenuOpen)
    {
        if (inventoryUI == null || playerInventory == null)
            return;

        bool combineMode = inventoryUI.IsCombineSelectionMode();

        if (contextMenuOpen)
        {
            HandleContextMenuInput(gamepad);
            return;
        }

        if (gamepad.buttonNorth.wasPressedThisFrame)
        {
            inventoryUI.ShowContextMenuForActiveItem(true);
            return;
        }

        if (combineMode && gamepad.buttonSouth.wasPressedThisFrame)
        {
            inventoryUI.TryCombineWithActiveItem();
            return;
        }

        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            TryUseActiveInventoryItem();
            return;
        }

        if (combineMode && gamepad.buttonEast.wasPressedThisFrame)
        {
            inventoryUI.CancelCombineSelectionMode();
            return;
        }

        if (gamepad.buttonEast.wasPressedThisFrame)
        {
            inventoryUI.ToggleInventory();
            return;
        }

        if (gamepad.dpad.down.wasPressedThisFrame)
            CycleActiveItem(1);
        else if (gamepad.dpad.up.wasPressedThisFrame)
            CycleActiveItem(-1);
    }

    private bool HandleUIInput(
        Gamepad gamepad,
        bool gamepadActive,
        bool inventoryOpen,
        bool chestOpen,
        bool contextMenuOpen,
        Vector2 lookInput)
    {
        if (!gamepadActive || gamepad == null)
            return false;

        if (contextMenuOpen)
        {
            HandleContextMenuInput(gamepad);
            ClearCombatInput();
            return true;
        }

        if (inventoryOpen)
        {
            MoveUiCursor(lookInput);
            HandleInventoryInput(gamepad, contextMenuOpen);
            ClearCombatInput();
            return true;
        }

        if (chestOpen)
        {
            HandleChestInput(gamepad);
            ClearCombatInput();
            return true;
        }

        return false;
    }

    private void HandleContextMenuInput(Gamepad gamepad)
    {
        if (inventoryUI == null)
            return;

        if (gamepad.dpad.up.wasPressedThisFrame)
        {
            inventoryUI.MoveContextMenuSelection(-1);
            return;
        }

        if (gamepad.dpad.down.wasPressedThisFrame)
        {
            inventoryUI.MoveContextMenuSelection(1);
            return;
        }

        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            inventoryUI.TriggerContextMenuSelect();
            return;
        }

        if (gamepad.buttonEast.wasPressedThisFrame)
            inventoryUI.TriggerContextMenuCancel();
    }

    private void TryUseActiveInventoryItem()
    {
        if (playerInventory == null || playerInventory.inventoryData == null)
            return;

        int activeIndex = playerInventory.activeItemIndex;
        if (activeIndex < 0 || activeIndex >= playerInventory.inventoryData.GetSlotCount())
            return;

        InventoryItem activeItem = playerInventory.inventoryData.GetItemAt(activeIndex);
        if (activeItem == null)
            return;

        if (activeItem.type == InventoryItem.ItemType.Medkit)
        {
            playerInventory.UseMedkitFromInventory();
            return;
        }

        playerInventory.SetActiveItemByIndex(activeIndex);
    }

    private void CycleActiveItem(int direction)
    {
        if (inventoryUI != null)
        {
            inventoryUI.SelectNextInventoryItem(direction);
            return;
        }

        if (playerInventory == null || playerInventory.inventoryData == null)
            return;

        var slots = playerInventory.inventoryData.GetSlots();
        if (slots == null || slots.Count == 0)
            return;

        int count = slots.Count;
        int index = playerInventory.activeItemIndex;
        if (index < 0 || index >= count)
            index = direction > 0 ? -1 : 0;

        for (int i = 0; i < count; i++)
        {
            index = (index + direction + count) % count;
            InventoryItem item = slots[index];
            if (item != null && item.type != InventoryItem.ItemType.Empty)
            {
                playerInventory.SetActiveItemByIndex(index);
                return;
            }
        }
    }
}