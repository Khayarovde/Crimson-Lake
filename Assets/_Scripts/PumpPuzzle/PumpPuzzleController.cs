using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Контроллер пазла с переливанием жидкости (A, B, C).
/// </summary>
public class PumpPuzzleController : MonoBehaviour
{
    private enum PumpAction
    {
        FillAFromSource,
        TransferAtoB,
        TransferAtoC,
        TransferBtoA,
        TransferBtoC,
        TransferCtoA,
        TransferCtoB,
        ResetPuzzle
    }

    [System.Serializable]
    private class PumpButtonBinding
    {
        public Button button;
        public PumpAction action;
    }

    // ─── Events ───────────────────────────────────────────────────────────────
    [Header("Events")]
    [SerializeField] private UnityEvent onPuzzleSolved;

    // ─── Tank UI ──────────────────────────────────────────────────────────────
    [Header("Tank UI (A, B, C)")]
    [Tooltip("Используйте Image с типом Filled (Vertical, Bottom) для заливки без искажений.")]
    [SerializeField] private Image[] tankFillImages = new Image[3];
    [SerializeField] private RectTransform[] tankShakeTargets = new RectTransform[3];
    
    [Header("Adaptive Visual Mapping")]
    [Tooltip("Настройка кривой для адаптации логического объема (X) к визуальной заливке (Y). Позволяет подстроить 0.125 под нужную высоту на спрайте.")]
    [SerializeField] private AnimationCurve[] tankVisualCurves = new AnimationCurve[3];
    
    [Header("Scale Fallback (Legacy)")]
    [SerializeField] private RectTransform[] tankLiquidScaleTargets = new RectTransform[3];
    [SerializeField] private bool useScaleVisualFallback = false; // Рекомендуется false при использовании Image Fill

    // ─── Tank State Sprites ───────────────────────────────────────────────────
    [Header("Tank State Sprites (A, B, C)")]
    [SerializeField] private Image[] tankStateLow = new Image[3];
    [SerializeField] private Image[] tankStateMid = new Image[3];
    [SerializeField] private Image[] tankStateHigh = new Image[3];
    [SerializeField] private float stateMidThreshold = 0.376f;
    [SerializeField] private float stateHighThreshold = 0.76f;

    // ─── Bubble FX ────────────────────────────────────────────────────────────
    [Header("Bubble FX (optional)")]
    [SerializeField] private RectTransform[] bubbleRects;
    [SerializeField] private float bubbleRise = 24f;
    [SerializeField] private float bubbleDuration = 0.45f;

    // ─── UI State ─────────────────────────────────────────────────────────────
    [Header("UI State")]
    [SerializeField] private Button[] actionButtons;
    [SerializeField] private GameObject resetOptionRoot;
    [SerializeField] private Text statusText;
    [SerializeField] private CanvasGroup fadeToBlackCanvasGroup;

    // ─── Button Bindings ──────────────────────────────────────────────────────
    [Header("Runtime Button Binding (optional)")]
    [SerializeField] private bool useRuntimeButtonBindings = true;
    [SerializeField] private bool clearExistingButtonListeners = false;
    [SerializeField] private PumpButtonBinding[] buttonBindings;

    // ─── Puzzle Values ────────────────────────────────────────────────────────
    [Header("Puzzle Values")]
    [SerializeField] private float[] initialLevels = { 1f, 0f, 0f };
    [SerializeField] private float[] targetLevels = { 0.5f, 0.75f, 0f };
    [SerializeField] private float fillStep = 0.125f;
    [SerializeField] private bool stepwiseTransfer = true;
    [SerializeField] private float levelTolerance = 0.001f;

    [Header("Persistence")]
    [PickupId]
    [SerializeField] private string puzzleId;
    [SerializeField] private bool invokeSolvedEventOnLoad = true;

    // ─── Gamepad / Input debounce ─────────────────────────────────────────────
    [Header("Input Protection")]
    [SerializeField] private float actionCooldown = 0.4f;

    // ─── Success Flow ─────────────────────────────────────────────────────────
    [Header("Success Flow")]
    [SerializeField] private bool allowInfinitePlayAfterSuccess = false;
    [SerializeField] private bool autoResetOnSuccessInInfiniteMode = true;
    [SerializeField] private float autoResetDelay = 0.85f;

    // ─── Tween Settings ───────────────────────────────────────────────────────
    [Header("Tween Settings")]
    [SerializeField] private float fillTweenDuration = 0.35f;
    [SerializeField] private float overflowShakeDuration = 0.35f;
    [SerializeField] private float overflowShakeStrength = 20f;

