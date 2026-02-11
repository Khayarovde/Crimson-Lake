using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class SaveSlotsUI : MonoBehaviour
{
    public static SaveSlotsUI Instance { get; private set; }
    [Header("UI Root")]
    [SerializeField] private GameObject canvasRoot;
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private float canvasFadeDuration = 0.2f;

    [Header("Slots")]
    [SerializeField] private Button[] slotButtons = new Button[4];
    [SerializeField] private TMP_Text[] slotLabels = new TMP_Text[4];
    [SerializeField] private Text[] slotLabelsLegacy = new Text[4];
    [SerializeField] private Image[] slotImages = new Image[4];
    [SerializeField] private bool autoToggleSlotImages = true;

    [Header("Confirm Overwrite")]
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;
    [SerializeField] private Text confirmTextLegacy;

    [Header("Confirm Delete All")]
    [SerializeField] private GameObject deleteConfirmPanel;
    [SerializeField] private Button deleteConfirmYesButton;
    [SerializeField] private Button deleteConfirmNoButton;
    [SerializeField] private Text deleteConfirmTextLegacy;

    [Header("Continue Without Saving")]
    [SerializeField] private Button continueWithoutSavingButton;
    [SerializeField] private string continueWarningText = "вы об этом пожелеете";

    [Header("Warning UI")]
    [SerializeField] private SaveWarningUI warningUI;
    [SerializeField] private TMP_Text warningTextTMP;
    [SerializeField] private Text warningTextLegacy;
    [SerializeField] private float warningDisplaySeconds = 2.5f;

    [Header("Input Lock")]
    [SerializeField] private bool pauseTimeWhileOpen = false;
    [SerializeField] private bool unlockCursorWhileOpen = true;
    [SerializeField] private Behaviour[] disableWhileOpen;

    private SaveManager saveManager;
    private int pendingOverwriteSlot = -1;
    private bool isOpen;
    private Coroutine warningRoutine;
    private Coroutine canvasFadeRoutine;
    private CanvasGroup canvasGroup;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private bool[] previousBehaviourStates;
    private bool isInitialized;
    private int openedFrame = -1;

    public bool IsOpen => isOpen;
    public static bool IsSaveMenuOpen { get; private set; }

    private void Awake()
    {
        Instance = this;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;

        if (canvasRoot == null)
            canvasRoot = gameObject;

        ResolveSaveManager();
        HookupButtons();

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);

        isInitialized = true;
    }

    private void Update()
    {
        if (!isOpen) return;

        if (Time.frameCount == openedFrame)
            return;

        if (unlockCursorWhileOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }


        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
        {
            if (confirmPanel != null && confirmPanel.activeSelf)
            {
                CancelOverwrite();
                return;
            }

            Hide();
        }
    }

    private void ResolveSaveManager()
    {
        if (SaveManager.Instance != null)
        {
            saveManager = SaveManager.Instance;
            return;
        }

        var go = new GameObject("SaveManager");
        saveManager = go.AddComponent<SaveManager>();
    }

    private void HookupButtons()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slotIndex = i;
            if (slotButtons[i] != null)
            {
                slotButtons[i].onClick.AddListener(() => OnSlotPressed(slotIndex));
            }
        }

        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(ConfirmOverwrite);
        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(CancelOverwrite);
        if (continueWithoutSavingButton != null)
            continueWithoutSavingButton.onClick.AddListener(ContinueWithoutSaving);

        if (deleteConfirmYesButton != null)
            deleteConfirmYesButton.onClick.AddListener(ConfirmDeleteAll);
        if (deleteConfirmNoButton != null)
            deleteConfirmNoButton.onClick.AddListener(CancelDeleteAll);
    }

    public void Show()
    {
        EnsureInitialized();

        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);

        isOpen = true;
        IsSaveMenuOpen = true;
        openedFrame = Time.frameCount;
        saveManager.MarkUnsaved();
        RefreshSlots();
        EnsureEventSystem();
        ApplyInputLock(true);
        ForcePanelToTop();
        EnableAllButtons();
        StartCanvasFade(true);
    }

    public void Hide()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);
        isOpen = false;
        IsSaveMenuOpen = false;
        pendingOverwriteSlot = -1;
        ApplyInputLock(false);
        StartCanvasFade(false);
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < slotLabels.Length; i++)
        {
            if (slotLabels[i] == null) continue;
            string summary = saveManager.GetSaveSummary(i);
            slotLabels[i].text = $" {summary}";
        }

        for (int i = 0; i < slotLabelsLegacy.Length; i++)
        {
            if (slotLabelsLegacy[i] == null) continue;
            string summary = saveManager.GetSaveSummary(i);
            slotLabelsLegacy[i].text = " " + summary;
        }

        if (autoToggleSlotImages && slotImages != null)
        {
            int count = slotImages.Length;
            for (int i = 0; i < count; i++)
            {
                Image image = slotImages[i];
                if (image == null) continue;
                image.enabled = saveManager.HasSave(i);
            }
        }
    }

    private void OnSlotPressed(int slotIndex)
    {
        if (saveManager.HasSave(slotIndex))
        {
            pendingOverwriteSlot = slotIndex;
            if (confirmPanel != null)
                confirmPanel.SetActive(true);
            return;
        }

        SaveToSlot(slotIndex);
    }

    private void SaveToSlot(int slotIndex)
    {
        saveManager.SaveSlot(slotIndex);
        RefreshSlots();
        Hide();
    }

    private void ConfirmOverwrite()
    {
        if (pendingOverwriteSlot < 0)
        {
            if (confirmPanel != null)
                confirmPanel.SetActive(false);
            return;
        }

        SaveToSlot(pendingOverwriteSlot);
        pendingOverwriteSlot = -1;

        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    private void CancelOverwrite()
    {
        pendingOverwriteSlot = -1;
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    public void RequestDeleteAllSaves()
    {
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(true);
    }

    private void ConfirmDeleteAll()
    {
        if (saveManager != null)
            saveManager.DeleteAllSaves();

        RefreshSlots();

        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);
    }

    private void CancelDeleteAll()
    {
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);
    }

    private void ContinueWithoutSaving()
    {
        saveManager.MarkUnsaved();
        Hide();

        ShowWarningMessage(continueWarningText);
    }

    private void ShowWarningMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (warningUI == null)
            warningUI = FindObjectOfType<SaveWarningUI>();

        if (warningUI != null)
        {
            warningUI.ShowWarning(message);
            return;
        }

        if (warningTextTMP != null)
            warningTextTMP.text = message;
        if (warningTextLegacy != null)
            warningTextLegacy.text = message;

        SetWarningVisible(true);

        if (warningRoutine != null)
            StopCoroutine(warningRoutine);

        if (warningDisplaySeconds > 0f)
            warningRoutine = StartCoroutine(HideWarningAfterDelay(warningDisplaySeconds));
    }

    private System.Collections.IEnumerator HideWarningAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        SetWarningVisible(false);
    }

    private void SetWarningVisible(bool visible)
    {
        if (warningTextTMP != null)
            warningTextTMP.gameObject.SetActive(visible);
        if (warningTextLegacy != null)
            warningTextLegacy.gameObject.SetActive(visible);
    }

    private void EnsureRaycastState(bool active)
    {
        if (canvasRoot == null) return;

        if (canvasGroup == null)
            canvasGroup = canvasRoot.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = canvasRoot.AddComponent<CanvasGroup>();

        canvasGroup.interactable = active;
        canvasGroup.blocksRaycasts = active;
        canvasGroup.alpha = active ? 1f : 0f;
    }

    private void StartCanvasFade(bool show)
    {
        if (canvasFadeRoutine != null)
            StopCoroutine(canvasFadeRoutine);

        canvasFadeRoutine = StartCoroutine(FadeCanvas(show));
    }

    private System.Collections.IEnumerator FadeCanvas(bool show)
    {
        if (canvasRoot == null)
            yield break;

        if (canvasGroup == null)
            canvasGroup = canvasRoot.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = canvasRoot.AddComponent<CanvasGroup>();

        float duration = Mathf.Max(0.01f, canvasFadeDuration);
        float startAlpha = canvasGroup.alpha;
        float targetAlpha = show ? 1f : 0f;

        canvasGroup.interactable = show;
        canvasGroup.blocksRaycasts = show;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (!show && canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            Debug.LogWarning("[SaveSlotsUI] EventSystem не найден, создан автоматически.");
        }

        if (canvasRoot != null)
        {
            var canvas = canvasRoot.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.LogWarning("[SaveSlotsUI] GraphicRaycaster не найден, добавлен автоматически.");
            }
        }
    }

    private void ApplyInputLock(bool lockInput)
    {
        if (lockInput)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            if (unlockCursorWhileOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (disableWhileOpen != null && disableWhileOpen.Length > 0)
            {
                previousBehaviourStates = new bool[disableWhileOpen.Length];
                for (int i = 0; i < disableWhileOpen.Length; i++)
                {
                    if (disableWhileOpen[i] == null) continue;
                    previousBehaviourStates[i] = disableWhileOpen[i].enabled;
                    disableWhileOpen[i].enabled = false;
                }
            }
        }
        else
        {
            if (unlockCursorWhileOpen)
            {
                Cursor.visible = previousCursorVisible;
                Cursor.lockState = previousCursorLock;
            }

            if (disableWhileOpen != null && previousBehaviourStates != null)
            {
                for (int i = 0; i < disableWhileOpen.Length; i++)
                {
                    if (disableWhileOpen[i] == null) continue;
                    disableWhileOpen[i].enabled = previousBehaviourStates[i];
                }
            }
        }
    }

    private void ForcePanelToTop()
    {
        if (canvasRoot == null) return;

        Canvas canvas = canvasRoot.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 1000;
            canvas.overrideSorting = true;
        }

        canvasRoot.transform.SetAsLastSibling();
        if (confirmPanel != null)
            confirmPanel.transform.SetAsLastSibling();
    }

    private void EnableAllButtons()
    {
        if (canvasRoot == null) return;

        Button[] buttons = canvasRoot.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn == null) continue;
            btn.interactable = true;
            Image img = btn.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = true;
        }

        Slider[] sliders = canvasRoot.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            if (slider == null) continue;

            if (slider.fillRect != null)
            {
                Image fill = slider.fillRect.GetComponent<Image>();
                if (fill != null)
                    fill.raycastTarget = true;
            }

            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                if (handle != null)
                    handle.raycastTarget = true;
            }
        }
    }


}
