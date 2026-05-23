using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class PumpPuzzleInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private PumpPuzzleController puzzleController;
    [SerializeField] private GameObject puzzleCanvasRoot;
    [SerializeField] private GameObject interactionHint;

    [Header("Input")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("Flow")]
    [SerializeField] private bool resetPuzzleOnOpen = true;
    [SerializeField] private bool resetPuzzleOnClose = false;
    [SerializeField] private bool autoCloseOnSolved = true;
    [SerializeField] private float autoCloseDelay = 1.25f;

    [Header("Completion Actions")]
    [SerializeField] private MonoBehaviour[] disableOnSolved;

    [Header("Solver State")]
    [SerializeField] private bool isSolvedByDefault = false;

    [Header("Lighting")]
    [SerializeField] private GameObject[] lightsToDisableOnSolved;

    [Header("Input Lock")]
    [SerializeField] private bool unlockCursorWhileOpen = true;
    [SerializeField] private bool autoFindPlayerControllers = true;
    [SerializeField] private TankController movementController;
    [SerializeField] private PlayerInventory inventoryController;
    [SerializeField] private WeaponHandler weaponController;
    [SerializeField] private Behaviour[] disableWhileOpen;

    private bool playerInRange;
    private bool isOpened;
    private bool solvedCloseTriggered;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool[] disabledByInteraction;
    private bool movementWasEnabled;
    private bool inventoryWasEnabled;
    private bool weaponWasEnabled;

    // UI gamepad navigation
    private Button[] puzzleButtons;
    private int selectedPuzzleIndex = 0;
    private float lastUiNavTime = 0f;
    private float uiNavCooldown = 0.12f;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (autoFindPlayerControllers)
            TryAutoAssignPlayerControllers();

        if (puzzleCanvasRoot != null)
            puzzleCanvasRoot.SetActive(false);

        if (isSolvedByDefault)
            ApplyCompletedState();

        SetHintVisible(false);
    }

    private void Update()
    {
        if (isSolvedByDefault) return;

        if (!playerInRange)
        {
            if (!isOpened) SetHintVisible(false);
            return;
        }

        if (!isOpened)
        {
            SetHintVisible(true);
            if (Input.GetKeyDown(interactionKey) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))
            {
                OpenPuzzle();
            }
            return;
        }

        // If opened, handle UI navigation and buttons
        if (Gamepad.current != null)
            HandlePuzzleUiNavigation();

        if (Input.GetKeyDown(closeKey) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
        {
            ClosePuzzle();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        if (!isOpened) SetHintVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
        if (!isOpened) SetHintVisible(false);
    }

    public void OnPuzzleSolved()
    {
        if (solvedCloseTriggered) return;
        isSolvedByDefault = true;
        ApplyCompletedState();
        if (isOpened && autoCloseOnSolved)
        {
            solvedCloseTriggered = true;
            Invoke(nameof(ClosePuzzle), Mathf.Max(0f, autoCloseDelay));
        }
    }

    private void ApplyCompletedState()
    {
        if (lightsToDisableOnSolved != null)
        {
            foreach (var go in lightsToDisableOnSolved)
                if (go != null) go.SetActive(false);
        }

        if (disableOnSolved != null)
        {
            foreach (var b in disableOnSolved)
                if (b != null) b.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        SetHintVisible(false);
    }

    public void OpenPuzzle()
    {
        if (isOpened) return;
        isOpened = true;
        solvedCloseTriggered = false;

        if (puzzleCanvasRoot != null) puzzleCanvasRoot.SetActive(true);
        if (puzzleController != null && resetPuzzleOnOpen) puzzleController.ResetPuzzle();

        ApplyInputLock(true);
        SetHintVisible(false);
        EnsurePuzzleButtons();
        UpdatePuzzleSelectionVisual();
    }

    public void ClosePuzzle()
    {
        if (!isOpened) return;
        isOpened = false;
        solvedCloseTriggered = false;
        CancelInvoke(nameof(ClosePuzzle));

        ApplyInputLock(false);
        if (puzzleController != null && resetPuzzleOnClose) puzzleController.ResetPuzzle();
        if (puzzleCanvasRoot != null) puzzleCanvasRoot.SetActive(false);
        if (playerInRange) SetHintVisible(true);

        // clear selection
        selectedPuzzleIndex = 0;
        UpdatePuzzleSelectionVisual();
    }

    private void EnsurePuzzleButtons()
    {
        if (puzzleCanvasRoot == null) return;
        var btns = puzzleCanvasRoot.GetComponentsInChildren<Button>(true);
        puzzleButtons = btns != null ? btns : new Button[0];
        selectedPuzzleIndex = Mathf.Clamp(selectedPuzzleIndex, 0, Mathf.Max(0, puzzleButtons.Length - 1));
        if (puzzleButtons.Length > 0 && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(puzzleButtons[selectedPuzzleIndex].gameObject);
    }

    private void HandlePuzzleUiNavigation()
    {
        if (puzzleButtons == null || puzzleButtons.Length == 0) return;
        if (Time.unscaledTime - lastUiNavTime < uiNavCooldown) return;
        if (Gamepad.current == null) return;

        bool moved = false;
        if (Gamepad.current.dpad.left.wasPressedThisFrame)
        {
            selectedPuzzleIndex = (selectedPuzzleIndex - 1 + puzzleButtons.Length) % puzzleButtons.Length;
            moved = true;
        }
        else if (Gamepad.current.dpad.right.wasPressedThisFrame)
        {
            selectedPuzzleIndex = (selectedPuzzleIndex + 1) % puzzleButtons.Length;
            moved = true;
        }

        if (moved)
        {
            lastUiNavTime = Time.unscaledTime;
            UpdatePuzzleSelectionVisual();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(puzzleButtons[selectedPuzzleIndex].gameObject);
            return;
        }

        if (Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            Button btn = puzzleButtons[selectedPuzzleIndex];
            if (btn != null && btn.interactable) btn.onClick.Invoke();
        }

        if (Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            ClosePuzzle();
        }
    }

    private void UpdatePuzzleSelectionVisual()
    {
        if (puzzleButtons == null) return;
        for (int i = 0; i < puzzleButtons.Length; i++)
        {
            var btn = puzzleButtons[i];
            if (btn == null) continue;

            UnityEngine.UI.Outline outline = btn.GetComponent<UnityEngine.UI.Outline>();
            if (i == selectedPuzzleIndex)
            {
                if (outline == null) outline = btn.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = Color.red;
                outline.effectDistance = new Vector2(2f, 2f);
                outline.enabled = true;
            }
            else
            {
                if (outline != null) outline.enabled = false;
            }
        }
    }

    private void ApplyInputLock(bool lockInput)
    {
        if (lockInput)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;

            if (unlockCursorWhileOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (movementController != null)
            {
                movementWasEnabled = movementController.enabled;
                movementController.enabled = false;
            }

            if (inventoryController != null)
            {
                inventoryWasEnabled = inventoryController.enabled;
                inventoryController.enabled = false;
            }

            if (weaponController != null)
            {
                weaponWasEnabled = weaponController.enabled;
                weaponController.enabled = false;
            }

            if (disableWhileOpen != null)
            {
                if (disabledByInteraction == null || disabledByInteraction.Length != disableWhileOpen.Length)
                    disabledByInteraction = new bool[disableWhileOpen.Length];

                for (int i = 0; i < disableWhileOpen.Length; i++)
                {
                    Behaviour behaviour = disableWhileOpen[i];
                    if (behaviour != null && behaviour.enabled)
                    {
                        behaviour.enabled = false;
                        disabledByInteraction[i] = true;
                    }
                    else if (disabledByInteraction != null)
                    {
                        disabledByInteraction[i] = false;
                    }
                }
            }

            return;
        }

        if (disableWhileOpen != null)
        {
            for (int i = 0; i < disableWhileOpen.Length; i++)
            {
                Behaviour behaviour = disableWhileOpen[i];
                bool shouldRestore = disabledByInteraction != null && i < disabledByInteraction.Length && disabledByInteraction[i];
                if (behaviour != null && shouldRestore)
                {
                    behaviour.enabled = true;
                    disabledByInteraction[i] = false;
                }
            }
        }

        if (movementController != null && movementWasEnabled)
        {
            movementController.enabled = true;
            movementWasEnabled = false;
        }

        if (inventoryController != null && inventoryWasEnabled)
        {
            inventoryController.enabled = true;
            inventoryWasEnabled = false;
        }

        if (weaponController != null && weaponWasEnabled)
        {
            weaponController.enabled = true;
            weaponWasEnabled = false;
        }

        if (unlockCursorWhileOpen)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
        }
    }

    private void TryAutoAssignPlayerControllers()
    {
        Transform playerTransform = player;
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (playerTransform == null) return;

        if (movementController == null) movementController = FindOnPlayer<TankController>(playerTransform);
        if (inventoryController == null) inventoryController = FindOnPlayer<PlayerInventory>(playerTransform);
        if (weaponController == null) weaponController = FindOnPlayer<WeaponHandler>(playerTransform);
    }

    private static T FindOnPlayer<T>(Transform playerTransform) where T : Component
    {
        if (playerTransform == null) return null;
        T component = playerTransform.GetComponent<T>();
        if (component != null) return component;
        component = playerTransform.GetComponentInParent<T>();
        if (component != null) return component;
        return playerTransform.GetComponentInChildren<T>(true);
    }

    private void SetHintVisible(bool isVisible)
    {
        if (interactionHint != null) interactionHint.SetActive(isVisible);
    }
}