    // ─── Debug ────────────────────────────────────────────────────────────────
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // ─── Runtime state ────────────────────────────────────────────────────────
    private readonly float[] levels = new float[3];
    private readonly Vector3[] tankBaseScales = new Vector3[3];
    private bool isSolved;
    private float lastActionTime = -999f;

    public bool IsSolved => isSolved;

    private void Awake()
    {
        EnsureArraySize(ref initialLevels);
        EnsureArraySize(ref targetLevels);
        EnsureCurveArraySize();
    }

    private void Start()
    {
        ConfigureRuntimeButtonBindings();
        ValidateButtonBindings();
        ValidateAndPrepareTankImages();
        if (!ApplyPersistenceState())
        {
            ResetPuzzle();
        }
    }

    private void Update()
    {
        if (!isSolved && !string.IsNullOrWhiteSpace(puzzleId))
        {
            if (SaveManager.HasPuzzleSolved(puzzleId))
            {
                ApplyPersistenceState();
            }
        }
    }

    public void ResetPuzzle() => ResetPuzzleInternal(false);

    public void FillAFromSource()
    {
        if (!TryConsumeActionCooldown("FillAFromSource")) return;

        if (isSolved) return;

        if (levels[0] + fillStep > 1f + levelTolerance)
        {
            TriggerOverflow(0);
            return;
        }

        levels[0] = RoundToStep(Mathf.Clamp01(levels[0] + fillStep));
        SetTankVisual(0, levels[0], true);
        PlayBubbles();
        EvaluateWinCondition();
    }

    public void TransferAtoB() => Transfer(0, 1);
    public void TransferAtoC() => Transfer(0, 2);
    public void TransferBtoA() => Transfer(1, 0);
    public void TransferBtoC() => Transfer(1, 2);
    public void TransferCtoA() => Transfer(2, 0);
    public void TransferCtoB() => Transfer(2, 1);

    private void ResetPuzzleInternal(bool forceReset)
    {
        if (!forceReset && isSolved && !allowInfinitePlayAfterSuccess) return;

        isSolved = false;
        lastActionTime = -999f;

        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = Mathf.Clamp01(initialLevels[i]);
            SetTankVisual(i, levels[i], false);
        }

        if (resetOptionRoot != null) resetOptionRoot.SetActive(false);

        if (fadeToBlackCanvasGroup != null)
        {
            fadeToBlackCanvasGroup.alpha = 0f;
            fadeToBlackCanvasGroup.blocksRaycasts = false;
        }

