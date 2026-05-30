// #if UNITY_EDITOR || DEVELOPMENT_BUILD
// Remove the #if / #endif guards if you intentionally ship this in release builds.
// DebugTeleportPoint must also be wrapped in the same conditional.

using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Debug-only teleportation overlay.
/// Attach to the player — canvas is created automatically at runtime.
///
/// Hotkey: LeftCtrl + LeftAlt + P (configurable in Inspector).
/// Submit via Enter → TMP_InputField.onSubmit (no per-frame polling, no EventSystem conflicts).
/// </summary>
[AddComponentMenu("Debug/Debug Teleport Manager")]
public sealed class DebugTeleportManager : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Teleport Points")]
    [SerializeField] private DebugTeleportPoint[] teleportPoints = Array.Empty<DebugTeleportPoint>();

    [Header("Hotkeys")]
    [SerializeField] private KeyCode openUIKey    = KeyCode.P;
    [SerializeField] private KeyCode modifierKey1 = KeyCode.LeftControl;
    [SerializeField] private KeyCode modifierKey2 = KeyCode.LeftAlt;

    [Header("Target")]
    [Tooltip("Root transform to teleport. Auto-resolved from NavMeshAgent / CharacterController / Rigidbody if left empty.")]
    [SerializeField] private Transform teleportRoot;

    [Header("NavMesh")]
    [Tooltip("Search radius when snapping to the nearest NavMesh point.")]
    [SerializeField, Min(0f)] private float navMeshSampleRadius = 3f;

    [Header("Status Messages")]
    [SerializeField] private string msgEmptyInput     = "Введите название точки";
    [SerializeField] private string msgNotFound       = "Точка не найдена";
    [SerializeField] private string msgNoTarget       = "У точки нет цели";
    [SerializeField] private string msgTeleportFailed = "ok ";

    // ── Constants ────────────────────────────────────────────────────────────

    // Warn if actual position drifted more than this after teleport (e.g. CC depenetration).
    private const float k_DriftWarnThreshold = 0.5f;

    // ── Static palette ───────────────────────────────────────────────────────

    private static readonly Color s_ColSuccess = new(0.68f, 1.00f, 0.72f, 1f);
    private static readonly Color s_ColError   = new(1.00f, 0.62f, 0.62f, 1f);

    // ── UI refs ──────────────────────────────────────────────────────────────

    private Canvas          _canvas;
    private TMP_InputField  _inputField;
    private TextMeshProUGUI _pointsText;
    private TextMeshProUGUI _statusText;

    // ── Movement components ──────────────────────────────────────────────────

    private NavMeshAgent        _navMeshAgent;
    private CharacterController _characterController;
    private Rigidbody           _rigidbody;

    // ── State ────────────────────────────────────────────────────────────────

    private bool IsUIOpen => _canvas != null && _canvas.gameObject.activeSelf;

    // ────────────────────────────────────────────────────────────────────────
    #region Unity lifecycle

    private void Awake()
    {
        // Awake, not Start: components must be ready before any other script
        // might call into us during Start.
        EnsureEventSystem();
        ResolveTeleportRoot();
        BuildUI();
    }

    private void OnDestroy()
    {
        // Always unsubscribe — leaked UnityEvents hold a GC root to this object.
        if (_inputField != null)
            _inputField.onSubmit.RemoveListener(OnInputSubmit);
    }

    private void Update()
    {
        if (Input.GetKeyDown(openUIKey)
            && Input.GetKey(modifierKey1)
            && Input.GetKey(modifierKey2))
        {
            ToggleUI();
            return;
        }

        if (IsUIOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseUI();
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region UI control

    private void ToggleUI()
    {
        if (IsUIOpen) CloseUI();
        else          OpenUI();
    }

    private void OpenUI()
    {
        _canvas.gameObject.SetActive(true);
        _inputField.text = string.Empty;
        _pointsText.text = BuildPointsList();
        SetStatus(string.Empty, success: true);
        FocusInput();
    }

    private void CloseUI()
    {
        _canvas.gameObject.SetActive(false);
        _inputField.DeactivateInputField();
    }

    /// <summary>Returns keyboard focus to the input field after showing an error.</summary>
    private void FocusInput()
    {
        _inputField.Select();
        _inputField.ActivateInputField();
    }

    private void SetStatus(string message, bool success)
    {
        if (_statusText == null) return;
        _statusText.text  = message;
        _statusText.color = success ? s_ColSuccess : s_ColError;
    }

    private string BuildPointsList()
    {
        var  sb  = new StringBuilder("Доступные точки: ");
        bool any = false;

        foreach (var p in teleportPoints)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.pointName)) continue;
            if (any) sb.Append(", ");
            sb.Append(p.pointName);
            any = true;
        }

        if (!any) sb.Append("нет");
        return sb.ToString();
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region Teleport logic

    // Named method (not a lambda) so RemoveListener works correctly.
    private void OnInputSubmit(string _) => ExecuteTeleport();

    private void ExecuteTeleport()
    {
        string input = _inputField.text.Trim();

        if (string.IsNullOrEmpty(input))
        {
            SetStatus(msgEmptyInput, success: false);
            FocusInput();
            return;
        }

        var point = FindPoint(input);
        if (point == null)
        {
            SetStatus(msgNotFound, success: false);
            FocusInput();
            return;
        }

        if (point.target == null)
        {
            SetStatus(msgNoTarget, success: false);
            FocusInput();
            return;
        }

        if (Teleport(point))
        {
            CloseUI();
        }
        else
        {
            SetStatus(msgTeleportFailed, success: false);
            FocusInput();
        }
    }

    private DebugTeleportPoint FindPoint(string name)
    {
        foreach (var p in teleportPoints)
        {
            if (p != null
                && !string.IsNullOrWhiteSpace(p.pointName)
                && string.Equals(p.pointName, name, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    private bool Teleport(DebugTeleportPoint point)
    {
        Vector3    pos = point.target.position;
        Quaternion rot = point.target.rotation;

        // NavMeshAgent takes priority — it owns the transform on NavMesh objects.
        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled)
            return TeleportWithNavMeshAgent(point.pointName, pos, rot);

        return TeleportDirect(point.pointName, pos, rot);
    }

    /// <summary>
    /// Direct teleport: handles Rigidbody, CharacterController, and plain Transform.
    /// Always succeeds unless the platform is completely broken.
    /// </summary>
    private bool TeleportDirect(string pointName, Vector3 pos, Quaternion rot)
    {
        if (_rigidbody != null)
        {
            // Zero velocity so the object doesn't keep moving after teleport.
            _rigidbody.linearVelocity  = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            // Use Rigidbody.position/rotation (not Transform) to keep physics in sync.
            _rigidbody.position = pos;
            _rigidbody.rotation = rot;
        }
        else if (_characterController != null)
        {
            // CharacterController intercepts Transform.position, must be disabled first.
            _characterController.enabled = false;
            teleportRoot.SetPositionAndRotation(pos, rot);
            _characterController.enabled = true;
        }
        else
        {
            teleportRoot.SetPositionAndRotation(pos, rot);
        }

        // Unity 6: flush transform changes to the physics engine immediately.
        Physics.SyncTransforms();

        return CheckAndLogResult(pointName, pos);
    }

    /// <summary>
    /// NavMeshAgent teleport: prefer Warp() for in-mesh moves,
    /// fall back to disable → move → re-enable when not on mesh.
    /// </summary>
    private bool TeleportWithNavMeshAgent(string pointName, Vector3 targetPos, Quaternion targetRot)
    {
        bool onMesh = NavMesh.SamplePosition(
            targetPos, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas);

        Vector3 dest = onMesh ? hit.position : targetPos;

        // Fast path: Warp handles everything if the agent is already on the mesh.
        if (_navMeshAgent.isOnNavMesh && _navMeshAgent.Warp(dest))
        {
            _navMeshAgent.ResetPath();
            teleportRoot.rotation = targetRot;
            Physics.SyncTransforms();
            return CheckAndLogResult(pointName, dest);
        }

        // Fallback: manual move (agent was off-mesh or Warp returned false).
        Debug.LogWarning($"[DebugTeleport] Деформация не удалась из-за '{pointName}' — использование ручного перемещения.");
        _navMeshAgent.enabled = false;
        teleportRoot.SetPositionAndRotation(dest, targetRot);
        Physics.SyncTransforms();
        _navMeshAgent.enabled = true;

        if (_navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.ResetPath();
        }
        else
        {
            // Destination was off NavMesh and agent couldn't snap — still succeeds
            // as a debug tool but worth knowing about.
            Debug.LogWarning($"[DebugTeleport] Agent for '{pointName}' is not on NavMesh after teleport.");
        }

        return CheckAndLogResult(pointName, dest);
    }

    /// <summary>
    /// Logs the result and returns true unless the position drift is extreme
    /// (e.g. physics pushed the character into a wall and depenetrated it far away).
    /// Small drifts from CharacterController depenetration are expected and accepted.
    /// </summary>
    private bool CheckAndLogResult(string pointName, Vector3 expected)
    {
        float drift = Vector3.Distance(teleportRoot.position, expected);

        if (drift <= k_DriftWarnThreshold)
        {
            Debug.Log($"[DebugTeleport] ✓ '{pointName}' → {teleportRoot.position:F2}");
            return true;
        }

        // Large drift likely means the destination is inside geometry.
        Debug.LogWarning(
            $"[DebugTeleport] '{pointName}' teleported but drifted {drift:F2}m " +
            $"(landed: {teleportRoot.position:F2}, expected: {expected:F2}). " +
            "Destination may be inside geometry.");
        return false;
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region Component resolution

    private void ResolveTeleportRoot()
    {
        if (teleportRoot == null)
            teleportRoot = transform;

        // Priority: NavMeshAgent → CharacterController → Rigidbody.
        // includeInactive: true — find components even on disabled parent objects.
        _navMeshAgent = teleportRoot.GetComponentInParent<NavMeshAgent>(includeInactive: true);
        if (_navMeshAgent != null)
        {
            teleportRoot         = _navMeshAgent.transform;
            _characterController = teleportRoot.GetComponent<CharacterController>();
            _rigidbody           = teleportRoot.GetComponent<Rigidbody>();
            return;
        }

        _characterController = teleportRoot.GetComponentInParent<CharacterController>(includeInactive: true);
        if (_characterController != null)
        {
            teleportRoot = _characterController.transform;
            _rigidbody   = teleportRoot.GetComponent<Rigidbody>();
            return;
        }

        _rigidbody = teleportRoot.GetComponentInParent<Rigidbody>(includeInactive: true);
        if (_rigidbody != null)
            teleportRoot = _rigidbody.transform;
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region UI construction

    /// <summary>
    /// Creates a minimal EventSystem if none exists in the scene.
    /// TMP_InputField won't receive keyboard input without one.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem (auto)");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
        Debug.LogWarning("[DebugTeleport] No EventSystem found — created one automatically.");
    }

    private void BuildUI()
    {
        _canvas     = BuildCanvas();
        var card    = BuildCard(_canvas.transform);
        BuildLabel(card);
        _inputField = BuildInputField(card);
        _pointsText = BuildPointsText(card);
        _statusText = BuildStatusText(card);

        _inputField.onSubmit.AddListener(OnInputSubmit);
        _canvas.gameObject.SetActive(false);
    }

    private static Canvas BuildCanvas()
    {
        var go     = new GameObject("DebugTeleportCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Renders above everything else

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Creates backdrop + content card, returns card's RectTransform.</summary>
    private static RectTransform BuildCard(Transform canvasTransform)
    {
        var backdrop = CreateUIObject("Backdrop", canvasTransform);
        StretchFull(backdrop.AddComponent<RectTransform>());
        backdrop.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.04f, 0.80f);

        var card   = CreateUIObject("ContentCard", backdrop.transform);
        var cardRT = card.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.10f, 0.15f);
        cardRT.anchorMax = new Vector2(0.90f, 0.85f);
        cardRT.offsetMin = cardRT.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.12f, 0.92f);

        var vl = card.AddComponent<VerticalLayoutGroup>();
        vl.spacing               = 18f;
        vl.padding               = new RectOffset(28, 28, 28, 28);
        vl.childAlignment        = TextAnchor.UpperCenter;
        vl.childControlWidth     = true;
        vl.childControlHeight    = false;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        return cardRT;
    }

    private static void BuildLabel(RectTransform parent)
    {
        var go  = CreateUIObject("Label", parent.transform);
        go.AddComponent<LayoutElement>().preferredHeight = 80f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = "Введите название точки телепортации:";
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = 28;
        tmp.fontSizeMax      = 64;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = Color.white;
    }

    private static TMP_InputField BuildInputField(RectTransform parent)
    {
        var go    = CreateUIObject("InputField", parent.transform);
        go.AddComponent<LayoutElement>().preferredHeight = 96f;
        var bg    = go.AddComponent<Image>();
        bg.color  = new Color(0.95f, 0.96f, 0.98f, 1f);

        var field           = go.AddComponent<TMP_InputField>();
        field.targetGraphic = bg;
        field.lineType      = TMP_InputField.LineType.SingleLine;

        // Text component
        var textGO  = CreateUIObject("Text", go.transform);
        var textRT  = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(18f, 10f);
        textRT.offsetMax = new Vector2(-18f, -10f);
        var inputTMP = textGO.AddComponent<TextMeshProUGUI>();
        inputTMP.enableAutoSizing = true;
        inputTMP.fontSizeMin      = 30;
        inputTMP.fontSizeMax      = 64;
        inputTMP.color            = Color.black;
        field.textComponent       = inputTMP;

        // Placeholder
        var phGO  = CreateUIObject("Placeholder", go.transform);
        var phRT  = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(18f, 10f);
        phRT.offsetMax = new Vector2(-18f, -10f);
        var phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text             = "Например: osnova";
        phTMP.enableAutoSizing = true;
        phTMP.fontSizeMin      = 22;
        phTMP.fontSizeMax      = 48;
        phTMP.color            = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        field.placeholder      = phTMP;

        return field;
    }

    private static TextMeshProUGUI BuildPointsText(RectTransform parent)
    {
        var go  = CreateUIObject("PointsList", parent.transform);
        go.AddComponent<LayoutElement>().preferredHeight = 120f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.enableAutoSizing   = true;
        tmp.fontSizeMin        = 20;
        tmp.fontSizeMax        = 34;
        tmp.alignment          = TextAlignmentOptions.Left;
        tmp.margin             = new Vector4(6f, 0f, 6f, 0f);
        tmp.color              = new Color(0.70f, 0.72f, 0.74f, 1f);
        tmp.enableWordWrapping = true;
        return tmp;
    }

    private static TextMeshProUGUI BuildStatusText(RectTransform parent)
    {
        var go  = CreateUIObject("StatusText", parent.transform);
        go.AddComponent<LayoutElement>().preferredHeight = 48f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = string.Empty;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin      = 18;
        tmp.fontSizeMax      = 32;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = s_ColError;
        return tmp;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, worldPositionStays: false);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    #endregion
}

// #endif