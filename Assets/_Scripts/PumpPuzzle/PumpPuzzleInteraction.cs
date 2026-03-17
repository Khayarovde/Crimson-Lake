using UnityEngine;
using System.Reflection;

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

    [Header("Camera (Cinemachine)")]
    [SerializeField] private MonoBehaviour gameplayCamera;
    [SerializeField] private MonoBehaviour puzzleCamera;
    [SerializeField] private int gameplayPriority = 10;
    [SerializeField] private int puzzlePriority = 30;

    [Header("Input Lock")]
    [SerializeField] private bool unlockCursorWhileOpen = true;
    [SerializeField] private MonoBehaviour[] disableWhileOpen;

    private bool playerInRange;
    private bool isOpened;
    private bool solvedCloseTriggered;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

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

        if (puzzleCanvasRoot != null)
        {
            puzzleCanvasRoot.SetActive(false);
        }

        SetHintVisible(false);
        ApplyCameraState(false);
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
        ApplyCameraState(true);
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
        ApplyCameraState(false);

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

    private void ApplyCameraState(bool puzzleActive)
    {
        SetCameraPriority(gameplayCamera, puzzleActive ? gameplayPriority : puzzlePriority);
        SetCameraPriority(puzzleCamera, puzzleActive ? puzzlePriority : gameplayPriority);
    }

    private static void SetCameraPriority(MonoBehaviour cameraBehaviour, int value)
    {
        if (cameraBehaviour == null)
        {
            return;
        }

        PropertyInfo priorityProperty = cameraBehaviour.GetType().GetProperty("Priority");
        if (priorityProperty != null && priorityProperty.CanWrite)
        {
            priorityProperty.SetValue(cameraBehaviour, value);
            return;
        }

        FieldInfo priorityField = cameraBehaviour.GetType().GetField("m_Priority", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (priorityField != null)
        {
            priorityField.SetValue(cameraBehaviour, value);
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

            if (disableWhileOpen != null)
            {
                for (int i = 0; i < disableWhileOpen.Length; i++)
                {
                    if (disableWhileOpen[i] != null)
                    {
                        disableWhileOpen[i].enabled = false;
                    }
                }
            }

            return;
        }

        if (disableWhileOpen != null)
        {
            for (int i = 0; i < disableWhileOpen.Length; i++)
            {
                if (disableWhileOpen[i] != null)
                {
                    disableWhileOpen[i].enabled = true;
                }
            }
        }

        if (unlockCursorWhileOpen)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
        }
    }

    private void SetHintVisible(bool isVisible)
    {
        if (interactionHint != null)
        {
            interactionHint.SetActive(isVisible);
        }
    }
}
