using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Система отладочной телепортации через UI.
/// Вешается на игрока, автоматически создаёт UI-канвас.
/// Горячие клавиши: LeftCtrl + LeftAlt + P (по умолчанию).
///
/// ФИКС: Enter теперь через inputField.onSubmit — больше не теряется
/// из-за конфликта с EventSystem/TMP_InputField.
/// </summary>
public class DebugTeleportManager : MonoBehaviour
{
    [Header("Точки телепортации")]
    [SerializeField] private DebugTeleportPoint[] teleportPoints = Array.Empty<DebugTeleportPoint>();

    [Header("Управление")]
    [SerializeField] private KeyCode openUIKey    = KeyCode.P;
    [SerializeField] private KeyCode modifierKey1 = KeyCode.LeftControl;
    [SerializeField] private KeyCode modifierKey2 = KeyCode.LeftAlt;

    [Header("Телепортируемый объект")]
    [SerializeField] private Transform teleportRoot;

    [Header("NavMesh")]
    [Tooltip("Радиус поиска ближайшей точки на NavMesh.")]
    [SerializeField] private float navMeshSampleRadius = 3f;
    [Tooltip("Порог успеха телепорта в метрах.")]
    [SerializeField] private float successThreshold = 1.5f;

    [Header("UI подсказки")]
    [SerializeField] private string emptyInputMessage     = "Введите название точки";
    [SerializeField] private string notFoundMessage       = "Точка не найдена";
    [SerializeField] private string noTargetMessage       = "У точки нет цели";
    [SerializeField] private string teleportFailedMessage = "Телепорт не удался";

    // ── UI ──────────────────────────────────────────────────────────────────
    private Canvas          teleportCanvas;
    private TMP_InputField  inputField;
    private TextMeshProUGUI pointsText;
    private TextMeshProUGUI statusText;

    // ── Компоненты движения ─────────────────────────────────────────────────
    private NavMeshAgent        navMeshAgent;
    private CharacterController characterController;
    private Rigidbody           playerRigidbody;

    // ── Свойства ────────────────────────────────────────────────────────────
    private bool IsUIOpen => teleportCanvas != null && teleportCanvas.gameObject.activeSelf;

    // ───────────────────────────────────────────────────────────────────────
    #region Unity lifecycle

    private void Start()
    {
        CreateUI();
        ResolveTeleportRootAndComponents();
    }

