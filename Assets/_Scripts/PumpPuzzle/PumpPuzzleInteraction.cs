using UnityEngine;

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

    [Header("Lighting")]
    [SerializeField] private GameObject[] lightsToEnableOnSolved;
    [SerializeField] private bool forceLightsOffOnStart = true;

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

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        if (autoFindPlayerControllers)
        {
            TryAutoAssignPlayerControllers();
        }

        if (puzzleCanvasRoot != null)
        {
            puzzleCanvasRoot.SetActive(false);
        }

        if (forceLightsOffOnStart)
        {
            SetSolvedLightsActive(false);
        }

        SetHintVisible(false);
    }

    private void Update()
    {
        if (!playerInRange)
        {
            if (!isOpened)
            {
                SetHintVisible(false);
            }
            return;
        }

        if (!isOpened)
        {
            SetHintVisible(true);
            if (Input.GetKeyDown(interactionKey))
            {
                OpenPuzzle();
            }
            return;
        }

        if (Input.GetKeyDown(closeKey) || Input.GetKeyDown(interactionKey))
        {
            ClosePuzzle();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerInRange = true;
        if (!isOpened)
        {
            SetHintVisible(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerInRange = false;
        if (!isOpened)
        {
            SetHintVisible(false);
        }
    }

    public void OnPuzzleSolved()
    {
        if (!isOpened || !autoCloseOnSolved || solvedCloseTriggered)
        {
            return;
        }

        SetSolvedLightsActive(true);
        solvedCloseTriggered = true;
        Invoke(nameof(ClosePuzzle), Mathf.Max(0f, autoCloseDelay));
    }

    public void OpenPuzzle()
    {
        if (isOpened)
        {
            return;
        }

        isOpened = true;
        solvedCloseTriggered = false;

        if (puzzleCanvasRoot != null)
        {
            puzzleCanvasRoot.SetActive(true);
        }

        if (puzzleController != null && resetPuzzleOnOpen)
        {
            puzzleController.ResetPuzzle();
        }

        ApplyInputLock(true);
        SetHintVisible(false);
    }

    public void ClosePuzzle()
    {
        if (!isOpened)
        {
            return;
        }

        isOpened = false;
        solvedCloseTriggered = false;
        CancelInvoke(nameof(ClosePuzzle));

        ApplyInputLock(false);

        if (puzzleController != null && resetPuzzleOnClose)
        {
            puzzleController.ResetPuzzle();
        }

        if (puzzleCanvasRoot != null)
        {
            puzzleCanvasRoot.SetActive(false);
        }

        if (playerInRange)
        {
            SetHintVisible(true);
        }
    }

    private void SetSolvedLightsActive(bool isActive)
    {
        if (lightsToEnableOnSolved == null)
        {
            return;
        }

        for (int i = 0; i < lightsToEnableOnSolved.Length; i++)
        {
            if (lightsToEnableOnSolved[i] != null)
            {
                lightsToEnableOnSolved[i].SetActive(isActive);
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
                {
                    disabledByInteraction = new bool[disableWhileOpen.Length];
                }

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
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        if (playerTransform == null)
        {
            return;
        }

        if (movementController == null)
        {
            movementController = FindOnPlayer<TankController>(playerTransform);
        }

        if (inventoryController == null)
        {
            inventoryController = FindOnPlayer<PlayerInventory>(playerTransform);
        }

        if (weaponController == null)
        {
            weaponController = FindOnPlayer<WeaponHandler>(playerTransform);
        }
    }

    private static T FindOnPlayer<T>(Transform playerTransform) where T : Component
    {
        if (playerTransform == null)
        {
            return null;
        }

        T component = playerTransform.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        component = playerTransform.GetComponentInParent<T>();
        if (component != null)
        {
            return component;
        }

        return playerTransform.GetComponentInChildren<T>(true);
    }

    private void SetHintVisible(bool isVisible)
    {
        if (interactionHint != null)
        {
            interactionHint.SetActive(isVisible);
        }
    }
}
