using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class GamepadController : MonoBehaviour
{
    private enum ActiveInputDevice
    {
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
    [SerializeField, Range(0.1f, 5f)] private float gamepadActiveTimeout = 1.5f;
    [SerializeField, Range(200f, 2500f)] private float uiCursorMoveSpeed = 1100f;
    [SerializeField, Range(0f, 80f)] private float uiCursorEdgePadding = 18f;

    [Header("HUD")]
    [SerializeField] private bool showInputModeLabel = true;
    [SerializeField] private string gamepadModeLabel = "Подключен геймпад";
    [SerializeField] private string keyboardMouseModeLabel = "Клава + мышь";
    [SerializeField, Range(18f, 42f)] private float inputModeLabelFontSize = 22f;
    [SerializeField, Range(0f, 80f)] private float inputModeLabelBottomOffset = 24f;
    [SerializeField, Range(180f, 420f)] private float inputModeLabelWidth = 280f;
    [SerializeField, Range(28f, 72f)] private float inputModeLabelHeight = 36f;
    [SerializeField] private TextMeshProUGUI inputModeLabelText;

    private float lastGamepadInputTime = -10f;
    private float lastMouseKeyboardInputTime = -10f;
    private ActiveInputDevice activeInputDevice = ActiveInputDevice.KeyboardMouse;
    private RectTransform inputModeLabelRect;
    private bool ignoreMouseMotionOnce;
    private int externalCursorOverrideCount;

    private void Awake()
    {
        ResolveReferences();
        EnsureInputModeLabel();
        UpdateInputModeLabel();
    }

    private void OnDisable()
    {
        externalCursorOverrideCount = 0;
        ignoreMouseMotionOnce = false;
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

        UpdateActiveDevice(uiOpen);

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

    public bool IsGamepadModeActive => activeInputDevice == ActiveInputDevice.Gamepad;

    public void PushExternalCursorOverride()
    {
        externalCursorOverrideCount = Mathf.Max(0, externalCursorOverrideCount + 1);
    }

    public void PopExternalCursorOverride()
    {
        externalCursorOverrideCount = Mathf.Max(0, externalCursorOverrideCount - 1);
    }

    private void UpdateActiveDevice(bool uiOpen)
    {
        Gamepad gamepad = Gamepad.current;
        bool gamepadAvailable = gamepad != null;

        if (HasMouseKeyboardInputThisFrame())
            lastMouseKeyboardInputTime = Time.unscaledTime;

        if (gamepadAvailable)
        {
            Vector2 moveInput = gamepad.leftStick.ReadValue();
            Vector2 lookInput = gamepad.rightStick.ReadValue();
            bool runHeld = gamepad.leftShoulder.isPressed;
            bool aimHeld = gamepad.leftTrigger.ReadValue() >= triggerThreshold;
            bool fireHeld = gamepad.rightTrigger.ReadValue() >= triggerThreshold;

            bool anyGamepadInput = moveInput.sqrMagnitude >= moveDeadzone * moveDeadzone ||
                                   lookInput.sqrMagnitude >= lookDeadzone * lookDeadzone ||
                                   runHeld ||
                                   aimHeld ||
                                   fireHeld ||
                                   gamepad.startButton.wasPressedThisFrame ||
                                   gamepad.buttonSouth.wasPressedThisFrame ||
                                   gamepad.buttonEast.wasPressedThisFrame ||
                                   gamepad.buttonWest.wasPressedThisFrame ||
                                   gamepad.buttonNorth.wasPressedThisFrame ||
                                   gamepad.leftShoulder.wasPressedThisFrame ||
                                   gamepad.rightShoulder.wasPressedThisFrame ||
                                   gamepad.dpad.left.wasPressedThisFrame ||
                                   gamepad.dpad.right.wasPressedThisFrame ||
                                   gamepad.dpad.up.wasPressedThisFrame ||
                                   gamepad.dpad.down.wasPressedThisFrame;

            if (anyGamepadInput)
                lastGamepadInputTime = Time.unscaledTime;
        }

        bool gamepadRecent = gamepadAvailable && Time.unscaledTime - lastGamepadInputTime <= gamepadActiveTimeout;
        bool mouseKeyboardRecent = Time.unscaledTime - lastMouseKeyboardInputTime <= gamepadActiveTimeout;
        ActiveInputDevice nextActiveDevice = gamepadRecent && (!mouseKeyboardRecent || lastGamepadInputTime >= lastMouseKeyboardInputTime)
            ? ActiveInputDevice.Gamepad
            : ActiveInputDevice.KeyboardMouse;

        if (nextActiveDevice != activeInputDevice)
        {
            ClearGamepadState();
            activeInputDevice = nextActiveDevice;
        }

        UpdateCursorState(uiOpen, IsGamepadModeActive);
    }

    private bool HasMouseKeyboardInputThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            return true;

        if (Mouse.current == null)
            return false;

        if (Mouse.current.leftButton.isPressed ||
            Mouse.current.rightButton.isPressed ||
            Mouse.current.middleButton.isPressed ||
            Mouse.current.forwardButton.isPressed ||
            Mouse.current.backButton.isPressed ||
            Mouse.current.scroll.ReadValue().sqrMagnitude > 0.01f)
        {
            return true;
        }

        if (ignoreMouseMotionOnce)
        {
            ignoreMouseMotionOnce = false;
            return false;
        }

        return Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f;
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

    private void UpdateCursorState(bool uiOpen, bool gamepadActive)
    {
        bool cursorVisible = uiOpen || externalCursorOverrideCount > 0 || !gamepadActive;

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

    private void MoveUiCursor(Vector2 lookInput)
    {
        if (lookInput.sqrMagnitude < lookDeadzone * lookDeadzone)
            return;

        if (Mouse.current == null)
            return;

        Vector2 currentPosition = Mouse.current.position.ReadValue();
        float deltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        Vector2 nextPosition = currentPosition + lookInput * uiCursorMoveSpeed * deltaTime;

        float maxX = Mathf.Max(uiCursorEdgePadding, Screen.width - uiCursorEdgePadding);
        float maxY = Mathf.Max(uiCursorEdgePadding, Screen.height - uiCursorEdgePadding);
        nextPosition.x = Mathf.Clamp(nextPosition.x, uiCursorEdgePadding, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, uiCursorEdgePadding, maxY);

        Mouse.current.WarpCursorPosition(nextPosition);
        ignoreMouseMotionOnce = true;
    }

    private bool TryInteractWithPickup()
    {
        if (playerInventory == null)
            return false;

        Transform playerTransform = playerInventory.transform;
        if (playerTransform == null)
            return false;

        const float interactionRange = 2.5f;

        ItemPickup[] itemPickups = FindObjectsByType<ItemPickup>(FindObjectsSortMode.InstanceID);
        ItemPickup closestItemPickup = FindClosestInRange(itemPickups, playerTransform.position, interactionRange);
        if (closestItemPickup != null && closestItemPickup.TryPickupFromGamepad())
            return true;

        MedkitPickup[] medkitPickups = FindObjectsByType<MedkitPickup>(FindObjectsSortMode.InstanceID);
        MedkitPickup closestMedkitPickup = FindClosestInRange(medkitPickups, playerTransform.position, interactionRange);
        if (closestMedkitPickup != null && closestMedkitPickup.TryPickupFromGamepad())
            return true;

        AmmoPickup[] ammoPickups = FindObjectsByType<AmmoPickup>(FindObjectsSortMode.InstanceID);
        AmmoPickup closestAmmoPickup = FindClosestInRange(ammoPickups, playerTransform.position, interactionRange);
        if (closestAmmoPickup != null && closestAmmoPickup.TryPickupFromGamepad())
            return true;

        EnemyPickupInteraction[] enemyPickups = FindObjectsByType<EnemyPickupInteraction>(FindObjectsSortMode.InstanceID);
        EnemyPickupInteraction closestEnemyPickup = FindClosestInRange(enemyPickups, playerTransform.position, interactionRange);
        if (closestEnemyPickup != null && closestEnemyPickup.TryPickupFromGamepad())
            return true;

        Interact[] interactables = FindObjectsByType<Interact>(FindObjectsSortMode.InstanceID);
        Interact closestInteract = FindClosestInRange(interactables, playerTransform.position, interactionRange);
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