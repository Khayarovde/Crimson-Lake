using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public interface IInputLockable
{
    void SetInputLocked(bool locked);
}

/// <summary>
/// Дверь в стиле Signalis:
///   1. Игрок входит в триггер → показывается подсказка [E] с иконкой состояния двери
///   2. Нажимает E → стоит на месте, поворачивается к двери, анимация взаимодействия
///   3. Дверь открывается
///   4. Персонаж проходит через дверной проём (только по X/Z, без взлёта по Y)
///   5. Fade to black → телепорт → fade from black
///   6. Персонаж выходит из двери на другой стороне
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DoorTeleportTrigger : MonoBehaviour
{
    public Transform TeleportTarget => teleportTarget;

    // Глобальная блокировка, чтобы одновременно работала только одна дверь.
    private static bool _anyDoorInteracting;
    private static DoorTeleportTrigger _activeDoor;
    private static Image _sharedFadeImage;

    // ─── Состояние двери ────────────────────────────────────────────────────

    public enum DoorState { Open, Locked, LockedNeedKey }

    [Header("Состояние двери")]
    [SerializeField] private DoorState doorState = DoorState.Open;
    [SerializeField] private bool hasKey = false;
    [SerializeField] private bool consumeKeyOnUse = true;
    
    [Tooltip("Предмет из инвентаря, проверяемый при открытии двери (если 'LockedNeedKey').")]
    [SerializeField] private InventoryItem requiredKeyItem;

    // ─── Телепорт ────────────────────────────────────────────────────────────

    [Header("Телепорт")]
    [Tooltip("Точка, куда телепортируется игрок (на другой стороне двери)")]
    [SerializeField] private Transform teleportTarget;
    [Tooltip("Максимальная дистанция до двери для старта взаимодействия")]
    [SerializeField] private float maxInteractDistance = 1.5f;

    // ─── Визуал двери ───────────────────────────────────────────────────────

    [Header("Визуал двери")]
    [SerializeField] private Transform doorVisual;
    [Tooltip("Смещение открытия строго по мировой оси Y (вверх/вниз)")]
    [SerializeField] private float doorOpenOffset = 2f;
    [SerializeField] private float doorOpenDuration = 0.55f;
    [SerializeField] private Ease doorOpenEase = Ease.OutSine;
    [SerializeField] private Collider[] doorBlockColliders;

    [Header("Автозакрытие двери")]
    [SerializeField] private bool autoCloseDoor = true;
    [SerializeField] private float doorCloseDelay = 1.2f;
    [SerializeField] private float doorCloseDuration = 0.45f;
    [SerializeField] private Ease doorCloseEase = Ease.InSine;

    // ─── Звук ───────────────────────────────────────────────────────────────

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorLockedClip;

    // ─── Движение через дверь ────────────────────────────────────────────────

    [Header("Проход через дверь (перед телепортом)")]
    [Tooltip("Точка за дверным проёмом — движение только по X/Z, Y персонажа не меняется")]
    [SerializeField] private Transform enterPoint;
    [SerializeField] private float enterDuration = 0.35f;
    [SerializeField] private Ease enterEase = Ease.Linear;

    [Header("Выход из двери (после телепорта)")]
    [Tooltip("Точка выхода на другой стороне — движение только по X/Z")]
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float exitDuration = 0.35f;
    [SerializeField] private Ease exitEase = Ease.Linear;

    // ─── Анимации игрока ─────────────────────────────────────────────────────

    [Header("Анимации игрока")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string interactAnimState = "Interact_Door";
    [SerializeField] private float interactAnimDuration = 0.75f;
    [SerializeField] private string walkAnimState = "Blend Tree_WALK";
    [SerializeField] private string idleAnimState = "Idle";
    [SerializeField] private float animCrossFade = 0.1f;

    // ─── Fade (затемнение экрана) ─────────────────────────────────────────────

    [Header("Затемнение экрана")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private float fadeToDarkDuration = 0.5f;
    [SerializeField] private float fadeFromDarkDuration = 0.5f;
    [SerializeField] private float holdDarkDuration = 0.2f;

    // ─── Hint UI (подсказка с иконкой состояния) ──────────────────────────────

    [Header("Подсказка UI")]
    [SerializeField] private GameObject hintRoot;
    [Tooltip("Image, в котором меняется спрайт в зависимости от состояния двери")]
    [SerializeField] private Image hintStateImage;
    [SerializeField] private Sprite spriteOpen;
    [SerializeField] private Sprite spriteLocked;
    [SerializeField] private Sprite spriteNeedKey;

    [Header("Анимация появления подсказки")]
    [Tooltip("Длительность растягивания по Y от 0 до исходного размера")]
    [SerializeField] private float hintRevealDuration = 0.2f;
    [SerializeField] private Ease hintRevealEase = Ease.OutBack;

    // ─── Управление ──────────────────────────────────────────────────────────

    [Header("Управление")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [Tooltip("Компонент контроллера игрока с методом SetInputLocked(bool)")]
    [SerializeField] private MonoBehaviour playerController;

    // ─── Приватные поля ───────────────────────────────────────────────────────

    private Transform _player;
    private Rigidbody _playerRb;
    private bool _playerInRange;
    private bool _isInteracting;
    private Vector3 _doorClosedWorldPos;
    private Tween _hintTween;
    private int _playerOverlapCount;
    private bool _hintVisible;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (doorVisual != null)
            _doorClosedWorldPos = doorVisual.position;

        SetDoorBlockers(true);
        HideHintImmediate(); // без анимации при старте сцены

        if (fadeImage != null)
        {
            fadeImage.color = Color.clear;
            fadeImage.gameObject.SetActive(false);

            if (_sharedFadeImage == null)
                _sharedFadeImage = fadeImage;
        }
    }

    private void Update()
    {
        if (!_playerInRange || _isInteracting || _anyDoorInteracting)
            return;

        if (_player == null)
            return;

        if (Input.GetKeyDown(interactKey) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))
            StartCoroutine(InteractionSequence());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other) || _isInteracting)
            return;

        Transform candidatePlayer = GetPlayerRoot(other);
        if (candidatePlayer == null)   // ← убрали IsWithinInteractDistance отсюда
            return;

        _playerOverlapCount++;
        _player = candidatePlayer;
        _playerRb = _player.GetComponentInParent<Rigidbody>();
        _playerInRange = true;

        if (playerAnimator == null && _player != null)
            playerAnimator = _player.GetComponentInChildren<Animator>();

        if (playerController == null && _player != null)
            playerController = _player.GetComponentInChildren<MonoBehaviour>();

        if (!_hintVisible)
            ShowHint();
        else
            UpdateHintSprite();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other) || _isInteracting)
            return;

        _playerOverlapCount = Mathf.Max(0, _playerOverlapCount - 1);

        if (_playerOverlapCount > 0)
            return;

        _playerInRange = false;
        HideHint();
    }

    private void OnDisable()
    {
        DOTween.Kill(gameObject);

        if (_activeDoor == this)
        {
            _activeDoor = null;
            _anyDoorInteracting = false;
        }

        _playerInRange = false;
        _isInteracting = false;
        _playerOverlapCount = 0;
        _hintVisible = false;
        HideHintImmediate();
        ResetFade();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Главная последовательность взаимодействия
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator InteractionSequence()
    {
        if (teleportTarget == null || _player == null)
            yield break;

        if (_anyDoorInteracting && _activeDoor != this)
            yield break;

        _isInteracting = true;
        _anyDoorInteracting = true;
        _activeDoor = this;
        HideHint();
        LockPlayerInput(true);

        // 1. Повернуться к двери (стоя на месте)
        yield return FacePlayerToDoor();

        // 2. Проверка доступа
        bool canOpen = false;
        yield return ValidateAccess(result => canOpen = result);

        if (!canOpen)
        {
            // Дверь заблокирована — просто разблокируем игрока
            LockPlayerInput(false);
            _isInteracting = false;
            yield break;
        }

        // 3. Анимация взаимодействия с дверью
        yield return PlayAnimation(interactAnimState, interactAnimDuration);

        // 4. Дверь открывается
        yield return OpenDoor();

        // 5. Пройти через дверной проём (только X/Z)
        yield return PlayAnimation(walkAnimState, 0f);
        yield return MovePlayerFlatTo(enterPoint, enterDuration, enterEase);

        // 6. Fade to black → телепорт → fade from black
        yield return FadeTo(1f, fadeToDarkDuration);
        yield return new WaitForSeconds(holdDarkDuration);
        Teleport();

        // После телепорта принудительно синхронизируем физику и обнуляем локальный триггер-контекст.
        if (_playerRb != null)
            _playerRb.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        _playerOverlapCount = 0;
        _playerInRange = false;

        yield return new WaitForSeconds(holdDarkDuration);

        // 7. Выйти из двери на другой стороне (только X/Z), без повторной walk-анимации
        yield return PlayAnimation(idleAnimState, 0f);
        yield return MovePlayerFlatTo(exitPoint, exitDuration, exitEase);

        yield return FadeTo(0f, fadeFromDarkDuration);

        // 8. Сразу вернуть управление игроку, чтобы его анимации от WASD включались без задержки
        LockPlayerInput(false);

        // 9. Автозакрытие двери запускаем в фоне, без блокировки игрока
        StartCoroutine(CloseDoorIfNeeded());

        _isInteracting = false;
        _player = null;
        _playerRb = null;
        _activeDoor = null;
        _anyDoorInteracting = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Проверка доступа к двери
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator ValidateAccess(System.Action<bool> result)
    {
        switch (doorState)
        {
            case DoorState.Open:
                result(true);
                yield break;

            case DoorState.Locked:
                // Полностью заперта — нельзя открыть
                if (audioSource != null && doorLockedClip != null)
                    audioSource.PlayOneShot(doorLockedClip);
                result(false);
                yield break;

            case DoorState.LockedNeedKey:
                bool haveRequiredKey = false;
                
                // Проверяем, назначен ли конкретный ключ, и ищем ли его в инвентаре.
                if (requiredKeyItem != null && _player != null)
                {
                    PlayerInventory pInv = _player.GetComponent<PlayerInventory>();
                    if (pInv != null && pInv.inventoryData != null)
                    {
                        foreach (var itm in pInv.inventoryData.items)
                        {
                            if (itm == requiredKeyItem)
                            {
                                haveRequiredKey = true;
                                if (consumeKeyOnUse)
                                {
                                    pInv.inventoryData.RemoveItem(itm);
                                }
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // Фолбэк на старую логику с bool hasKey
                    haveRequiredKey = hasKey;
                    if (haveRequiredKey && consumeKeyOnUse)
                    {
                        hasKey = false;
                    }
                }

                if (!haveRequiredKey)
                {
                    if (audioSource != null && doorLockedClip != null)
                        audioSource.PlayOneShot(doorLockedClip);
                    result(false);
                    yield break;
                }

                doorState = DoorState.Open;
                result(true);
                yield break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Телепорт
    // ─────────────────────────────────────────────────────────────────────────

    private void Teleport()
    {
        if (_player == null || teleportTarget == null)
            return;

        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector3.zero;
            _playerRb.angularVelocity = Vector3.zero;
            _playerRb.position = teleportTarget.position;
            _playerRb.rotation = teleportTarget.rotation;
        }
        else
        {
            _player.SetPositionAndRotation(teleportTarget.position, teleportTarget.rotation);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Движение игрока — ТОЛЬКО по X/Z, Y не меняется (нет взлёта/провала)
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator MovePlayerFlatTo(Transform target, float duration, Ease ease)
    {
        if (target == null || _player == null)
            yield break;

        // Целевая позиция: X/Z из точки, Y остаётся от текущего положения персонажа
        Vector3 flatTarget = new Vector3(
            target.position.x,
            _player.position.y,   // <-- Y персонажа не меняем!
            target.position.z
        );

        bool done = false;
        float safeDuration = Mathf.Max(0.05f, duration);

        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector3.zero;
            _playerRb.DOMove(flatTarget, safeDuration)
                .SetEase(ease)
                .OnComplete(() => done = true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
        else
        {
            _player.DOMove(flatTarget, safeDuration)
                .SetEase(ease)
                .OnComplete(() => done = true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        yield return new WaitUntil(() => done);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Поворот к двери (стоя на месте, Y игнорируется)
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator FacePlayerToDoor()
    {
        if (_player == null)
            yield break;

        Transform lookAt = doorVisual != null ? doorVisual : transform;
        Vector3 dir = lookAt.position - _player.position;
        dir.y = 0f; // только горизонтальный поворот

        if (dir.sqrMagnitude < 0.001f)
            yield break;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);

        bool done = false;
        _player.DORotateQuaternion(targetRot, 0.15f)
            .SetEase(Ease.OutSine)
            .OnComplete(() => done = true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        yield return new WaitUntil(() => done);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Открытие двери
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator OpenDoor()
    {
        if (audioSource != null && doorOpenClip != null)
            audioSource.PlayOneShot(doorOpenClip);

        SetDoorBlockers(false);

        if (doorVisual == null)
            yield break;

        bool done = false;
        Vector3 openPos = _doorClosedWorldPos + Vector3.up * doorOpenOffset;

        doorVisual.DOMove(openPos, doorOpenDuration)
            .SetEase(doorOpenEase)
            .OnComplete(() => done = true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        yield return new WaitUntil(() => done);
    }

    private IEnumerator CloseDoorIfNeeded()
    {
        if (!autoCloseDoor)
            yield break;

        if (doorCloseDelay > 0f)
            yield return new WaitForSeconds(doorCloseDelay);

        yield return CloseDoor();
    }

    private IEnumerator CloseDoor()
    {
        if (doorVisual == null)
        {
            SetDoorBlockers(true);
            yield break;
        }

        bool done = false;
        doorVisual.DOMove(_doorClosedWorldPos, Mathf.Max(0.05f, doorCloseDuration))
            .SetEase(doorCloseEase)
            .OnComplete(() => done = true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        yield return new WaitUntil(() => done);
        SetDoorBlockers(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Анимации игрока
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator PlayAnimation(string stateName, float waitDuration)
    {
        if (playerAnimator == null || string.IsNullOrEmpty(stateName))
            yield break;

        int hash = Animator.StringToHash(stateName);
        bool hasState = playerAnimator.HasState(0, hash);
        if (hasState)
            playerAnimator.CrossFadeInFixedTime(stateName, animCrossFade, 0);

        if (waitDuration > 0f)
        {
            float timeout = Mathf.Max(0.1f, waitDuration + 0.5f);
            if (!hasState)
            {
                yield return new WaitForSeconds(timeout);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < timeout)
            {
                AnimatorStateInfo state = playerAnimator.GetCurrentAnimatorStateInfo(0);
                if (!playerAnimator.IsInTransition(0) && state.shortNameHash == hash && state.normalizedTime >= 1f)
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fade
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        Image img = fadeImage != null ? fadeImage : _sharedFadeImage;
        if (img == null)
            yield break;

        if (_sharedFadeImage == null)
            _sharedFadeImage = img;

        fadeImage = _sharedFadeImage;
        img = fadeImage;

        img.gameObject.SetActive(true);
        Color to = fadeColor;
        to.a = targetAlpha;

        bool done = false;
        DOTween.To(() => img.color, c => img.color = c, to, Mathf.Max(0.05f, duration))
            .SetEase(Ease.Linear)
            .OnComplete(() => done = true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        yield return new WaitUntil(() => done);

        if (targetAlpha <= 0f)
            img.gameObject.SetActive(false);
    }

    private void ResetFade()
    {
        if (fadeImage == null)
            return;
        fadeImage.color = Color.clear;
        fadeImage.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hint UI
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Показывает подсказку и меняет спрайт под текущее состояние двери.
    /// </summary>
    private void ShowHint()
    {
        if (_hintVisible)
            return;

        UpdateHintSprite();
        SetHintVisible(true);
        PlayHintReveal();
        _hintVisible = true;
    }

    private void UpdateHintSprite()
    {
        if (hintStateImage != null)
        {
            if (doorState == DoorState.LockedNeedKey)
            {
                bool haveRequiredKey = hasKey;

                if (requiredKeyItem != null && _player != null)
                {
                    PlayerInventory pInv = _player.GetComponent<PlayerInventory>();
                    if (pInv != null && pInv.inventoryData != null)
                    {
                        foreach (var itm in pInv.inventoryData.items)
                        {
                            if (itm == requiredKeyItem)
                            {
                                haveRequiredKey = true;
                                break;
                            }
                        }
                    }
                }

                hintStateImage.sprite = haveRequiredKey ? spriteOpen : spriteNeedKey;
            }
            else
            {
                hintStateImage.sprite = doorState switch
                {
                    DoorState.Open => spriteOpen,
                    DoorState.Locked => spriteLocked,
                    _ => null
                };
            }
        }
    }

    private void SetHintVisible(bool visible)
    {
        if (hintRoot == null) return;

        if (!visible)
        {
            hintRoot.SetActive(false);
            return;
        }

        hintRoot.SetActive(true);
    }

    private void HideHint()
    {
        if (!_hintVisible)
        {
            SetHintVisible(false);
            return;
        }

        _hintVisible = false;
        PlayHintHide();
    }

    /// <summary>
    /// Мгновенное скрытие без анимации — используется в Awake и OnDisable.
    /// </summary>
    private void HideHintImmediate()
    {
        _hintTween?.Kill();
        _hintVisible = false;

        if (hintStateImage != null)
        {
            Vector3 s = hintStateImage.rectTransform.localScale;
            s.y = 1f; // сбросить на случай если tween оборвался на середине
            hintStateImage.rectTransform.localScale = s;
        }

        if (hintRoot != null)
            hintRoot.SetActive(false);
    }

    /// <summary>
    /// Появление: scale.y 0 → 1 (раскрывается по высоте).
    /// </summary>
    private void PlayHintReveal()
    {
        if (hintStateImage == null) return;

        RectTransform rt = hintStateImage.rectTransform;
        _hintTween?.Kill();

        Vector3 s = rt.localScale;
        s.y = 0f;
        rt.localScale = s;

        _hintTween = DOTween
            .To(() => rt.localScale.y, v =>
            {
                Vector3 cur = rt.localScale;
                cur.y = v;
                rt.localScale = cur;
            }, 1f, Mathf.Max(0.05f, hintRevealDuration))
            .SetEase(hintRevealEase)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    /// <summary>
    /// Пропадание: scale.y 1 → 0 (схлопывается по высоте), затем скрывает объект.
    /// </summary>
    private void PlayHintHide()
    {
        if (hintStateImage == null)
        {
            hintRoot.SetActive(false);
            return;
        }

        RectTransform rt = hintStateImage.rectTransform;
        _hintTween?.Kill();

        // Убедиться что начинаем с текущего scale.y (мог не дойти до 1)
        _hintTween = DOTween
            .To(() => rt.localScale.y, v =>
            {
                Vector3 cur = rt.localScale;
                cur.y = v;
                rt.localScale = cur;
            }, 0f, Mathf.Max(0.05f, hintRevealDuration))
            .SetEase(hintRevealEase)
            .OnComplete(() =>
            {
                if (hintRoot != null)
                    hintRoot.SetActive(false);

                // Сбросить scale.y в 1 чтобы следующий Reveal стартовал корректно
                Vector3 cur = rt.localScale;
                cur.y = 1f;
                rt.localScale = cur;
            })
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Вспомогательные методы
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(playerTag)
            || (other.transform.root != null && other.transform.root.CompareTag(playerTag));
    }

    private Transform GetPlayerRoot(Collider other)
    {
        if (other.attachedRigidbody != null)
            return other.attachedRigidbody.transform;

        Rigidbody rb = other.GetComponentInParent<Rigidbody>();
        if (rb != null)
            return rb.transform;

        return other.transform.root != null ? other.transform.root : other.transform;
    }

    private void SetDoorBlockers(bool enabled)
    {
        if (doorBlockColliders == null)
            return;

        foreach (Collider col in doorBlockColliders)
        {
            if (col != null)
                col.enabled = enabled;
        }
    }

    private void LockPlayerInput(bool locked)
    {
        if (playerController == null && _player != null)
            playerController = _player.GetComponentInChildren<MonoBehaviour>();

        if (playerController == null)
            return;

        if (playerController is IInputLockable lockable)
        {
            lockable.SetInputLocked(locked);
            return;
        }

        playerController.SendMessage("SetInputLocked", locked, SendMessageOptions.DontRequireReceiver);
    }

    private bool IsWithinInteractDistance(Vector3 playerPosition)
    {
        Vector3 toDoor = transform.position - playerPosition;
        toDoor.y = 0f;
        float maxDist = Mathf.Max(0.1f, maxInteractDistance);
        return toDoor.sqrMagnitude <= maxDist * maxDist;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Editor helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnValidate()
    {
        GetComponent<Collider>().isTrigger = true;

        if (doorVisual != null)
            _doorClosedWorldPos = doorVisual.position;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawPoint(enterPoint,       Color.cyan,    "Enter");
        DrawPoint(exitPoint,        Color.green,   "Exit");
        DrawPoint(teleportTarget,   Color.magenta, "Teleport");
        DrawTeleportToExitArrow();
    }

    private void DrawPoint(Transform t, Color color, string label)
    {
        if (t == null) return;
        Gizmos.color = color;
        Gizmos.DrawSphere(t.position, 0.1f);
        UnityEditor.Handles.Label(t.position + Vector3.up * 0.2f, label);
    }

    private void DrawTeleportToExitArrow()
    {
        if (teleportTarget == null || exitPoint == null)
            return;

        Vector3 from = teleportTarget.position;
        Vector3 to = exitPoint.position;
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawAAPolyLine(4f, from, to);
        UnityEditor.Handles.ConeHandleCap(0, to, Quaternion.LookRotation(dir.normalized), 0.18f, EventType.Repaint);
        UnityEditor.Handles.Label((from + to) * 0.5f + Vector3.up * 0.2f, "Teleport -> Exit");
    }
#endif
}