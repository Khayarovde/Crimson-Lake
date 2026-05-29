using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events; // Добавляем для UnityEvent
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class ComputerInteraction : MonoBehaviour
{
    [Header("References")]
    public GameObject player;
    public float interactionDistance = 3f;
    public Canvas computerCanvas;
    public Image pcImage;
    public Sprite pcOffSprite;
    public Sprite pcOnSprite;
    public Sprite pcInsertedSprite;
    public Button powerButton;
    public Button insertButton;
    public Image disketteIcon;
    public Slider progressBar;
    public TextMeshProUGUI statusText;
    
    [Header("Sounds")]
    public AudioClip openInterfaceSound;    // Тихий хум/вентиляторы при подходе
    public AudioClip powerOnSound;          // Beep или клик включения
    public AudioClip buzzingSound;          // Жужжание привода при удержании (уже был)
    public AudioClip failureSound;          // Звук сбоя/заедания дискеты
    public AudioClip successSound;          // Звук успешной вставки/загрузки

    public InventoryData inventoryData;
    public InventoryUI inventoryUI;

    [Header("Settings")]
    public float fillSpeed = 30f;
    public float dropSpeed = 15f;
    public float randomFailureChance = 0.15f;
    public float randomFailureDrop = 20f;

    [Header("Persistence")]
    [PickupId]
    [SerializeField] private string disketteInsertedId;
    [SerializeField] private bool invokeSuccessEventOnLoad = true;

    [Header("Gamepad")]
    [SerializeField] private float gamepadCursorSpeed = 1200f;
    [SerializeField, Range(0.05f, 0.75f)] private float triggerThreshold = 0.5f;

    [Header("Лифт")]
    public UnityEvent onDisketteInsertedSuccess; // Событие, которое вызовется при успехе

    private AudioSource audioSource;
    private GamepadController gamepadController;
    private bool interacting = false;
    private bool isOn = false;
    private bool hasDiskette = false;
    private InventoryItem disketteItem = null;
    private bool inserting = false;
    private bool holding = false;
    private float progress = 0f;
    private float failureTimer = 0f;
    private bool insertSuccess = false;
    private bool insertSuccessPersisted = false;
    private bool persistenceApplied = false;
    private bool insertHoldStartedByMouse = false;
    private Vector2 gamepadCursorPosition;
    private bool gamepadCursorInitialized;
    private bool cursorWasVisible;
    private CursorLockMode cursorWasLockState;
    private bool leftTriggerWasPressed;

    [HideInInspector] public bool isInsertHeld = false;

    void Start()
    {
        ResolvePlayerReference();
        ResolveGamepadController();

        if (computerCanvas != null) computerCanvas.gameObject.SetActive(false);
        if (progressBar != null) progressBar.gameObject.SetActive(false);
        if (insertButton != null) insertButton.gameObject.SetActive(false);
        if (disketteIcon != null) disketteIcon.gameObject.SetActive(false);
        if (statusText != null) statusText.text = "";

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (powerButton != null)
            powerButton.onClick.AddListener(TurnOnPC);
        if (insertButton != null)
            insertButton.onClick.AddListener(StartInserting);
    }

    void Update()
    {
        if (!persistenceApplied && !string.IsNullOrWhiteSpace(disketteInsertedId))
        {
            if (SaveManager.HasPickedUpItem(disketteInsertedId))
            {
                ApplyPersistenceState();
            }
        }

        // Input handling separated for clarity and symmetry
        HandleInput();
    }

    private void HandleInput()
    {
        bool hasGamepad = Gamepad.current != null;
        bool gpPressed = hasGamepad && Gamepad.current.buttonSouth.wasPressedThisFrame;
        bool gpHeld = hasGamepad && Gamepad.current.buttonSouth.isPressed;

        // Open interaction: keyboard E or gamepad A
        if (!interacting && player != null && Vector3.Distance(player.transform.position, transform.position) < interactionDistance && (Input.GetKeyDown(KeyCode.E) || gpPressed))
        {
            StartInteraction();
            return;
        }

        if (!interacting)
            return;

        // When interacting, always allow keyboard E to close, and gamepad East to close
        HandleGamepadCursor();

        if (Input.GetKeyDown(KeyCode.E) || (hasGamepad && Gamepad.current.buttonEast.wasPressedThisFrame))
        {
            CloseInteraction();
            return;
        }

        // Gamepad click handling for UI when interacting
        if (hasGamepad && gpPressed)
        {
            if (!ClickButtonUnderCursor())
            {
                if (computerCanvas != null && computerCanvas.gameObject.activeSelf)
                {
                    if (insertButton != null && insertButton.gameObject.activeSelf)
                    {
                        if (insertButton.onClick != null)
                            insertButton.onClick.Invoke();
                    }
                    else if (powerButton != null && powerButton.gameObject.activeSelf)
                    {
                        if (powerButton.onClick != null)
                            powerButton.onClick.Invoke();
                    }
                }
            }
        }

        if (inserting && !insertSuccess)
        {
            // Update isInsertHeld from gamepad hold as well
            if (!isInsertHeld && gpHeld)
            {
                BeginInsertHold(false);
            }
            else if (isInsertHeld && !gpHeld && Gamepad.current != null)
            {
                if (!insertHoldStartedByMouse)
                    EndInsertHold();
            }

            if (isInsertHeld)
            {
                if (!holding)
                {
                    holding = true;
                    if (buzzingSound != null) audioSource.PlayOneShot(buzzingSound);
                    statusText.text = "Запуск мотора привода...";
                }

                progress += Time.deltaTime * fillSpeed;

                failureTimer += Time.deltaTime;
                if (failureTimer >= 1f)
                {
                    if (Random.value < randomFailureChance)
                    {
                        progress -= randomFailureDrop;
                        if (failureSound != null) audioSource.PlayOneShot(failureSound); // Звук сбоя
                    }
                    failureTimer = 0f;
                }
            }
            else
            {
                if (holding)
                {
                    holding = false;
                    audioSource.Stop();
                    if (progress < 100f) statusText.text = "Вставка приостановлена...";
                }

                progress -= Time.deltaTime * dropSpeed;
            }

            progress = Mathf.Clamp(progress, 0f, 100f);
            progressBar.value = progress / 100f;

            if (progress >= 100f)
            {
                InsertSuccess();
            }
        }
    }

    private void StartInteraction()
    {
        if (computerCanvas == null || progressBar == null || insertButton == null || powerButton == null || pcImage == null || statusText == null)
            return;

        interacting = true;
        computerCanvas.gameObject.SetActive(true);
        RefreshDisketteState();
        ApplyGamepadCursorState(true);
        if (gamepadController != null)
            gamepadController.PushExternalCursorOverride();

        // Initialize gamepad cursor to center immediately so UI is ready for gamepad
        gamepadCursorInitialized = true;
        gamepadCursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // Звук открытия интерфейса (подход к ПК)
        if (openInterfaceSound != null) audioSource.PlayOneShot(openInterfaceSound);

        bool shouldShowInserted = insertSuccessPersisted || insertSuccess;

        // Сброс...
        isOn = false;
        inserting = false;
        if (!insertSuccessPersisted)
            insertSuccess = false;
        holding = false;
        insertHoldStartedByMouse = false;
        isInsertHeld = false;
        progress = 0f;
        progressBar.value = 0f;
        progressBar.gameObject.SetActive(false);
        insertButton.gameObject.SetActive(false);
        statusText.text = "";
        pcImage.sprite = pcOffSprite;
        powerButton.gameObject.SetActive(true);

        isInsertHeld = false;

        if (disketteIcon != null)
        {
            disketteIcon.gameObject.SetActive(hasDiskette);
        }

        if (shouldShowInserted)
        {
            ApplyInsertedVisualState(false);
        }
    }

    private void CloseInteraction()
    {
        interacting = false;
        computerCanvas.gameObject.SetActive(false);
        audioSource.Stop();
        isInsertHeld = false;
        ApplyGamepadCursorState(false);
        if (gamepadController != null)
            gamepadController.PopExternalCursorOverride();

        if (inserting && !insertSuccess)
        {
            inserting = false;
            progressBar.gameObject.SetActive(false);
            statusText.text = "";
        }

        insertHoldStartedByMouse = false;
    }

    private void TurnOnPC()
    {
        if (!isOn)
        {
            isOn = true;
            pcImage.sprite = pcOnSprite;
            powerButton.gameObject.SetActive(false);

            // Звук включения ПК
            if (powerOnSound != null) audioSource.PlayOneShot(powerOnSound);

            RefreshDisketteState();
            if (hasDiskette)
            {
                insertButton.gameObject.SetActive(true);
            }
        }
    }

    public void StartInserting()
    {
        if (isOn && hasDiskette && !inserting)
        {
            inserting = true;
            progressBar.gameObject.SetActive(true);
            statusText.text = "Нажмите и удерживайте (Вставить)";
        }
    }

    public void BeginInsertHold()
    {
        BeginInsertHold(false);
    }

    public void BeginInsertHold(bool fromMouse)
    {
        insertHoldStartedByMouse = fromMouse;
        isInsertHeld = true;
        if (isOn && hasDiskette && !inserting)
            StartInserting();
    }

    public void EndInsertHold()
    {
        isInsertHeld = false;
        insertHoldStartedByMouse = false;
    }

    private void HandleGamepadCursor()
    {
        if (!interacting || Gamepad.current == null)
            return;

        if (!gamepadCursorInitialized)
        {
            gamepadCursorPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            gamepadCursorInitialized = true;
        }

        Vector2 stick = Gamepad.current.rightStick.ReadValue();
        if (stick.sqrMagnitude > 0.01f)
        {
            gamepadCursorPosition += stick * gamepadCursorSpeed * Time.unscaledDeltaTime;
            gamepadCursorPosition.x = Mathf.Clamp(gamepadCursorPosition.x, 0f, Screen.width - 1f);
            gamepadCursorPosition.y = Mathf.Clamp(gamepadCursorPosition.y, 0f, Screen.height - 1f);

            if (Mouse.current != null)
            {
                Mouse.current.WarpCursorPosition(gamepadCursorPosition);
            }
        }
    }

    private bool ClickButtonUnderCursor()
    {
        if (EventSystem.current == null)
            return false;

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
                return true;
            }
        }

        return false;
    }

    private void ApplyGamepadCursorState(bool active)
    {
        if (active)
        {
            cursorWasVisible = Cursor.visible;
            cursorWasLockState = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            gamepadCursorInitialized = false;
            return;
        }

        Cursor.visible = cursorWasVisible;
        Cursor.lockState = cursorWasLockState;
    }

    private void InsertSuccess()
    {
        insertSuccess = true;
        inserting = false;
        holding = false;
        audioSource.Stop();

        if (successSound != null) audioSource.PlayOneShot(successSound);

        statusText.text = "Дискета успешно вставлена! Лифт разблокирован.";

        if (disketteItem != null)
        {
            InventoryData activeInventoryData = ResolveInventoryData();
            if (activeInventoryData != null)
                activeInventoryData.RemoveItem(disketteItem);
            if (inventoryUI != null) inventoryUI.UpdateInventoryUI();
        }

        if (disketteIcon != null)
        {
            disketteIcon.gameObject.SetActive(false);
        }

        if (pcInsertedSprite != null)
        {
            pcImage.sprite = pcInsertedSprite;
        }

        insertButton.gameObject.SetActive(false);
        progressBar.gameObject.SetActive(false);

        // <<< ВАЖНО: Сообщаем, что лифт теперь можно открыть >>>
        onDisketteInsertedSuccess?.Invoke();

        if (!string.IsNullOrWhiteSpace(disketteInsertedId))
            SaveManager.MarkItemPickedUp(disketteInsertedId);
    }

    private void ApplyPersistenceState()
    {
        if (persistenceApplied)
            return;

        if (string.IsNullOrWhiteSpace(disketteInsertedId))
            return;

        if (!SaveManager.HasPickedUpItem(disketteInsertedId))
            return;

        persistenceApplied = true;

        insertSuccessPersisted = true;
        insertSuccess = true;
        isOn = true;
        inserting = false;
        holding = false;
        isInsertHeld = false;
        progress = 100f;

        ApplyInsertedVisualState(false);

        if (invokeSuccessEventOnLoad)
            onDisketteInsertedSuccess?.Invoke();
    }

    private void ApplyInsertedVisualState(bool showStatus)
    {
        if (pcInsertedSprite != null)
            pcImage.sprite = pcInsertedSprite;
        else if (pcOnSprite != null)
            pcImage.sprite = pcOnSprite;

        if (powerButton != null) powerButton.gameObject.SetActive(false);
        if (insertButton != null) insertButton.gameObject.SetActive(false);
        if (progressBar != null) progressBar.gameObject.SetActive(false);
        if (disketteIcon != null) disketteIcon.gameObject.SetActive(false);

        if (showStatus && statusText != null)
            statusText.text = "";
    }

    private InventoryData ResolveInventoryData()
    {
        if (player != null)
        {
            PlayerInventory playerInventory = player.GetComponentInParent<PlayerInventory>();
            if (playerInventory != null && playerInventory.inventoryData != null)
                return playerInventory.inventoryData;
        }

        PlayerInventory fallbackPlayerInventory = FindFirstObjectByType<PlayerInventory>();
        if (fallbackPlayerInventory != null && fallbackPlayerInventory.inventoryData != null)
            return fallbackPlayerInventory.inventoryData;

        return inventoryData;
    }

    private void RefreshDisketteState()
    {
        InventoryData activeInventoryData = ResolveInventoryData();
        hasDiskette = false;
        disketteItem = null;

        if (activeInventoryData == null)
            return;

        var slots = activeInventoryData.GetSlots();
        foreach (var item in slots)
        {
            if (item == null || item.type == InventoryItem.ItemType.Empty)
                continue;

            if (item.type == InventoryItem.ItemType.Cassette || item.type == InventoryItem.ItemType.Disketa)
            {
                hasDiskette = true;
                disketteItem = item;
                break;
            }
        }
    }

    private bool IsPointerOverButton(Button button)
    {
        if (button == null || computerCanvas == null || Mouse.current == null)
            return false;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect == null)
            return false;

        Camera uiCamera = computerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : computerCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Mouse.current.position.ReadValue(), uiCamera);
    }

    private void ResolvePlayerReference()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject;
            return;
        }

        PlayerInventory playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (playerInventory != null)
            player = playerInventory.gameObject;
    }

    private void ResolveGamepadController()
    {
        if (gamepadController == null)
            gamepadController = FindFirstObjectByType<GamepadController>();
    }
}