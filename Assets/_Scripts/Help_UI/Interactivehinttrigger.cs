using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[AddComponentMenu("Game/UI/Interactive Hint Trigger")]
[RequireComponent(typeof(Collider))]
public sealed class InteractiveHintTrigger : MonoBehaviour
{
    #region Inspector

    [Header("Core")]
    [SerializeField] private Image hintImage;
    [SerializeField] private Image[] additionalHintImages;
    [SerializeField] private string playerTag = "Player";

    [Header("Multi Image")]
    [SerializeField] private bool playMultiPressSequence = true;
    [SerializeField, Min(0f)] private float multiStepDelay = 0.15f;
    [SerializeField, Range(0.5f, 0.99f)] private float multiPressScale = 0.88f;
    [SerializeField, Range(0.05f, 0.5f)] private float multiPressTime = 0.12f;
    [SerializeField, Range(0.05f, 0.8f)] private float multiReleaseTime = 0.18f;
    [SerializeField] private Ease multiPressEase = Ease.InSine;
    [SerializeField] private Ease multiReleaseEase = Ease.OutQuad;

    [Header("Fade")]
    [SerializeField, Range(0f, 2f)] private float fadeDuration = 0.3f;

    [Header("DOTween Pulse")]
    [SerializeField] private bool usePulse = true;
    [SerializeField, Range(0.5f, 0.99f)] private float pulseScale  = 0.88f;
    [SerializeField, Range(0.05f, 0.5f)] private float pressTime   = 0.15f;
    [SerializeField, Range(0.05f, 0.8f)] private float releaseTime = 0.25f;
    [SerializeField, Range(0f, 3f)]      private float pauseBetween = 0.7f;
    [SerializeField] private Ease pressEase   = Ease.InSine;
    [SerializeField] private Ease releaseEase = Ease.OutElastic;

    #endregion

    #region Private

    [Serializable]
    private sealed class HintVisual
    {
        public Image image;
        public CanvasGroup canvasGroup;
        public RectTransform rectTransform;
        public Vector3 originScale;
        public Sequence pulseSeq;
    }

    private readonly List<HintVisual> _visuals = new List<HintVisual>();
    private bool _playerInside;
    private bool _isDestroyed;
    private bool _ready;          // Awake прошёл без ошибок
    private int _showVersion;

