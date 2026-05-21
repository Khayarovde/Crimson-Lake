using System;
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
    [SerializeField] private string playerTag = "Player";

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

    private CanvasGroup  _cg;
    private RectTransform _rt;
    private Vector3      _originScale;
    private bool         _playerInside;
    private bool         _isDestroyed;
    private bool         _ready;          // Awake прошёл без ошибок

    private Tween    _fadeTween;
    private Sequence _pulseSeq;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!ValidateImage()) return;

        _rt          = hintImage.rectTransform;
        _originScale = _rt.localScale;

        // CanvasGroup — ищем на gameObject Image-компонента, добавляем если нет
        _cg = hintImage.gameObject.GetComponent<CanvasGroup>();
        if (_cg == null)
            _cg = hintImage.gameObject.AddComponent<CanvasGroup>();

        // Триггер
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Сразу прячем
        _cg.alpha          = 0f;
        _cg.interactable   = false;
        _cg.blocksRaycasts = false;
        hintImage.gameObject.SetActive(false);

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
        SafeHideCG();
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
        // Гарантируем что объект активен ПЕРЕД любыми твинами
        hintImage.gameObject.SetActive(true);

        // Сбрасываем состояние CanvasGroup вручную (без GetComponent — _cg уже кэширован)
        _cg.alpha          = 0f;
        _cg.interactable   = true;
        _cg.blocksRaycasts = true;

        KillTweens(resetScale: true);

        _fadeTween = DOTween
            .To(() => _cg.alpha, x => _cg.alpha = x, 1f, fadeDuration)
            .SetUpdate(true)
            .SetEase(Ease.OutQuad)
            .OnComplete(StartPulse);
    }

    private void Hide(bool instant = false)
    {
        KillTweens(resetScale: true);

        _cg.interactable   = false;
        _cg.blocksRaycasts = false;

        if (instant || fadeDuration <= 0f)
        {
            _cg.alpha = 0f;
            SafeDeactivate();
            return;
        }

        _fadeTween = DOTween
            .To(() => _cg.alpha, x => _cg.alpha = x, 0f, fadeDuration)
            .SetUpdate(true)
            .SetEase(Ease.InQuad)
            .OnComplete(SafeDeactivate);
    }

    private void SafeDeactivate()
    {
        if (_isDestroyed || hintImage == null) return;
        hintImage.gameObject.SetActive(false);
    }

    private void SafeHideCG()
    {
        if (_cg == null) return;
        _cg.alpha          = 0f;
        _cg.interactable   = false;
        _cg.blocksRaycasts = false;
        SafeDeactivate();
    }

    #endregion

    #region Pulse

    private void StartPulse()
    {
        if (!usePulse || _isDestroyed || _rt == null) return;

        KillPulse();

        var pressed = _originScale * pulseScale;

        _pulseSeq = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Restart);
        _pulseSeq.Append(_rt.DOScale(pressed,      pressTime  ).SetEase(pressEase));
        _pulseSeq.Append(_rt.DOScale(_originScale, releaseTime).SetEase(releaseEase));
        if (pauseBetween > 0f)
            _pulseSeq.AppendInterval(pauseBetween);
    }

    private void KillPulse()
    {
        _pulseSeq?.Kill();
        _pulseSeq = null;
    }

    #endregion

    #region Tween Management

    private void KillTweens(bool resetScale)
    {
        _fadeTween?.Kill(); _fadeTween = null;
        KillPulse();
        if (resetScale && !_isDestroyed && _rt != null)
            _rt.localScale = _originScale;
    }

    #endregion

    #region Validation

    private bool ValidateImage()
    {
        if (hintImage != null) return true;
        Debug.LogError($"[InteractiveHintTrigger] '{name}': поле Hint Image не назначено в Inspector! Скрипт отключён.", this);
        enabled = false;
        return false;
    }

    #endregion

    #region Public API

    public void ForceShow()                  => Show();
    public void ForceHide(bool instant=false) => Hide(instant);
    public void SetSprite(Sprite s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));
        hintImage.sprite = s;
    }

    #endregion
}