    private void Update()
    {
        // Открытие/закрытие окна — только здесь, не конфликтует с полем ввода
        if (Input.GetKeyDown(openUIKey)
            && Input.GetKey(modifierKey1)
            && Input.GetKey(modifierKey2))
        {
            ToggleUI();
        }

        // Escape закрывает окно (не конфликтует с TMP, т.к. поле его не ест)
        if (IsUIOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseUI();
    }

    #endregion

    // ───────────────────────────────────────────────────────────────────────
    #region UI management

    private void ToggleUI()
    {
        if (IsUIOpen) CloseUI();
        else          OpenUI();
    }

    private void OpenUI()
    {
        teleportCanvas.gameObject.SetActive(true);
        inputField.text = string.Empty;
        pointsText.text = BuildPointsList();
        SetStatus(string.Empty, false);

        // Активируем поле — фокус нужен, чтобы onSubmit работал
        inputField.Select();
        inputField.ActivateInputField();
    }

    private void CloseUI()
    {
        teleportCanvas.gameObject.SetActive(false);
        inputField.DeactivateInputField();
    }

    private string BuildPointsList()
    {
        var sb        = new StringBuilder("Доступные точки: ");
        bool hasPoints = false;

        foreach (var point in teleportPoints)
        {
            if (point == null || string.IsNullOrWhiteSpace(point.pointName)) continue;
            if (hasPoints) sb.Append(", ");
            sb.Append(point.pointName);
            hasPoints = true;
        }

        if (!hasPoints) sb.Append("нет");
        return sb.ToString();
    }

    #endregion

    // ───────────────────────────────────────────────────────────────────────
    #region Teleport logic

    private void ExecuteTeleport()
    {
        string targetName = inputField.text.Trim();

        if (string.IsNullOrEmpty(targetName))
        {
            SetStatus(emptyInputMessage, false);
            // Возвращаем фокус — пользователь может сразу набрать заново
            inputField.Select();
            inputField.ActivateInputField();
            return;
        }

        var point = FindPointByName(targetName);

        if (point == null)
        {
            SetStatus(notFoundMessage, false);
            inputField.Select();
            inputField.ActivateInputField();
            return;
        }

        if (point.target == null)
        {
            SetStatus(noTargetMessage, false);
            inputField.Select();
            inputField.ActivateInputField();
            return;
        }

        if (TeleportToPoint(point))
            CloseUI();
        else
        {
            SetStatus(teleportFailedMessage, false);
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    private DebugTeleportPoint FindPointByName(string name)
    {
        foreach (var point in teleportPoints)
        {
            if (point == null || string.IsNullOrWhiteSpace(point.pointName)) continue;
            if (string.Equals(point.pointName, name, StringComparison.OrdinalIgnoreCase))
                return point;
        }
        return null;
    }

    private bool TeleportToPoint(DebugTeleportPoint point)
    {
        Vector3    targetPos = point.target.position;
        Quaternion targetRot = point.target.rotation;

        if (navMeshAgent != null && navMeshAgent.enabled)
            return TeleportWithNavMesh(point.pointName, targetPos, targetRot);

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity  = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            teleportRoot.SetPositionAndRotation(targetPos, targetRot);
        }
        else if (characterController != null)
        {
            characterController.enabled = false;
            teleportRoot.SetPositionAndRotation(targetPos, targetRot);
            characterController.enabled = true;
        }
        else
        {
            teleportRoot.SetPositionAndRotation(targetPos, targetRot);
        }

        return LogAndReturn(point.pointName, targetPos);
    }

    private bool TeleportWithNavMesh(string pointName, Vector3 targetPos, Quaternion targetRot)
    {
        bool foundOnMesh = NavMesh.SamplePosition(targetPos, out NavMeshHit hit,
                                                  navMeshSampleRadius, NavMesh.AllAreas);
        Vector3 navTarget = foundOnMesh ? hit.position : targetPos;

        if (navMeshAgent.isOnNavMesh && navMeshAgent.Warp(navTarget))
        {
            navMeshAgent.ResetPath();
            teleportRoot.rotation = targetRot;
            return LogAndReturn(pointName, navTarget);
        }

        Debug.LogWarning("[DEBUG TELEPORT] Warp не удался — ручное перемещение.");
        navMeshAgent.enabled = false;
        teleportRoot.SetPositionAndRotation(navTarget, targetRot);
        navMeshAgent.enabled = true;

        if (navMeshAgent.isOnNavMesh)
            navMeshAgent.ResetPath();

        return LogAndReturn(pointName, navTarget);
    }

    private bool LogAndReturn(string pointName, Vector3 navTarget)
    {
        bool success = Vector3.Distance(teleportRoot.position, navTarget) < successThreshold;

        if (success)
            Debug.Log($"[DEBUG TELEPORT] Телепортирован в '{pointName}' → {navTarget}");
        else
            Debug.LogWarning($"[DEBUG TELEPORT] Телепорт в '{pointName}' выполнен, " +
                             $"но позиция не совпадает (факт: {teleportRoot.position}, ожидание: {navTarget})");

        return success;
    }

    #endregion

    // ───────────────────────────────────────────────────────────────────────
    #region Component resolution

    private void ResolveTeleportRootAndComponents()
    {
        if (teleportRoot == null)
            teleportRoot = transform;

        navMeshAgent = teleportRoot.GetComponentInParent<NavMeshAgent>();
        if (navMeshAgent != null)
        {
            teleportRoot        = navMeshAgent.transform;
            characterController = teleportRoot.GetComponent<CharacterController>();
            playerRigidbody     = teleportRoot.GetComponent<Rigidbody>();
            return;
        }

        characterController = teleportRoot.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            teleportRoot    = characterController.transform;
            playerRigidbody = teleportRoot.GetComponent<Rigidbody>();
            return;
        }

        playerRigidbody = teleportRoot.GetComponentInParent<Rigidbody>();
        if (playerRigidbody != null)
            teleportRoot = playerRigidbody.transform;
    }

    #endregion

    // ───────────────────────────────────────────────────────────────────────
    #region UI creation

    private void SetStatus(string message, bool isSuccess)
    {
        if (statusText == null) return;
        statusText.text  = message;
        statusText.color = isSuccess
            ? new Color(0.68f, 1f, 0.72f, 1f)
            : new Color(1f, 0.62f, 0.62f, 1f);
    }

    private void CreateUI()
    {
        // ── Canvas ──────────────────────────────────────────────────────────
        var canvasGO = new GameObject("DebugTeleportCanvas");
        teleportCanvas = canvasGO.AddComponent<Canvas>();
        teleportCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Backdrop ─────────────────────────────────────────────────────────
        var backdropGO   = CreateGO("Backdrop", canvasGO.transform);
        var backdropRect = backdropGO.AddComponent<RectTransform>();
        StretchFull(backdropRect);
        backdropGO.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.04f, 0.80f);

        // ── Content card ─────────────────────────────────────────────────────
        var cardGO   = CreateGO("ContentCard", backdropGO.transform);
        var cardRect = cardGO.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.10f, 0.15f);
        cardRect.anchorMax = new Vector2(0.90f, 0.85f);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
        cardGO.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.12f, 0.92f);

        var layout = cardGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing                = 18f;
        layout.padding                = new RectOffset(28, 28, 28, 28);
        layout.childAlignment         = TextAnchor.UpperCenter;
        layout.childControlWidth      = true;
        layout.childControlHeight     = false;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;

        // ── Label ─────────────────────────────────────────────────────────────
        var labelGO  = CreateGO("Label", cardGO.transform);
        labelGO.AddComponent<LayoutElement>().preferredHeight = 80f;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text             = "Введите название точки телепортации:";
        labelTMP.enableAutoSizing = true;
        labelTMP.fontSizeMin      = 28;
        labelTMP.fontSizeMax      = 64;
        labelTMP.alignment        = TextAlignmentOptions.Center;
        labelTMP.color            = Color.white;

        // ── Input field ──────────────────────────────────────────────────────
        var inputGO    = CreateGO("InputField", cardGO.transform);
        inputGO.AddComponent<LayoutElement>().preferredHeight = 96f;
        var inputImage = inputGO.AddComponent<Image>();
        inputImage.color = new Color(0.95f, 0.96f, 0.98f, 1f);
        inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.targetGraphic = inputImage;
        inputField.lineType      = TMP_InputField.LineType.SingleLine;

        var textGO   = CreateGO("Text", inputGO.transform);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18, 10);
        textRect.offsetMax = new Vector2(-18, -10);
        var inputTMP = textGO.AddComponent<TextMeshProUGUI>();
        inputTMP.enableAutoSizing = true;
        inputTMP.fontSizeMin      = 30;
        inputTMP.fontSizeMax      = 64;
        inputTMP.color            = Color.black;
        inputField.textComponent  = inputTMP;

        var phGO   = CreateGO("Placeholder", inputGO.transform);
        var phRect = phGO.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(18, 10);
        phRect.offsetMax = new Vector2(-18, -10);
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text             = "Например: osnova";
        phTMP.enableAutoSizing = true;
        phTMP.fontSizeMin      = 22;
        phTMP.fontSizeMax      = 48;
        phTMP.color            = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        inputField.placeholder = phTMP;

        // ── КЛЮЧЕВОЕ: вешаем телепорт на onSubmit, а не на Update ────────────
        // onSubmit стреляет именно тогда, когда TMP_InputField получает Enter —
        // без конфликта с EventSystem и без пропусков кадров.
        inputField.onSubmit.AddListener(_ => ExecuteTeleport());

        // ── Points list ──────────────────────────────────────────────────────
        var pointsGO = CreateGO("PointsList", cardGO.transform);
        pointsGO.AddComponent<LayoutElement>().preferredHeight = 120f;
        pointsText                    = pointsGO.AddComponent<TextMeshProUGUI>();
        pointsText.text               = BuildPointsList();
        pointsText.enableAutoSizing   = true;
        pointsText.fontSizeMin        = 20;
        pointsText.fontSizeMax        = 34;
        pointsText.alignment          = TextAlignmentOptions.Left;
        pointsText.margin             = new Vector4(6f, 0f, 6f, 0f);
        pointsText.color              = new Color(0.70f, 0.72f, 0.74f, 1f);
        pointsText.enableWordWrapping = true;

        // ── Status text ──────────────────────────────────────────────────────
        var statusGO = CreateGO("StatusText", cardGO.transform);
        statusGO.AddComponent<LayoutElement>().preferredHeight = 48f;
        statusText                  = statusGO.AddComponent<TextMeshProUGUI>();
        statusText.text             = string.Empty;
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin      = 18;
        statusText.fontSizeMax      = 32;
        statusText.alignment        = TextAlignmentOptions.Center;
        statusText.color            = new Color(1f, 0.62f, 0.62f, 1f);

        teleportCanvas.gameObject.SetActive(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static GameObject CreateGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    #endregion
}