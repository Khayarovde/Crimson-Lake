using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Система отладочной телепортации через UI
/// Вешается на игрока, автоматически создает UI канвас
/// </summary>
public class DebugTeleportManager : MonoBehaviour
{
    [Header("Точки телепортации")]
    [SerializeField] private DebugTeleportPoint[] teleportPoints = new DebugTeleportPoint[0];

    [Header("Управление")]
    [SerializeField] private KeyCode openUIKey = KeyCode.P;
    [SerializeField] private KeyCode modifierKey1 = KeyCode.LeftControl;
    [SerializeField] private KeyCode modifierKey2 = KeyCode.LeftAlt;

    [Header("Телепортируемый объект")]
    [SerializeField] private Transform teleportRoot;

    private Canvas teleportCanvas;
    private TMP_InputField inputField;
    private TextMeshProUGUI pointsText;
    private TextMeshProUGUI statusText;
    private Rigidbody playerRigidbody;
    private CharacterController characterController;
    private NavMeshAgent navMeshAgent;

    [Header("UI подсказки")]
    [SerializeField] private string emptyInputMessage = "Введите название точки";
    [SerializeField] private string notFoundMessage = "Точка не найдена";
    [SerializeField] private string noTargetMessage = "У точки нет цели";
    [SerializeField] private string teleportFailedMessage = "Телепорт не удался";

    private void Start()
    {
        CreateUI();
        ResolveTeleportRootAndComponents();
    }

    private void Update()
    {
        if (Input.GetKeyDown(openUIKey) && Input.GetKey(modifierKey1) && Input.GetKey(modifierKey2))
            ToggleUI();

        if (teleportCanvas.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Return))
            ExecuteTeleport();

        if (teleportCanvas.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CloseUI();
    }

    private void CreateUI()
    {
        GameObject canvasObject = new GameObject("DebugTeleportCanvas");
        teleportCanvas = canvasObject.AddComponent<Canvas>();
        teleportCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.03f, 0.04f, 0.8f);

