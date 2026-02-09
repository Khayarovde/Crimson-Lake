using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;

public class VideoTrigger : MonoBehaviour
{
    [Header("Настройка видео")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject videoPanel;
    [SerializeField] private RawImage videoRawImage;
    [SerializeField] private string videoFileName = "cutscene.mp4"; // Имя файла видео в StreamingAssets для WebGL

    [Header("Подсказка (интерактивная)")]
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private Image hintImage;
    [SerializeField] private Sprite hintSprite;
    [SerializeField] private Text hintText;
    [SerializeField] [TextArea] private string hintMessage = "Нажмите E для просмотра";

    [Header("Звук предупреждения (за 3 сек до конца)")]
    [SerializeField] private AudioSource warningAudio;
    [SerializeField] private AudioClip warningClip;

    [Header("Телепортация (за 1 сек до конца видео)")]
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private bool copyRotation = true;

    [Header("Настройки")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string shownPrefsKey = "VideoShown_Trigger1";

    [Header("ВРЕМЕННО: Тестовый режим (смотреть видео много раз)")]
    [SerializeField] private bool testModeAllowRepeat = true;

    [Header("Время событий (сек до конца видео)")]
    [SerializeField] private float warningTimeBeforeEnd = 3f;
    [SerializeField] private float teleportTimeBeforeEnd = 1f;

    // Состояние до видео
    private CursorLockMode initialCursorLockState;
    private bool initialCursorVisible;
    private float initialTimeScale;

    private bool playerInTrigger = false;
    private bool isVideoActive = false;
    private bool soundPlayed = false;
    private bool teleported = false;

    private bool HasShown => !testModeAllowRepeat && PlayerPrefs.GetInt(shownPrefsKey, 0) == 1;

    void Start()
    {
        initialTimeScale = Time.timeScale;
        initialCursorLockState = Cursor.lockState;
        initialCursorVisible = Cursor.visible;

        if (videoRawImage != null && videoPlayer?.targetTexture != null)
        {
            videoRawImage.texture = videoPlayer.targetTexture;
        }

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnd;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.errorReceived += OnVideoError;
        }
        else
        {
            Debug.LogError("[VideoTrigger] VideoPlayer не назначен!", this);
        }

        if (hintText != null) hintText.text = hintMessage;
        if (hintImage != null && hintSprite != null) hintImage.sprite = hintSprite;

        if (hintPanel != null) hintPanel.SetActive(false);
        if (videoPanel != null) videoPanel.SetActive(false);

        #if UNITY_WEBGL
            Debug.LogWarning("[VideoTrigger] WebGL сборка обнаружена. Убедитесь, что видео загружено через WWW или используется StreamingAssets!");
        #endif
    }

    void Update()
    {
        // Блокировка Tab во время видео
        if (isVideoActive && Input.GetKeyDown(KeyCode.Tab))
        {
            return;
        }

        if (playerInTrigger && Input.GetKeyDown(KeyCode.E) && !HasShown && !isVideoActive)
        {
            HideHint();
            StartVideoPlayback();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag) && !HasShown && !isVideoActive)
        {
            playerInTrigger = true;
            if (hintPanel != null) hintPanel.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInTrigger = false;
            if (hintPanel != null && hintPanel.activeSelf)
                hintPanel.SetActive(false);
        }
    }

    private void HideHint()
    {
        if (hintPanel != null) hintPanel.SetActive(false);
    }

    private void StartVideoPlayback()
    {
        isVideoActive = true;
        soundPlayed = false;
        teleported = false;

        if (videoPanel != null) videoPanel.SetActive(true);

        if (videoPlayer != null)
        {
            videoPlayer.time = 0f;
            videoPlayer.isLooping = false;

            #if UNITY_WEBGL
            // В WebGL используем путь через StreamingAssets
            string videoPath = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
            videoPlayer.url = videoPath;
            Debug.Log("[VideoTrigger] WebGL: Загрузка видео из: " + videoPath);
            #endif

            if (!videoPlayer.isPrepared)
            {
                videoPlayer.Prepare();
                videoPlayer.prepareCompleted += OnPreparedPlay;
            }
            else
            {
                videoPlayer.Play();
            }
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (warningAudio != null && warningClip != null)
        {
            warningAudio.clip = warningClip;
            warningAudio.loop = false;
        }

        StartCoroutine(CheckVideoTime());
    }

    private void OnPreparedPlay(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPreparedPlay;
        vp.Play();
    }

    private IEnumerator CheckVideoTime()
    {
        while (videoPlayer != null && (!videoPlayer.isPlaying || !videoPlayer.isPrepared))
            yield return null;

        while (videoPlayer != null && videoPlayer.isPlaying)
        {
            double current = videoPlayer.time;
            double length = videoPlayer.length;

            if (!soundPlayed && current >= length - warningTimeBeforeEnd)
            {
                if (warningAudio != null && warningAudio.clip != null)
                    warningAudio.Play();
                soundPlayed = true;
            }

            if (!teleported && current >= length - teleportTimeBeforeEnd)
            {
                TeleportPlayer();
                FullyRestoreControl();
                teleported = true;
            }

            yield return null;
        }
    }

    private void TeleportPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && teleportTarget != null)
        {
            player.transform.position = teleportTarget.position;
            if (copyRotation)
                player.transform.rotation = teleportTarget.rotation;
        }
    }

    private void FullyRestoreControl()
    {
        Time.timeScale = initialTimeScale;
        Cursor.lockState = initialCursorLockState;
        Cursor.visible = initialCursorVisible;

        Debug.Log("Управление полностью восстановлено (движение, мышь, Tab)");
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        if (!teleported)
            FullyRestoreControl();

        HideEverything();
    }

    private void HideEverything()
    {
        isVideoActive = false;

        if (videoPanel != null) videoPanel.SetActive(false);
        if (videoPlayer != null) videoPlayer.Stop();

        if (!testModeAllowRepeat)
            PlayerPrefs.SetInt(shownPrefsKey, 1);
    }

    // Отладка
    private void OnVideoPrepared(VideoPlayer vp) => Debug.Log("Видео подготовлено");
    private void OnVideoError(VideoPlayer vp, string message) => Debug.LogError("ОШИБКА ВИДЕО: " + message);

    // === КНОПКА СБРОСА В КОНТЕКСТНОМ МЕНЮ (работает всегда!) ===
    [ContextMenu("Reset Video Viewed Flag")]
    private void ResetVideoViewedFlag()
    {
        PlayerPrefs.DeleteKey(shownPrefsKey);
        PlayerPrefs.Save();
        Debug.Log($"<color=cyan>Флаг просмотра видео сброшен! Теперь можно смотреть заново (ключ: {shownPrefsKey})</color>");
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }
}