    private Tween    _fadeTween;
    private Sequence _multiPressSeq;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!ValidateAndCacheImages()) return;

        // Триггер
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Сразу прячем
        HideAllVisuals();

        _ready = true;
    }

    private void OnDestroy()
    {
        _isDestroyed = true;
        KillTweens(resetScale: false);
    }

    private void OnDisable()
    {
        if (_isDestroyed || !_ready) return;
        KillTweens(resetScale: true);
        HideAllVisuals();
        _playerInside = false;
    }

    #endregion

    #region Trigger

    private void OnTriggerEnter(Collider other)
    {
        if (!_ready || _playerInside || !other.CompareTag(playerTag)) return;
        _playerInside = true;
        Show();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_ready || !_playerInside || !other.CompareTag(playerTag)) return;
        _playerInside = false;
        Hide();
    }

    #endregion

    #region Show / Hide

    private void Show()
    {
        KillTweens(resetScale: true);
        KillMultiPressSequence(resetScale: true);

        _showVersion++;
        int version = _showVersion;

        if (_visuals.Count == 0) return;

        PrepareVisualsForShow();

        _fadeTween = BuildParallelShowTween(version);
    }

    private void Hide(bool instant = false)
    {
        KillTweens(resetScale: true);
        KillMultiPressSequence(resetScale: true);

        SetVisualInteraction(false);

        if (instant || fadeDuration <= 0f)
        {
            HideAllVisuals();
            return;
        }

        _fadeTween = BuildHideTween()
            .SetUpdate(true)
            .OnComplete(HideAllVisuals);
    }

    private void HideAllVisuals()
    {
        for (int i = 0; i < _visuals.Count; i++)
        {
            HintVisual visual = _visuals[i];
            if (visual == null || visual.image == null) continue;

            if (visual.pulseSeq != null)
            {
                visual.pulseSeq.Kill();
                visual.pulseSeq = null;
            }

            if (!_isDestroyed && visual.rectTransform != null)
            {
                visual.rectTransform.localScale = visual.originScale;
            }

            if (visual.canvasGroup != null)
            {
                visual.canvasGroup.alpha = 0f;
                visual.canvasGroup.interactable = false;
                visual.canvasGroup.blocksRaycasts = false;
            }

            if (!_isDestroyed)
            {
                visual.image.gameObject.SetActive(false);
            }
        }

        KillMultiPressSequence(resetScale: true);
    }

    private void SetVisualInteraction(bool enabled)
    {
        for (int i = 0; i < _visuals.Count; i++)
        {
            HintVisual visual = _visuals[i];
            if (visual?.canvasGroup == null) continue;

            visual.canvasGroup.interactable = enabled;
            visual.canvasGroup.blocksRaycasts = enabled;
        }
    }

    #endregion

    #region Pulse

    private void StartPulseAll()
    {
        if (playMultiPressSequence && _visuals.Count > 1)
        {
            StartMultiPressSequence();
            return;
        }

        for (int i = 0; i < _visuals.Count; i++)
        {
            StartPulse(_visuals[i]);
        }
    }

    private void StartMultiPressSequence()
    {
        KillMultiPressSequence(resetScale: false);

        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < _visuals.Count; i++)
        {
            HintVisual visual = _visuals[i];
            if (visual == null || visual.rectTransform == null) continue;

            Vector3 pressedScale = visual.originScale * multiPressScale;

            sequence.Append(visual.rectTransform.DOScale(pressedScale, multiPressTime).SetEase(multiPressEase));
            sequence.Append(visual.rectTransform.DOScale(visual.originScale, multiReleaseTime).SetEase(multiReleaseEase));

            if (multiStepDelay > 0f && i < _visuals.Count - 1)
            {
                sequence.AppendInterval(multiStepDelay);
            }
        }

        sequence.SetLoops(-1, LoopType.Restart);
        sequence.SetLink(gameObject, LinkBehaviour.KillOnDisable);
        _multiPressSeq = sequence;
    }

    private void StartPulse(HintVisual visual)
    {
        if (!usePulse || _isDestroyed || visual == null || visual.rectTransform == null) return;

        if (visual.pulseSeq != null)
        {
            visual.pulseSeq.Kill();
            visual.pulseSeq = null;
        }

        Vector3 pressed = visual.originScale * pulseScale;

        visual.pulseSeq = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Restart);
        visual.pulseSeq.Append(visual.rectTransform.DOScale(pressed, pressTime).SetEase(pressEase));
        visual.pulseSeq.Append(visual.rectTransform.DOScale(visual.originScale, releaseTime).SetEase(releaseEase));
        if (pauseBetween > 0f)
            visual.pulseSeq.AppendInterval(pauseBetween);
    }

    #endregion

    #region Tween Management

    private void KillTweens(bool resetScale)
    {
        _fadeTween?.Kill();
        _fadeTween = null;

        for (int i = 0; i < _visuals.Count; i++)
        {
            HintVisual visual = _visuals[i];
            if (visual?.pulseSeq != null)
            {
                visual.pulseSeq.Kill();
                visual.pulseSeq = null;
            }

            if (resetScale && !_isDestroyed && visual?.rectTransform != null)
            {
                visual.rectTransform.localScale = visual.originScale;
            }
        }

        if (resetScale)
        {
            KillMultiPressSequence(resetScale: true);
        }
    }

    #endregion

    #region Validation

    private bool ValidateAndCacheImages()
    {
        _visuals.Clear();

        AddVisual(hintImage);

        if (additionalHintImages != null)
        {
            for (int i = 0; i < additionalHintImages.Length; i++)
            {
                AddVisual(additionalHintImages[i]);
            }
        }

        if (_visuals.Count > 0)
        {
            return true;
        }

        Debug.LogError($"[InteractiveHintTrigger] '{name}': не назначен ни один Image для подсказки! Скрипт отключён.", this);
        enabled = false;
        return false;
    }

    private void AddVisual(Image image)
    {
        if (image == null) return;

        for (int i = 0; i < _visuals.Count; i++)
        {
            if (_visuals[i] != null && _visuals[i].image == image)
            {
                return;
            }
        }

        HintVisual visual = new HintVisual
        {
            image = image,
            rectTransform = image.rectTransform,
            originScale = image.rectTransform.localScale,
            canvasGroup = image.gameObject.GetComponent<CanvasGroup>()
        };

        if (visual.canvasGroup == null)
        {
            visual.canvasGroup = image.gameObject.AddComponent<CanvasGroup>();
        }

        _visuals.Add(visual);
    }

    private void PrepareVisualsForShow()
    {
        for (int i = 0; i < _visuals.Count; i++)
        {
            HintVisual visual = _visuals[i];
            if (visual == null || visual.image == null || visual.canvasGroup == null) continue;

            visual.image.gameObject.SetActive(true);
            visual.canvasGroup.alpha = 0f;
            visual.canvasGroup.interactable = true;
            visual.canvasGroup.blocksRaycasts = true;

            if (visual.rectTransform != null)
            {
                visual.rectTransform.localScale = visual.originScale;
            }
        }
    }

    private Sequence BuildParallelShowTween(int version)
    {
        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < _visuals.Count; i++)
        {
            HintVisual visual = _visuals[i];
            if (visual?.canvasGroup == null) continue;

            sequence.Join(DOTween
                .To(() => visual.canvasGroup.alpha, x => visual.canvasGroup.alpha = x, 1f, fadeDuration)
                .SetEase(Ease.OutQuad));
        }

        sequence.OnComplete(() =>
        {
            if (_isDestroyed || !_ready || !_playerInside || version != _showVersion)
            {
                return;
            }

            StartPulseAll();
        });
        return sequence;
    }

    private void KillMultiPressSequence(bool resetScale)
    {
        if (_multiPressSeq != null)
        {
            _multiPressSeq.Kill();
            _multiPressSeq = null;
        }

        if (!resetScale || _isDestroyed)
        {
            return;
        }

        for (int i = 0; i < _visuals.Count; i++)
        {
            HintVisual visual = _visuals[i];
            if (visual?.rectTransform == null) continue;

            visual.rectTransform.localScale = visual.originScale;
        }
    }

    private Sequence BuildHideTween()
    {
        Sequence sequence = DOTween.Sequence();

        for (int i = 0; i < _visuals.Count; i++)
        {
            HintVisual visual = _visuals[i];
            if (visual?.canvasGroup == null) continue;

            sequence.Join(DOTween
                .To(() => visual.canvasGroup.alpha, x => visual.canvasGroup.alpha = x, 0f, fadeDuration)
                .SetEase(Ease.InQuad));
        }

        return sequence;
    }

    #endregion

    #region Public API

    public void ForceShow()                  => Show();
    public void ForceHide(bool instant=false) => Hide(instant);
    public void SetSprite(Sprite s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));

        Image target = hintImage != null ? hintImage : GetFirstAvailableImage();
        if (target == null) throw new InvalidOperationException("No hint Image is assigned.");

        target.sprite = s;
    }

    private Image GetFirstAvailableImage()
    {
        for (int i = 0; i < _visuals.Count; i++)
        {
            if (_visuals[i]?.image != null)
            {
                return _visuals[i].image;
            }
        }

        return null;
    }

    #endregion
}