        GameObject contentObject = new GameObject("ContentCard");
        contentObject.transform.SetParent(panelObject.transform, false);
        RectTransform contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.1f, 0.15f);
        contentRect.anchorMax = new Vector2(0.9f, 0.85f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        Image contentImage = contentObject.AddComponent<Image>();
        contentImage.color = new Color(0.08f, 0.1f, 0.12f, 0.92f);

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(contentObject.transform, false);
        labelObject.AddComponent<LayoutElement>().preferredHeight = 80f;

        TextMeshProUGUI labelText = labelObject.AddComponent<TextMeshProUGUI>();
        labelText.text = "Введите название точки телепортации:";
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 28;
        labelText.fontSizeMax = 64;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;

        GameObject inputObject = new GameObject("InputField");
        inputObject.transform.SetParent(contentObject.transform, false);
        inputObject.AddComponent<LayoutElement>().preferredHeight = 96f;

        Image inputImage = inputObject.AddComponent<Image>();
        inputImage.color = new Color(0.95f, 0.96f, 0.98f, 1f);

        inputField = inputObject.AddComponent<TMP_InputField>();
        inputField.targetGraphic = inputImage;
        inputField.lineType = TMP_InputField.LineType.SingleLine;

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(inputObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18, 10);
        textRect.offsetMax = new Vector2(-18, -10);

        TextMeshProUGUI inputText = textObject.AddComponent<TextMeshProUGUI>();
        inputText.text = "";
        inputText.enableAutoSizing = true;
        inputText.fontSizeMin = 30;
        inputText.fontSizeMax = 64;
        inputText.color = Color.black;
        inputField.textComponent = inputText;

        GameObject placeholderObject = new GameObject("Placeholder");
        placeholderObject.transform.SetParent(inputObject.transform, false);
        RectTransform placeholderRect = placeholderObject.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(18, 10);
        placeholderRect.offsetMax = new Vector2(-18, -10);

        TextMeshProUGUI placeholderText = placeholderObject.AddComponent<TextMeshProUGUI>();
        placeholderText.text = "Например: osnova";
        placeholderText.enableAutoSizing = true;
        placeholderText.fontSizeMin = 22;
        placeholderText.fontSizeMax = 48;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        inputField.placeholder = placeholderText;

        GameObject pointsObject = new GameObject("PointsList");
        pointsObject.transform.SetParent(contentObject.transform, false);
        pointsObject.AddComponent<LayoutElement>().preferredHeight = 120f;

        pointsText = pointsObject.AddComponent<TextMeshProUGUI>();
        pointsText.text = BuildPointsList();
        pointsText.enableAutoSizing = true;
        pointsText.fontSizeMin = 20;
        pointsText.fontSizeMax = 34;
        pointsText.alignment = TextAlignmentOptions.Left;
        pointsText.margin = new Vector4(6f, 0f, 6f, 0f);
        pointsText.color = new Color(0.7f, 0.72f, 0.74f, 1f);
        pointsText.enableWordWrapping = true;

        GameObject statusObject = new GameObject("StatusText");
        statusObject.transform.SetParent(contentObject.transform, false);
        statusObject.AddComponent<LayoutElement>().preferredHeight = 48f;

        statusText = statusObject.AddComponent<TextMeshProUGUI>();
        statusText.text = "";
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 18;
        statusText.fontSizeMax = 32;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = new Color(1f, 0.62f, 0.62f, 1f);

        teleportCanvas.gameObject.SetActive(false);
    }

    private void ToggleUI()
    {
        if (teleportCanvas.gameObject.activeSelf)
            CloseUI();
        else
            OpenUI();
    }

    private void OpenUI()
    {
        teleportCanvas.gameObject.SetActive(true);
        inputField.text = "";
        if (pointsText != null)
            pointsText.text = BuildPointsList();
        SetStatus("", false);
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
        StringBuilder builder = new StringBuilder();
        builder.Append("Доступные точки: ");

        bool hasPoints = false;
        foreach (var point in teleportPoints)
        {
            if (point == null || string.IsNullOrWhiteSpace(point.pointName))
                continue;

            if (hasPoints)
                builder.Append(", ");

            builder.Append(point.pointName);
            hasPoints = true;
        }

        if (!hasPoints)
            builder.Append("нет");

        return builder.ToString();
    }

    private void ExecuteTeleport()
    {
        string targetName = inputField.text.Trim().ToLower();

        if (string.IsNullOrEmpty(targetName))
        {
            SetStatus(emptyInputMessage, false);
            return;
        }

        DebugTeleportPoint targetPoint = null;
        foreach (var point in teleportPoints)
        {
            if (point == null || string.IsNullOrWhiteSpace(point.pointName))
                continue;

            if (point.pointName.ToLower() == targetName)
            {
                targetPoint = point;
                break;
            }
        }

        if (targetPoint == null)
        {
            SetStatus(notFoundMessage, false);
            return;
        }

        if (targetPoint.target == null)
        {
            SetStatus(noTargetMessage, false);
            return;
        }

        if (TeleportToPoint(targetPoint))
            CloseUI();
        else
            SetStatus(teleportFailedMessage, false);
    }

    private bool TeleportToPoint(DebugTeleportPoint point)
    {
        Vector3 targetPosition = point.target.position;
        Quaternion targetRotation = point.target.rotation;

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            bool hasNavMeshTarget = NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 0.5f, NavMesh.AllAreas);
            Vector3 navMeshTarget = hasNavMeshTarget ? hit.position : targetPosition;
            bool warped = navMeshAgent.isOnNavMesh && navMeshAgent.Warp(navMeshTarget);
            if (!warped)
            {
                // Fallback when target is off-mesh or agent is in a bad state
                navMeshAgent.enabled = false;
                teleportRoot.position = targetPosition;
                teleportRoot.rotation = targetRotation;
                navMeshAgent.enabled = true;
                if (navMeshAgent.isOnNavMesh)
                    navMeshAgent.Warp(teleportRoot.position);
                navMeshAgent.ResetPath();
                Debug.LogWarning("[DEBUG TELEPORT] Warp failed, fallback to manual move.");
                return IsTeleportSuccessful(targetPosition);
            }

            navMeshAgent.ResetPath();
            teleportRoot.rotation = targetRotation;
        }
        else if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            teleportRoot.position = targetPosition;
            teleportRoot.rotation = targetRotation;
        }
        else if (characterController != null)
        {
            characterController.enabled = false;
            teleportRoot.position = targetPosition;
            teleportRoot.rotation = targetRotation;
            characterController.enabled = true;
        }
        else
        {
            teleportRoot.position = targetPosition;
            teleportRoot.rotation = targetRotation;
        }

        bool success = IsTeleportSuccessful(targetPosition);
        if (success)
            Debug.Log($"[DEBUG TELEPORT] Игрок телепортирован в '{point.pointName}' на позицию {targetPosition}");
        else
            Debug.LogWarning("[DEBUG TELEPORT] Телепорт выполнен, но позиция не изменилась.");

        return success;
    }

    private bool IsTeleportSuccessful(Vector3 targetPosition)
    {
        float distance = Vector3.Distance(teleportRoot.position, targetPosition);
        return distance < 0.5f;
    }

    private void SetStatus(string message, bool isSuccess)
    {
        if (statusText == null)
            return;

        statusText.text = message;
        statusText.color = isSuccess
            ? new Color(0.68f, 1f, 0.72f, 1f)
            : new Color(1f, 0.62f, 0.62f, 1f);
    }

    private void ResolveTeleportRootAndComponents()
    {
        if (teleportRoot == null)
            teleportRoot = transform;

        NavMeshAgent agentInParent = teleportRoot.GetComponentInParent<NavMeshAgent>();
        if (agentInParent != null)
        {
            teleportRoot = agentInParent.transform;
            navMeshAgent = agentInParent;
        }
        else
        {
            navMeshAgent = teleportRoot.GetComponent<NavMeshAgent>();
        }

        CharacterController controllerInParent = teleportRoot.GetComponentInParent<CharacterController>();
        if (navMeshAgent == null && controllerInParent != null)
            teleportRoot = controllerInParent.transform;

        Rigidbody rigidbodyInParent = teleportRoot.GetComponentInParent<Rigidbody>();
        if (navMeshAgent == null && controllerInParent == null && rigidbodyInParent != null)
            teleportRoot = rigidbodyInParent.transform;

        characterController = teleportRoot.GetComponent<CharacterController>();
        playerRigidbody = teleportRoot.GetComponent<Rigidbody>();
    }
}