        SetButtonsInteractable(true);
        SetStatus("Электрощит");
    }

    private void Transfer(int from, int to)
    {
        if (!TryConsumeActionCooldown($"Transfer {TankName(from)}->{TankName(to)}")) return;
        if (isSolved) return;

        if (levels[from] <= levelTolerance)
        {
            SetStatus("Источник пуст.");
            return;
        }

        float freeSpace = 1f - levels[to];
        if (freeSpace <= levelTolerance)
        {
            TriggerOverflow(to);
            return;
        }

        float transferLimit = stepwiseTransfer ? Mathf.Max(levelTolerance, fillStep) : 1f;
        float amount = Mathf.Min(levels[from], freeSpace, transferLimit);
        amount = RoundToStep(amount);

        levels[from] = RoundToStep(Mathf.Clamp01(levels[from] - amount));
        levels[to]   = RoundToStep(Mathf.Clamp01(levels[to]   + amount));

        SetTankVisual(from, levels[from], true);
        SetTankVisual(to,   levels[to],   true);
        PlayBubbles();

        EvaluateWinCondition();
    }

    private float RoundToStep(float value)
    {
        if (fillStep <= 0f) return value;
        return Mathf.Round(value / fillStep) * fillStep;
    }

    private bool TryConsumeActionCooldown(string actionName)
    {
        float now = Time.unscaledTime;
        if (now - lastActionTime < actionCooldown) return false;
        lastActionTime = now;
        return true;
    }

    private void EvaluateWinCondition()
    {
        for (int i = 0; i < levels.Length; i++)
        {
            if (Mathf.Abs(levels[i] - targetLevels[i]) > levelTolerance)
            {
                SetStatus($"A={levels[0]:0.###}  B={levels[1]:0.###}  C={levels[2]:0.###}");
                return;
            }
        }
        OnSolved();
    }

    private void OnSolved()
    {
        isSolved = true;

        if (!string.IsNullOrWhiteSpace(puzzleId))
            SaveManager.MarkPuzzleSolved(puzzleId);

        if (!allowInfinitePlayAfterSuccess)
        {
            SetButtonsInteractable(false);
            SetStatus("Успех! Ток есть!");
        }
        else
        {
            SetStatus("Успех! Бесконечный режим активен.");
        }

        onPuzzleSolved?.Invoke();

        if (fadeToBlackCanvasGroup != null)
        {
            fadeToBlackCanvasGroup.DOKill();
            fadeToBlackCanvasGroup.blocksRaycasts = true;
            fadeToBlackCanvasGroup.DOFade(1f, 1.1f).SetEase(Ease.InOutSine).SetDelay(0.35f);
        }

        if (allowInfinitePlayAfterSuccess && autoResetOnSuccessInInfiniteMode)
        {
            DOVirtual.DelayedCall(autoResetDelay, () => ResetPuzzleInternal(true));
        }
    }

    private bool ApplyPersistenceState()
    {
        if (string.IsNullOrWhiteSpace(puzzleId))
            return false;

        if (!SaveManager.HasPuzzleSolved(puzzleId))
            return false;

        isSolved = true;
        lastActionTime = -999f;

        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = Mathf.Clamp01(targetLevels[i]);
            SetTankVisual(i, levels[i], false);
        }

        if (resetOptionRoot != null) resetOptionRoot.SetActive(false);

        if (fadeToBlackCanvasGroup != null)
        {
            fadeToBlackCanvasGroup.alpha = 0f;
            fadeToBlackCanvasGroup.blocksRaycasts = false;
        }

        if (!allowInfinitePlayAfterSuccess)
        {
            SetButtonsInteractable(false);
            SetStatus("Успех! Ток есть!");
        }
        else
        {
            SetButtonsInteractable(true);
            SetStatus("Успех! Бесконечный режим активен.");
        }

        if (invokeSolvedEventOnLoad)
            onPuzzleSolved?.Invoke();

        return true;
    }

    private void TriggerOverflow(int tankIndex)
    {
        SetStatus("Переполнение! По блоку, можете нажать на Сброс.");

        if (tankIndex >= 0 && tankIndex < tankShakeTargets.Length && tankShakeTargets[tankIndex] != null)
        {
            tankShakeTargets[tankIndex].DOKill();
            tankShakeTargets[tankIndex].DOShakeAnchorPos(overflowShakeDuration, overflowShakeStrength, 25, 90f, false, true);
        }

        if (resetOptionRoot != null) resetOptionRoot.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Visual (Адаптивная заливка)
    // ─────────────────────────────────────────────────────────────────────────

    private void SetTankVisual(int index, float logicalValue, bool animate)
    {
        // Применяем кривую для конвертации "математики" в "визуал спрайта"
        float visualValue = logicalValue;
        if (tankVisualCurves != null && index < tankVisualCurves.Length && tankVisualCurves[index] != null && tankVisualCurves[index].length > 0)
        {
            visualValue = tankVisualCurves[index].Evaluate(logicalValue);
        }

        if (index >= 0 && index < tankFillImages.Length && tankFillImages[index] != null)
        {
            Image target = tankFillImages[index];

            if (!animate)
            {
                target.fillAmount = visualValue;
            }
            else
            {
                target.DOKill();
                // Адаптивная DOTween анимация заливки "снизу вверх"
                target.DOFillAmount(visualValue, fillTweenDuration).SetEase(Ease.OutCubic);
            }
        }

        ApplyScaleFallbackVisual(index, visualValue, animate);
        UpdateTankStateSprites(index, logicalValue); // Состояния опираются на математический объем, а не визуал
    }

    private void ValidateAndPrepareTankImages()
    {
        for (int i = 0; i < tankFillImages.Length; i++)
        {
            if (tankFillImages[i] == null) continue;

            // Принудительно устанавливаем настройки для правильного роста снизу вверх
            tankFillImages[i].type = Image.Type.Filled;
            tankFillImages[i].fillMethod = Image.FillMethod.Vertical;
            tankFillImages[i].fillOrigin = (int)Image.OriginVertical.Bottom;
        }

        for (int i = 0; i < tankLiquidScaleTargets.Length; i++)
        {
            if (tankLiquidScaleTargets[i] != null)
            {
                tankBaseScales[i] = tankLiquidScaleTargets[i].localScale;
                EnsureBottomGrowthPivot(tankLiquidScaleTargets[i]);
            }
        }
    }

    private static void EnsureBottomGrowthPivot(RectTransform target)
    {
        if (target == null) return;
        Vector2 pivot = target.pivot;
        if (Mathf.Approximately(pivot.y, 0f)) return;

        float height = target.rect.height;
        target.pivot = new Vector2(pivot.x, 0f);
        target.anchoredPosition += new Vector2(0f, -pivot.y * height);
    }

    private void ApplyScaleFallbackVisual(int index, float visualValue, bool animate)
    {
        if (!useScaleVisualFallback || tankLiquidScaleTargets == null || index < 0 || index >= tankLiquidScaleTargets.Length) return;

        RectTransform liquid = tankLiquidScaleTargets[index];
        if (liquid == null) return;

        Vector3 baseScale = tankBaseScales[index] == Vector3.zero ? liquid.localScale : tankBaseScales[index];
        float clampedValue = Mathf.Clamp01(visualValue);
        float safeY = Mathf.Max(0.001f, clampedValue);
        float targetScaleY = Mathf.Min(baseScale.y * safeY, baseScale.y);
        Vector3 targetScale = new Vector3(baseScale.x, targetScaleY, baseScale.z);

        if (!animate)
        {
            liquid.localScale = targetScale;
            return;
        }

        liquid.DOKill();
        liquid.DOScale(targetScale, fillTweenDuration).SetEase(Ease.OutCubic);
    }

    private void UpdateTankStateSprites(int index, float logicalValue)
    {
        if (index < 0 || index >= 3) return;

        bool showLow  = logicalValue <= stateMidThreshold;
        bool showMid  = logicalValue > stateMidThreshold && logicalValue < stateHighThreshold;
        bool showHigh = logicalValue >= stateHighThreshold;

        SetStateSpriteActive(tankStateLow,  index, showLow);
        SetStateSpriteActive(tankStateMid,  index, showMid);
        SetStateSpriteActive(tankStateHigh, index, showHigh);
    }

    private void ConfigureRuntimeButtonBindings()
    {
        if (!useRuntimeButtonBindings || buttonBindings == null) return;

        for (int i = 0; i < buttonBindings.Length; i++)
        {
            PumpButtonBinding binding = buttonBindings[i];
            if (binding == null || binding.button == null) continue;

            if (clearExistingButtonListeners) binding.button.onClick.RemoveAllListeners();

            switch (binding.action)
            {
                case PumpAction.FillAFromSource: binding.button.onClick.AddListener(FillAFromSource); break;
                case PumpAction.TransferAtoB:    binding.button.onClick.AddListener(TransferAtoB);    break;
                case PumpAction.TransferAtoC:    binding.button.onClick.AddListener(TransferAtoC);    break;
                case PumpAction.TransferBtoA:    binding.button.onClick.AddListener(TransferBtoA);    break;
                case PumpAction.TransferBtoC:    binding.button.onClick.AddListener(TransferBtoC);    break;
                case PumpAction.TransferCtoA:    binding.button.onClick.AddListener(TransferCtoA);    break;
                case PumpAction.TransferCtoB:    binding.button.onClick.AddListener(TransferCtoB);    break;
                case PumpAction.ResetPuzzle:     binding.button.onClick.AddListener(ResetPuzzle);     break;
            }
        }
    }

    private void ValidateButtonBindings() { }

    private void SetButtonsInteractable(bool value)
    {
        if (actionButtons == null) return;
        foreach (Button btn in actionButtons) if (btn != null) btn.interactable = value;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void PlayBubbles()
    {
        if (bubbleRects == null) return;

        foreach (RectTransform bubble in bubbleRects)
        {
            if (bubble == null) continue;

            Vector2 startPos = bubble.anchoredPosition;
            bubble.DOKill();

            DOTween.Sequence()
                .Append(bubble.DOAnchorPosY(startPos.y + bubbleRise, bubbleDuration).SetEase(Ease.OutSine))
                .Join(bubble.DOScale(Vector3.one * 1.1f, bubbleDuration * 0.5f))
                .Append(bubble.DOScale(Vector3.one, bubbleDuration * 0.5f))
                .Join(bubble.DOAnchorPos(startPos, bubbleDuration * 0.5f).SetEase(Ease.InSine));
        }
    }

    private static void SetStateSpriteActive(Image[] targets, int index, bool isActive)
    {
        if (targets == null || index < 0 || index >= targets.Length) return;
        if (targets[index] != null) targets[index].gameObject.SetActive(isActive);
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs) return;
        Debug.Log($"[PumpPuzzleController] {message}", this);
    }

    private static string TankName(int index) => index switch { 0 => "A", 1 => "B", 2 => "C", _ => $"{index}" };

    private static void EnsureArraySize(ref float[] array)
    {
        if (array != null && array.Length == 3) return;
        float[] newArray = new float[3];
        if (array != null) for (int i = 0; i < Mathf.Min(3, array.Length); i++) newArray[i] = array[i];
        array = newArray;
    }

    private void EnsureCurveArraySize()
    {
        if (tankVisualCurves == null || tankVisualCurves.Length != 3)
        {
            tankVisualCurves = new AnimationCurve[3];
            for (int i = 0; i < 3; i++) tankVisualCurves[i] = AnimationCurve.Linear(0, 0, 1, 1);
        }
    }
}