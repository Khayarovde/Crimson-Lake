using UnityEngine;
using UnityEngine.InputSystem;

public class GamepadController : MonoBehaviour
{
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

    private float lastGamepadInputTime = -10f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            ClearGamepadState();
            return;
        }

        Vector2 moveInput = gamepad.leftStick.ReadValue();
        Vector2 lookInput = gamepad.rightStick.ReadValue();
        bool runHeld = gamepad.leftShoulder.isPressed;
        bool aimHeld = gamepad.leftTrigger.ReadValue() >= triggerThreshold;
        bool fireHeld = gamepad.rightTrigger.ReadValue() >= triggerThreshold;
        bool reloadHeld = aimHeld;

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

        bool uiOpen = inventoryUI != null && (inventoryUI.IsInventoryOpen() || inventoryUI.IsChestUIOpen() || inventoryUI.IsContextMenuOpen());
        bool gamepadActive = uiOpen || Time.unscaledTime - lastGamepadInputTime <= gamepadActiveTimeout;

        bool inventoryOpen = inventoryUI != null && inventoryUI.IsInventoryOpen();
        bool chestOpen = inventoryUI != null && inventoryUI.IsChestUIOpen();
        bool contextMenuOpen = inventoryUI != null && inventoryUI.IsContextMenuOpen();
        UpdateCursorState(uiOpen);

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

        if (gamepad.startButton.wasPressedThisFrame)
        {
            ToggleInventory();
            return;
        }

        if (!inventoryOpen && !chestOpen && !contextMenuOpen && reloadHeld && gamepad.buttonNorth.wasPressedThisFrame)
        {
            if (weaponHandler != null)
                weaponHandler.RequestGamepadReload();
            return;
        }

        if (inventoryOpen)
        {
            MoveUiCursor(lookInput);
            HandleInventoryInput(gamepad, contextMenuOpen);
            ClearCombatInput();
            return;
        }

        if (chestOpen)
        {
            HandleChestInput(gamepad);
            ClearCombatInput();
            return;
        }

        if (contextMenuOpen)
        {
            MoveUiCursor(lookInput);
            HandleContextMenuInput(gamepad);
            ClearCombatInput();
            return;
        }

        HandleWorldInput(gamepad);
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

        lastGamepadInputTime = -10f;
        UpdateCursorState(false);
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
        if (uiOpen)
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

        if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame)
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
            if (!inventoryUI.TryCombineWithActiveItem())
                return;

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