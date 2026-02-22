using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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

    [Header("Events")]
    [SerializeField] private UnityEvent onPuzzleSolved;

    [Header("Tank UI (A, B, C)")]
    [SerializeField] private Image[] tankFillImages = new Image[3];
    [SerializeField] private RectTransform[] tankShakeTargets = new RectTransform[3];
    [SerializeField] private RectTransform[] tankLiquidScaleTargets = new RectTransform[3];
    [SerializeField] private bool useScaleVisualFallback = true;

    [Header("Bubble FX (optional)")]
    [SerializeField] private RectTransform[] bubbleRects;
    [SerializeField] private float bubbleRise = 24f;
    [SerializeField] private float bubbleDuration = 0.45f;

    [Header("UI State")]
    [SerializeField] private Button[] actionButtons;
    [SerializeField] private GameObject resetOptionRoot;
    [SerializeField] private Text statusText;
    [SerializeField] private CanvasGroup fadeToBlackCanvasGroup;

    [Header("Runtime Button Binding (optional)")]
    [SerializeField] private bool useRuntimeButtonBindings = true;
    [SerializeField] private bool clearExistingButtonListeners = false;
    [SerializeField] private PumpButtonBinding[] buttonBindings;

    [Header("Puzzle Values")]
    [SerializeField] private float[] initialLevels = { 1f, 0f, 0f };
    [SerializeField] private float[] targetLevels = { 0.5f, 0.75f, 0f };
    [SerializeField] private float fillStep = 0.25f;
    [SerializeField] private bool stepwiseTransfer = true;
    [SerializeField] private float levelTolerance = 0.001f;

    [Header("Success Flow")]
    [SerializeField] private bool allowInfinitePlayAfterSuccess = false;
    [SerializeField] private bool autoResetOnSuccessInInfiniteMode = true;
    [SerializeField] private float autoResetDelay = 0.85f;

    [Header("Tween Settings")]
    [SerializeField] private float fillTweenDuration = 0.35f;
    [SerializeField] private float overflowShakeDuration = 0.35f;
    [SerializeField] private float overflowShakeStrength = 20f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private readonly float[] levels = new float[3];
    private readonly Vector3[] tankBaseScales = new Vector3[3];
    private bool isSolved;

    public bool IsSolved => isSolved;

    private void Awake()
    {
        EnsureArraySize(ref initialLevels);
        EnsureArraySize(ref targetLevels);
    }

    private void Start()
    {
        ConfigureRuntimeButtonBindings();
        ValidateButtonBindings();
        ValidateAndPrepareTankImages();
        ResetPuzzle();
    }

    public void ResetPuzzle()
    {
        ResetPuzzleInternal(false);
    }

    private void ResetPuzzleInternal(bool forceReset)
    {
        if (!forceReset && isSolved && !allowInfinitePlayAfterSuccess)
        {
            SetStatus("Питание запущено.");
            LogDebug("ResetPuzzle: игнорировано, пазл уже завершён и бесконечный режим выключен.");
            return;
        }

        LogDebug("ResetPuzzle вызван.");
        isSolved = false;

        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = Mathf.Clamp01(initialLevels[i]);
            SetTankVisual(i, levels[i], false);
        }

        if (resetOptionRoot != null)
        {
            resetOptionRoot.SetActive(false);
        }

        if (fadeToBlackCanvasGroup != null)
        {
            fadeToBlackCanvasGroup.alpha = 0f;
            fadeToBlackCanvasGroup.blocksRaycasts = false;
        }

        SetButtonsInteractable(true);
        SetStatus("Электрощит");
    }

    public void FillAFromSource()
    {
        LogDebug($"FillAFromSource: before A={levels[0]:0.###} B={levels[1]:0.###} C={levels[2]:0.###}");

        if (isSolved)
        {
            LogDebug("FillAFromSource: пазл уже решён, действие игнорировано.");
            return;
        }

        if (levels[0] + fillStep > 1f + levelTolerance)
        {
            LogDebug($"FillAFromSource: overflow в A (A={levels[0]:0.###}, step={fillStep:0.###}).");
            TriggerOverflow(0);
            return;
        }

        levels[0] = Mathf.Clamp01(levels[0] + fillStep);
        SetTankVisual(0, levels[0], true);
        PlayBubbles();
        EvaluateWinCondition();
        LogDebug($"FillAFromSource: after  A={levels[0]:0.###} B={levels[1]:0.###} C={levels[2]:0.###}");
    }

    public void TransferAtoB() => Transfer(0, 1);
    public void TransferAtoC() => Transfer(0, 2);
    public void TransferBtoA() => Transfer(1, 0);
    public void TransferBtoC() => Transfer(1, 2);
    public void TransferCtoA() => Transfer(2, 0);
    public void TransferCtoB() => Transfer(2, 1);

    private void Transfer(int from, int to)
    {
        LogDebug($"Transfer {TankName(from)}->{TankName(to)}: before A={levels[0]:0.###} B={levels[1]:0.###} C={levels[2]:0.###}");

        if (isSolved)
        {
            LogDebug($"Transfer {TankName(from)}->{TankName(to)}: пазл уже решён, действие игнорировано.");
            return;
        }

        if (levels[from] <= levelTolerance)
        {
            SetStatus("Источник пуст.");
            LogDebug($"Transfer {TankName(from)}->{TankName(to)}: источник пуст.");
            return;
        }

        float freeSpace = 1f - levels[to];
        if (freeSpace <= levelTolerance)
        {
            LogDebug($"Transfer {TankName(from)}->{TankName(to)}: target полный, overflow.");
            TriggerOverflow(to);
            return;
        }

        float transferLimit = stepwiseTransfer ? Mathf.Max(levelTolerance, fillStep) : 1f;
        float amount = Mathf.Min(levels[from], freeSpace, transferLimit);
        LogDebug($"Transfer {TankName(from)}->{TankName(to)}: amount={amount:0.###}, freeSpace={freeSpace:0.###}");
        levels[from] = Mathf.Clamp01(levels[from] - amount);
        levels[to] = Mathf.Clamp01(levels[to] + amount);

        SetTankVisual(from, levels[from], true);
        SetTankVisual(to, levels[to], true);
        PlayBubbles();

        EvaluateWinCondition();
        LogDebug($"Transfer {TankName(from)}->{TankName(to)}: after  A={levels[0]:0.###} B={levels[1]:0.###} C={levels[2]:0.###}");
    }

    private void EvaluateWinCondition()
    {
        bool matched = true;

        for (int i = 0; i < levels.Length; i++)
        {
            if (Mathf.Abs(levels[i] - targetLevels[i]) > levelTolerance)
            {
                matched = false;
                break;
            }
        }

        if (!matched)
        {
            SetStatus($"A={levels[0]:0.###}  B={levels[1]:0.###}  C={levels[2]:0.###}");
            LogDebug("EvaluateWinCondition: цель ещё не достигнута.");
            return;
        }

        OnSolved();
    }

    private void OnSolved()
    {
        LogDebug("OnSolved: пазл решён.");
        isSolved = true;
        if (!allowInfinitePlayAfterSuccess)
        {
            SetButtonsInteractable(false);
            SetStatus("Успех! Ток есть!.");
        }
        else
        {
            SetStatus("Успех! Бесконечный режим активен.");
        }

        onPuzzleSolved?.Invoke();

        if (tankFillImages[2] != null)
        {
            tankFillImages[2].transform.DOKill();
            tankFillImages[2].transform.DOScaleY(0.1f, 0.4f).SetEase(Ease.InBack);
        }

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

    private void TriggerOverflow(int tankIndex)
    {
        LogDebug($"TriggerOverflow: tank={TankName(tankIndex)}");
        SetStatus("Переполнение! По блоку, можете нажать на Сброс.");

        if (tankIndex >= 0 && tankIndex < tankShakeTargets.Length && tankShakeTargets[tankIndex] != null)
        {
            tankShakeTargets[tankIndex].DOKill();
            tankShakeTargets[tankIndex].DOShakeAnchorPos(
                overflowShakeDuration,
                overflowShakeStrength,
                25,
                90f,
                false,
                true
            );
        }

        if (resetOptionRoot != null)
        {
            resetOptionRoot.SetActive(true);
        }
    }

    private void SetTankVisual(int index, float value, bool animate)
    {
        if (index < 0 || index >= tankFillImages.Length || tankFillImages[index] == null)
        {
            LogDebug($"SetTankVisual: пропуск, tankFillImages[{index}] не назначен.");
        }

        if (index >= 0 && index < tankFillImages.Length && tankFillImages[index] != null)
        {
            Image target = tankFillImages[index];

            if (!animate)
            {
                target.fillAmount = value;
                LogDebug($"SetTankVisual: {TankName(index)} fillAmount={value:0.###} (без анимации)");
            }
            else
            {
                target.DOKill();
                target.DOFillAmount(value, fillTweenDuration).SetEase(Ease.OutCubic);
                LogDebug($"SetTankVisual: {TankName(index)} fillAmount={value:0.###} (tween {fillTweenDuration:0.###}s)");
            }
        }

        ApplyScaleFallbackVisual(index, value, animate);
    }

    private void ValidateAndPrepareTankImages()
    {
        if (tankFillImages == null || tankFillImages.Length != 3)
        {
            // Debug.LogWarning("[PumpPuzzleController] Tank Fill Images должен содержать ровно 3 элемента (A, B, C).", this);
            return;
        }

        for (int i = 0; i < tankFillImages.Length; i++)
        {
            Image tank = tankFillImages[i];
            if (tank == null)
            {
                // Debug.LogWarning($"[PumpPuzzleController] Tank Fill Images[{i}] не назначен.", this);
                continue;
            }

            tank.type = Image.Type.Filled;
            tank.fillMethod = Image.FillMethod.Vertical;
            tank.fillOrigin = (int)Image.OriginVertical.Bottom;
            LogDebug($"ValidateAndPrepareTankImages: {TankName(i)} готов (Filled Vertical Bottom).");

            if (i < tankLiquidScaleTargets.Length && tankLiquidScaleTargets[i] != null)
            {
                tankBaseScales[i] = tankLiquidScaleTargets[i].localScale;
                LogDebug($"ScaleFallback: база масштаба для {TankName(i)} = {tankBaseScales[i]}");
            }
        }
    }

    private void ApplyScaleFallbackVisual(int index, float value, bool animate)
    {
        if (!useScaleVisualFallback)
        {
            return;
        }

        if (tankLiquidScaleTargets == null || index < 0 || index >= tankLiquidScaleTargets.Length)
        {
            return;
        }

        RectTransform liquid = tankLiquidScaleTargets[index];
        if (liquid == null)
        {
            return;
        }

        Vector3 baseScale = tankBaseScales[index] == Vector3.zero ? liquid.localScale : tankBaseScales[index];
        float safeY = Mathf.Max(0.001f, value);
        Vector3 targetScale = new Vector3(baseScale.x, baseScale.y * safeY, baseScale.z);

        if (!animate)
        {
            liquid.localScale = targetScale;
            LogDebug($"ScaleFallback: {TankName(index)} scaleY={targetScale.y:0.###} (без анимации)");
            return;
        }

        liquid.DOKill();
        liquid.DOScale(targetScale, fillTweenDuration).SetEase(Ease.OutCubic);
        LogDebug($"ScaleFallback: {TankName(index)} scaleY={targetScale.y:0.###} (tween {fillTweenDuration:0.###}s)");
    }

    private void ConfigureRuntimeButtonBindings()
    {
        if (!useRuntimeButtonBindings || buttonBindings == null || buttonBindings.Length == 0)
        {
            LogDebug("Runtime button binding отключён или список пуст.");
            return;
        }

        for (int i = 0; i < buttonBindings.Length; i++)
        {
            PumpButtonBinding binding = buttonBindings[i];
            if (binding == null || binding.button == null)
            {
                // Debug.LogWarning($"[PumpPuzzleController] buttonBindings[{i}] не настроен.", this);
                continue;
            }

            if (clearExistingButtonListeners)
            {
                binding.button.onClick.RemoveAllListeners();
            }

            switch (binding.action)
            {
                case PumpAction.FillAFromSource:
                    binding.button.onClick.AddListener(FillAFromSource);
                    break;
                case PumpAction.TransferAtoB:
                    binding.button.onClick.AddListener(TransferAtoB);
                    break;
                case PumpAction.TransferAtoC:
                    binding.button.onClick.AddListener(TransferAtoC);
                    break;
                case PumpAction.TransferBtoA:
                    binding.button.onClick.AddListener(TransferBtoA);
                    break;
                case PumpAction.TransferBtoC:
                    binding.button.onClick.AddListener(TransferBtoC);
                    break;
                case PumpAction.TransferCtoA:
                    binding.button.onClick.AddListener(TransferCtoA);
                    break;
                case PumpAction.TransferCtoB:
                    binding.button.onClick.AddListener(TransferCtoB);
                    break;
                case PumpAction.ResetPuzzle:
                    binding.button.onClick.AddListener(ResetPuzzle);
                    break;
            }

            LogDebug($"Binding: {binding.button.name} -> {binding.action}");
        }
    }

    private void ValidateButtonBindings()
    {
        if (actionButtons == null || actionButtons.Length == 0)
        {
            // Debug.LogWarning("[PumpPuzzleController] Action Buttons не заполнен. Кнопки могут остаться неинтерактивными после победы.", this);
            return;
        }

        for (int i = 0; i < actionButtons.Length; i++)
        {
            Button button = actionButtons[i];
            if (button == null)
            {
                // Debug.LogWarning($"[PumpPuzzleController] Action Buttons[{i}] не назначен.", this);
                continue;
            }

            int persistentCount = button.onClick.GetPersistentEventCount();
            LogDebug($"Action button '{button.name}': persistent OnClick={persistentCount}");
        }
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        // Debug.Log($"[PumpPuzzleController] {message}", this);
    }

    private static string TankName(int index)
    {
        if (index == 0) return "A";
        if (index == 1) return "B";
        if (index == 2) return "C";
        return $"{index}";
    }

    private void PlayBubbles()
    {
        if (bubbleRects == null)
        {
            return;
        }

        for (int i = 0; i < bubbleRects.Length; i++)
        {
            RectTransform bubble = bubbleRects[i];
            if (bubble == null)
            {
                continue;
            }

            Vector2 startPos = bubble.anchoredPosition;
            bubble.DOKill();

            Sequence sequence = DOTween.Sequence();
            sequence.Append(bubble.DOAnchorPosY(startPos.y + bubbleRise, bubbleDuration).SetEase(Ease.OutSine));
            sequence.Join(bubble.DOScale(Vector3.one * 1.1f, bubbleDuration * 0.5f));
            sequence.Append(bubble.DOScale(Vector3.one, bubbleDuration * 0.5f));
            sequence.Join(bubble.DOAnchorPos(startPos, bubbleDuration * 0.5f).SetEase(Ease.InSine));
        }
    }

    private void SetButtonsInteractable(bool value)
    {
        if (actionButtons == null)
        {
            return;
        }

        for (int i = 0; i < actionButtons.Length; i++)
        {
            if (actionButtons[i] != null)
            {
                actionButtons[i].interactable = value;
            }
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private static void EnsureArraySize(ref float[] array)
    {
        if (array != null && array.Length == 3)
        {
            return;
        }

        float[] newArray = new float[3];
        if (array != null)
        {
            for (int i = 0; i < Mathf.Min(3, array.Length); i++)
            {
                newArray[i] = array[i];
            }
        }

        array = newArray;
    }
}
