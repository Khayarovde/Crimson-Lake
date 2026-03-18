using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(Collider))]
public class CutsceneVideoTrigger : MonoBehaviour
{
    [Header("Save Key")]
    [SerializeField] private string cutsceneEventId = "cutscene_intro";

    [Header("Playback Rules")]
    [SerializeField] private bool playOnlyOnce = true;
    [SerializeField] private bool disableTriggerAfterPlay = true;

    [Tooltip("Для тестов: игнорирует one-shot и сохраненный статус просмотра.")]
    [SerializeField] private bool allowReplayForTesting = false;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Collider triggerZone;

    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoContainer;
    [SerializeField] private bool pauseGameWhilePlaying = true;

    [Header("Skip (Optional)")]
    [SerializeField] private bool allowSkip = false;
    [SerializeField] private KeyCode skipKey = KeyCode.Space;

    private bool isPlaying;
    private float previousTimeScale = 1f;

    private void Reset()
    {
        triggerZone = GetComponent<Collider>();
        if (triggerZone != null)
            triggerZone.isTrigger = true;
    }

    private void Awake()
    {
        if (triggerZone == null)
            triggerZone = GetComponent<Collider>();

        if (triggerZone != null && !triggerZone.isTrigger)
        {
            Debug.LogWarning($"[CutsceneVideoTrigger] Collider на {name} должен быть Is Trigger = true");
        }

        if (videoContainer != null)
            videoContainer.SetActive(false);
    }

    private void Start()
    {
        if (ShouldBlockBySave())
            DisableTrigger();
    }

    private void Update()
    {
        if (!isPlaying || !allowSkip)
            return;

        if (Input.GetKeyDown(skipKey))
            FinishPlayback(markAsSeen: true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isPlaying)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (ShouldBlockBySave())
        {
            DisableTrigger();
            return;
        }

        if (videoPlayer == null)
        {
            Debug.LogWarning("[CutsceneVideoTrigger] Не назначен VideoPlayer");
            return;
        }

        PlayCutscene();
    }

    private void PlayCutscene()
    {
        isPlaying = true;

        if (videoContainer != null)
            videoContainer.SetActive(true);

        if (pauseGameWhilePlaying)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.Play();
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        FinishPlayback(markAsSeen: true);
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[CutsceneVideoTrigger] Ошибка проигрывания видео: {message}");
        FinishPlayback(markAsSeen: false);
    }

    private void FinishPlayback(bool markAsSeen)
    {
        if (!isPlaying)
            return;

        isPlaying = false;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;

            if (videoPlayer.isPlaying)
                videoPlayer.Stop();
        }

        if (videoContainer != null)
            videoContainer.SetActive(false);

        if (pauseGameWhilePlaying)
            Time.timeScale = previousTimeScale;

        if (markAsSeen && !allowReplayForTesting && !string.IsNullOrWhiteSpace(cutsceneEventId))
            SaveManager.MarkEventSeen(cutsceneEventId);

        if (markAsSeen && playOnlyOnce && !allowReplayForTesting && disableTriggerAfterPlay)
            DisableTrigger();
    }

    private bool ShouldBlockBySave()
    {
        if (!playOnlyOnce || allowReplayForTesting)
            return false;

        if (string.IsNullOrWhiteSpace(cutsceneEventId))
            return false;

        return SaveManager.HasSeenEvent(cutsceneEventId);
    }

    private void DisableTrigger()
    {
        if (triggerZone != null)
            triggerZone.enabled = false;

        enabled = false;
    }
}
