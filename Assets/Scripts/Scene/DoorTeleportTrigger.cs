using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DoorTeleportTrigger : MonoBehaviour
{
    private enum DoorState
    {
        Closed,
        Open,
        LockedNeedKey
    }

    [Header("Target")]
    [SerializeField] private Transform teleportTarget;

    [Header("Door Open Animation")]
    [SerializeField] private Transform doorVisual;
    [SerializeField] private float doorOpenYOffset = 2f;
    [SerializeField] private float doorOpenDuration = 0.6f;
    [SerializeField] private Ease doorOpenEase = Ease.OutSine;

    [Header("Door Collision")]
    [SerializeField] private Collider[] doorBlockColliders;

    [Header("Door Auto Close")]
    [SerializeField] private bool autoCloseDoor = true;
    [SerializeField] private float autoCloseDelay = 2.5f;
    [SerializeField] private float doorCloseDuration = 0.45f;
    [SerializeField] private Ease doorCloseEase = Ease.InSine;

    [Header("Door State")]
    [SerializeField] private DoorState doorState = DoorState.Closed;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Door Sound")]
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip doorOpenClip;

    [Header("Player Door Animation")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string playerDoorStateName = "Blend Tree_WALK";
    [SerializeField] private float playerDoorAnimationDuration = 0.8f;
    [SerializeField] private float playerAnimationTransition = 0.1f;

    [Header("Approach Door")]
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private float autoApproachDuration = 0.4f;
    [SerializeField] private Ease autoApproachEase = Ease.Linear;
    [SerializeField] private float arriveDistanceThreshold = 0.05f;
    [SerializeField] private float approachTimeoutPadding = 0.35f;

    [Header("Hint UI")]
    [SerializeField] private GameObject hintCanvas;
    [SerializeField] private RectTransform hintSpriteTransform;
    [SerializeField] private Image hintImage;

    [Header("State Sprites")]
    [SerializeField] private Sprite closedDoorSprite;
    [SerializeField] private Sprite openDoorSprite;
    [SerializeField] private Sprite needKeySprite;

    [Header("Hint Animation (DOTween)")]
    [SerializeField] private float pulseScaleMultiplier = 1.1f;
    [SerializeField] private float pulseDuration = 0.35f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;

    private Transform playerTransform;
    private Sequence hintSequence;
    private Tween doorOpenTween;
    private Tween playerApproachTween;
    private Coroutine autoCloseRoutine;
    private TankController cachedPlayerController;
    private Vector3 doorClosedLocalPosition;
    private Vector3 hintBaseScale;
    private bool hasHintBaseScale;
    private bool isInteracting;
    private bool isDoorOpened;
    private bool playerInsideTrigger;
    private int playerTriggerContacts;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
        ResolveHintReferences();
        ResolveDoorBlockColliders();

        if (doorVisual == null)
        {
            doorVisual = transform;
        }

        doorClosedLocalPosition = doorVisual.localPosition;

        if (doorAudioSource == null)
        {
            doorAudioSource = GetComponent<AudioSource>();
        }
    }

    private void Awake()
    {
        EnsureTriggerCollider();

        ResolveHintReferences();
        ResolveDoorBlockColliders();

        if (hintSpriteTransform != null)
        {
            hintBaseScale = hintSpriteTransform.localScale;
            hasHintBaseScale = true;
        }

        ApplyDoorBlockersEnabled(!isDoorOpened);

        HideHint();
    }

    private void Update()
    {
        if (!playerInsideTrigger || playerTransform == null)
        {
            return;
        }

        RefreshHintSprite();

        if (Input.GetKeyDown(interactKey))
        {
            HandleInteraction();
        }
    }

    private void OnDisable()
    {
        StopDoorAnimation();
        StopApproachTween();
        StopAutoCloseRoutine();
        ApplyDoorBlockersEnabled(true);
        SetPlayerInteractionLock(false);
        HideHint();
        playerTransform = null;
        playerInsideTrigger = false;
        playerTriggerContacts = 0;
        isInteracting = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        Transform enteringPlayer = ResolvePlayerTransform(other);
        if (enteringPlayer == null)
        {
            return;
        }

        if (playerTransform != null && enteringPlayer != playerTransform)
        {
            return;
        }

        playerTransform = enteringPlayer;
        playerTriggerContacts++;
        playerInsideTrigger = true;
        cachedPlayerController = null;
        TryResolvePlayerAnimator();
        ShowHint();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        Transform exitingPlayer = ResolvePlayerTransform(other);
        if (exitingPlayer != playerTransform)
        {
            return;
        }

        playerTriggerContacts = Mathf.Max(0, playerTriggerContacts - 1);
        playerInsideTrigger = playerTriggerContacts > 0;

        if (isInteracting)
        {
            return;
        }

        if (playerInsideTrigger)
        {
            return;
        }

        playerTransform = null;
        cachedPlayerController = null;
        HideHint();
    }

    private Transform ResolvePlayerTransform(Collider other)
    {
        if (other.attachedRigidbody != null)
        {
            return other.attachedRigidbody.transform;
        }

        Rigidbody parentBody = other.GetComponentInParent<Rigidbody>();
        if (parentBody != null)
        {
            return parentBody.transform;
        }

        Transform taggedRoot = other.transform.root;
        if (taggedRoot != null && taggedRoot.CompareTag(playerTag))
        {
            return taggedRoot;
        }

        return other.transform;
    }

    private void TeleportPlayer()
    {
        if (teleportTarget == null || playerTransform == null)
        {
            return;
        }

        playerTransform.SetPositionAndRotation(teleportTarget.position, teleportTarget.rotation);
    }

    private void ShowHint()
    {
        if (hintCanvas == null)
        {
            return;
        }

        RefreshHintSprite();
        hintCanvas.SetActive(true);
        StartHintAnimation();
    }

    private void HandleInteraction()
    {
        if (isInteracting)
        {
            return;
        }

        switch (doorState)
        {
            case DoorState.Open:
                StartInteractionSequence();
                break;

            case DoorState.LockedNeedKey:
                // TODO: Здесь добавить проверку инвентаря/ключа и логику открытия двери.
                // Заблокировано: без ключа нельзя открывать дверь, проигрывать анимации и телепортироваться.
                break;

            case DoorState.Closed:
                // Заблокировано: закрытая дверь не должна запускать анимации и телепорт.
                break;

            default:
                break;
        }
    }

    private void StartInteractionSequence()
    {
        if (teleportTarget == null || playerTransform == null)
        {
            return;
        }

        isInteracting = true;
        StartCoroutine(PlayInteractionSequence());
    }

    private System.Collections.IEnumerator PlayInteractionSequence()
    {
        SetPlayerInteractionLock(true);

        try
        {
            StartCoroutine(PlayDoorOpening());

            PlayPlayerDoorAnimation();
            yield return MovePlayerToDoorPoint();

            TeleportPlayer();
        }
        finally
        {
            SetPlayerInteractionLock(false);
            isInteracting = false;
            ClearPlayerInteractionContext();
        }
    }

    private System.Collections.IEnumerator PlayDoorOpening()
    {
        if (isDoorOpened)
        {
            yield break;
        }

        if (doorAudioSource != null && doorOpenClip != null)
        {
            doorAudioSource.PlayOneShot(doorOpenClip);
        }

        if (doorVisual == null)
        {
            isDoorOpened = true;
            ApplyDoorBlockersEnabled(false);
            yield break;
        }

        StopDoorAnimation();
        ApplyDoorBlockersEnabled(false);

        Vector3 openedPosition = doorClosedLocalPosition + Vector3.up * doorOpenYOffset;
        bool completed = false;

        doorOpenTween = doorVisual.DOLocalMove(openedPosition, doorOpenDuration)
            .SetEase(doorOpenEase)
            .OnComplete(() => completed = true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        while (!completed)
        {
            yield return null;
        }

        isDoorOpened = true;
        if (doorState == DoorState.Closed)
        {
            doorState = DoorState.Open;
        }

        RestartAutoCloseRoutine();
    }

    private System.Collections.IEnumerator MovePlayerToDoorPoint()
    {
        if (interactionPoint == null || playerTransform == null)
        {
            yield break;
        }

        Transform targetPlayer = playerTransform;
        Transform targetPoint = interactionPoint;
        if (targetPlayer == null || targetPoint == null)
        {
            yield break;
        }

        StopApproachTween();

        bool completed = false;
        float distance = Vector3.Distance(targetPlayer.position, targetPoint.position);
        if (distance <= Mathf.Max(0f, arriveDistanceThreshold))
        {
            targetPlayer.rotation = targetPoint.rotation;
            yield break;
        }

        float playerSpeed = GetPlayerApproachSpeed();
        float approachDuration = playerSpeed > 0.01f
            ? distance / playerSpeed
            : Mathf.Max(0.01f, autoApproachDuration);
        float timeout = Mathf.Max(0.05f, approachDuration + Mathf.Max(0f, approachTimeoutPadding));

        playerApproachTween = targetPlayer.DOMove(targetPoint.position, Mathf.Max(0.01f, approachDuration))
            .SetEase(autoApproachEase)
            .OnComplete(() => completed = true)
            .OnKill(() => completed = true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        float elapsed = 0f;
        while (!completed && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!completed && playerApproachTween != null)
        {
            playerApproachTween.Kill();
        }

        if (targetPlayer != null && targetPoint != null)
        {
            targetPlayer.rotation = targetPoint.rotation;
        }
    }

    private float GetPlayerApproachSpeed()
    {
        TankController controller = GetPlayerController();
        if (controller == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, controller.moveSpeed);
    }

    private TankController GetPlayerController()
    {
        if (cachedPlayerController != null)
        {
            return cachedPlayerController;
        }

        if (playerTransform == null)
        {
            return null;
        }

        cachedPlayerController = playerTransform.GetComponentInParent<TankController>();
        return cachedPlayerController;
    }

    private void SetPlayerInteractionLock(bool isLocked)
    {
        TankController controller = GetPlayerController();
        if (controller == null)
        {
            return;
        }

        controller.SetAnimationLock(isLocked, isLocked ? playerDoorStateName : null);
    }

    private void RestartAutoCloseRoutine()
    {
        if (!autoCloseDoor || !isDoorOpened)
        {
            return;
        }

        StopAutoCloseRoutine();
        autoCloseRoutine = StartCoroutine(AutoCloseDoorRoutine());
    }

    private System.Collections.IEnumerator AutoCloseDoorRoutine()
    {
        if (autoCloseDelay > 0f)
        {
            yield return new WaitForSeconds(autoCloseDelay);
        }

        if (isInteracting)
        {
            autoCloseRoutine = StartCoroutine(AutoCloseDoorRoutine());
            yield break;
        }

        yield return PlayDoorClosing();
        autoCloseRoutine = null;
    }

    private System.Collections.IEnumerator PlayDoorClosing()
    {
        if (!isDoorOpened || doorVisual == null)
        {
            yield break;
        }

        StopDoorAnimation();

        bool completed = false;
        doorOpenTween = doorVisual.DOLocalMove(doorClosedLocalPosition, Mathf.Max(0.01f, doorCloseDuration))
            .SetEase(doorCloseEase)
            .OnComplete(() => completed = true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        while (!completed)
        {
            yield return null;
        }

        isDoorOpened = false;
        ApplyDoorBlockersEnabled(true);
    }

    private void PlayPlayerDoorAnimation()
    {
        TryResolvePlayerAnimator();
        if (playerAnimator == null || string.IsNullOrEmpty(playerDoorStateName))
        {
            return;
        }

        if (playerAnimator.HasState(0, Animator.StringToHash(playerDoorStateName)))
        {
            playerAnimator.CrossFadeInFixedTime(playerDoorStateName, Mathf.Max(0f, playerAnimationTransition), 0);
        }
    }

    private void TryResolvePlayerAnimator()
    {
        if (playerAnimator != null)
        {
            return;
        }

        if (playerTransform != null)
        {
            playerAnimator = playerTransform.GetComponentInChildren<Animator>();
        }
    }

    private void RefreshHintSprite()
    {
        if (hintImage == null)
        {
            return;
        }

        Sprite targetSprite = GetCurrentStateSprite();
        if (targetSprite != null)
        {
            hintImage.sprite = targetSprite;
        }
    }

    private void ResolveHintReferences()
    {
        if (hintImage != null && hintSpriteTransform == null)
        {
            hintSpriteTransform = hintImage.rectTransform;
        }

        if (hintImage == null && hintSpriteTransform != null)
        {
            hintImage = hintSpriteTransform.GetComponent<Image>();
        }

        if (hintImage == null && hintCanvas != null)
        {
            hintImage = hintCanvas.GetComponent<Image>();
        }

        if (hintSpriteTransform == null && hintImage != null)
        {
            hintSpriteTransform = hintImage.rectTransform;
        }

        if (hintSpriteTransform == null && hintCanvas != null)
        {
            hintSpriteTransform = hintCanvas.GetComponent<RectTransform>();
        }
    }

    private Sprite GetCurrentStateSprite()
    {
        switch (doorState)
        {
            case DoorState.Open:
                return openDoorSprite;

            case DoorState.LockedNeedKey:
                return needKeySprite;

            case DoorState.Closed:
            default:
                return closedDoorSprite;
        }
    }

    private void HideHint()
    {
        StopHintAnimation();

        if (hintCanvas != null)
        {
            hintCanvas.SetActive(false);
        }
    }

    private void StartHintAnimation()
    {
        if (hintSpriteTransform == null)
        {
            return;
        }

        if (!hasHintBaseScale)
        {
            hintBaseScale = hintSpriteTransform.localScale;
            hasHintBaseScale = true;
        }

        StopHintAnimation();
        hintSpriteTransform.localScale = hintBaseScale;

        hintSequence = DOTween.Sequence();
        hintSequence.Append(hintSpriteTransform.DOScale(hintBaseScale * pulseScaleMultiplier, pulseDuration).SetEase(pulseEase));
        hintSequence.Append(hintSpriteTransform.DOScale(hintBaseScale, pulseDuration).SetEase(pulseEase));
        hintSequence.SetLoops(-1);
        hintSequence.SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void StopHintAnimation()
    {
        if (hintSequence != null)
        {
            hintSequence.Kill();
            hintSequence = null;
        }

        if (hintSpriteTransform != null && hasHintBaseScale)
        {
            hintSpriteTransform.localScale = hintBaseScale;
        }
    }

    private void StopDoorAnimation()
    {
        if (doorOpenTween != null)
        {
            doorOpenTween.Kill();
            doorOpenTween = null;
        }
    }

    private void StopApproachTween()
    {
        if (playerApproachTween != null)
        {
            playerApproachTween.Kill();
            playerApproachTween = null;
        }
    }

    private void StopAutoCloseRoutine()
    {
        if (autoCloseRoutine != null)
        {
            StopCoroutine(autoCloseRoutine);
            autoCloseRoutine = null;
        }
    }

    private void ClearPlayerInteractionContext()
    {
        playerInsideTrigger = false;
        playerTriggerContacts = 0;
        playerTransform = null;
        cachedPlayerController = null;
        HideHint();
    }

    private void EnsureTriggerCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void ResolveDoorBlockColliders()
    {
        if (doorBlockColliders != null && doorBlockColliders.Length > 0)
        {
            return;
        }

        Transform source = doorVisual != null ? doorVisual : transform;
        Collider[] found = source.GetComponentsInChildren<Collider>(true);
        List<Collider> blockers = new List<Collider>(found.Length);

        for (int i = 0; i < found.Length; i++)
        {
            Collider col = found[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            blockers.Add(col);
        }

        doorBlockColliders = blockers.ToArray();
    }

    private void ApplyDoorBlockersEnabled(bool enabled)
    {
        if (doorBlockColliders == null)
        {
            return;
        }

        for (int i = 0; i < doorBlockColliders.Length; i++)
        {
            Collider col = doorBlockColliders[i];
            if (col == null)
            {
                continue;
            }

            col.enabled = enabled;
        }
    }
}
