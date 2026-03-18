using System.Collections;
using DG.Tweening;
using UnityEngine;
using Ami.BroAudio;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DoorSlamIntroTrigger : MonoBehaviour
{
    [Header("Привязки")]
    [SerializeField] private DoorTeleportTrigger doorTeleportTrigger;
    [SerializeField] private Transform doorVisual;
    [SerializeField] private Transform shakeTarget;

    [Header("Настройки события")]
    [SerializeField] private string eventId = "DoorSlamIntro_Main";
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool testModeAllowRepeat;

    [Header("Падение двери по Y")]
    [SerializeField] private float dropDistance = 1.5f;
    [SerializeField] private float dropDuration = 0.25f;
    [SerializeField] private Ease dropEase = Ease.InQuart;
    [SerializeField] private float returnDuration = 0.15f;
    [SerializeField] private Ease returnEase = Ease.OutSine;

    [Header("Тряска камеры")]
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 0.22f;
    [SerializeField] private int shakeVibrato = 25;
    [SerializeField] private float shakeRandomness = 90f;

    [Header("Звук захлопывания")]
    [SerializeField] private GameObject broAudioObject;
    [SerializeField] private SoundSource broAudioSoundSource;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip slamClip;

    private bool isPlaying;
    private bool wasInitialized;
    private Vector3 initialDoorPos;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;

        ResolveBroAudioReference();

        if (doorTeleportTrigger == null)
            doorTeleportTrigger = GetComponent<DoorTeleportTrigger>();

        if (doorTeleportTrigger != null)
            doorTeleportTrigger.enabled = true;

        if (doorVisual != null)
        {
            initialDoorPos = doorVisual.position;
            wasInitialized = true;
        }

        if (!testModeAllowRepeat && SaveManager.HasSeenEvent(eventId))
            enabled = false;
    }

    private void OnEnable()
    {
        if (doorVisual != null && !wasInitialized)
        {
            initialDoorPos = doorVisual.position;
            wasInitialized = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isPlaying)
            return;

        if (!IsPlayer(other))
            return;

        if (!testModeAllowRepeat && SaveManager.HasSeenEvent(eventId))
        {
            enabled = false;
            return;
        }

        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        isPlaying = true;

        if (doorVisual != null)
        {
            Vector3 startPos = wasInitialized ? initialDoorPos : doorVisual.position;
            Vector3 downPos = startPos + Vector3.down * Mathf.Max(0f, dropDistance);
            bool downDone = false;

            doorVisual.DOMove(downPos, Mathf.Max(0.05f, dropDuration))
                .SetEase(dropEase)
                .OnComplete(() => downDone = true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            yield return new WaitUntil(() => downDone);
            PlaySlamSound();

            Transform target = ResolveShakeTarget();
            if (target != null)
            {
                target.DOShakePosition(
                        Mathf.Max(0.05f, shakeDuration),
                        Mathf.Max(0f, shakeStrength),
                        Mathf.Max(1, shakeVibrato),
                        Mathf.Max(0f, shakeRandomness),
                        false,
                        true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);

                yield return new WaitForSeconds(Mathf.Max(0.05f, shakeDuration));
            }

            bool upDone = false;
            doorVisual.DOMove(startPos, Mathf.Max(0.05f, returnDuration))
                .SetEase(returnEase)
                .OnComplete(() => upDone = true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            yield return new WaitUntil(() => upDone);
        }
        else
        {
            PlaySlamSound();

            Transform target = ResolveShakeTarget();
            if (target != null)
            {
                target.DOShakePosition(
                        Mathf.Max(0.05f, shakeDuration),
                        Mathf.Max(0f, shakeStrength),
                        Mathf.Max(1, shakeVibrato),
                        Mathf.Max(0f, shakeRandomness),
                        false,
                        true)
                    .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, shakeDuration));
        }

        if (!testModeAllowRepeat)
        {
            SaveManager.MarkEventSeen(eventId);
            enabled = false;
        }

        if (doorTeleportTrigger != null)
            doorTeleportTrigger.enabled = true;

        isPlaying = false;
    }

    private Transform ResolveShakeTarget()
    {
        if (shakeTarget != null)
            return shakeTarget;

        if (Camera.main != null)
            return Camera.main.transform;

        return null;
    }

    private void PlaySlamSound()
    {
        ResolveBroAudioReference();

        if (broAudioSoundSource != null)
        {
            broAudioSoundSource.Play();
            return;
        }

        if (audioSource != null && slamClip != null)
            audioSource.PlayOneShot(slamClip);
    }

    private void ResolveBroAudioReference()
    {
        if (broAudioSoundSource == null && broAudioObject != null)
            broAudioSoundSource = broAudioObject.GetComponent<SoundSource>();
    }

    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(playerTag)
               || (other.transform.root != null && other.transform.root.CompareTag(playerTag));
    }

    [ContextMenu("Reset Event Flag")]
    private void ResetEventFlagForTesting()
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return;

        Debug.Log($"[DoorSlamIntroTrigger] Сброс флага события выполняется через удаление сохранений или смену eventId: {eventId}");
    }

    private void OnValidate()
    {
        ResolveBroAudioReference();
    }
}
