using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Переход")]
    [Tooltip("Имя следующей сцены (должна быть добавлена в Build Settings!)")]
    public string nextSceneName = "NextScene";

    [Header("Триггер")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Collider triggerZone;

    [Header("Image после смерти босса")]
    [Tooltip("Image/Panel, который показывается только когда босс мёртв и игрок внутри триггера")]
    [SerializeField] private GameObject bossDeathImageRoot;

    [Header("Видео перед переходом")]
    [Tooltip("VideoPlayer для проигрывания ролика перед переходом")]
    public VideoPlayer introVideo;

    [Tooltip("Объект/Canvas с VideoPlayer, который нужно включить на время проигрывания")]
    public GameObject videoRoot;

    [Header("Black Screen")]
    [Tooltip("Черный Image поверх видео (alpha = 1, изначально неактивен)")]
    public GameObject blackScreenRoot;

    [Header("Отключение UI")]
    [Tooltip("Canvas, которые нужно скрыть на время видео")]
    public Canvas[] canvasesToDisable;

    [Tooltip("Поставить игру на паузу на время видео")]
    public bool pauseGameWhilePlaying = true;

    private bool triggered = false; // Чтобы срабатывало только раз
    private bool bossDefeated;
    private bool playerInsideTrigger;
    private float previousTimeScale = 1f;
    private bool videoEnded;
    private AsyncOperation loadOperation;

    private void Awake()
    {
        if (triggerZone == null)
            triggerZone = GetComponent<Collider>();

        if (videoRoot != null)
            videoRoot.SetActive(false);

        if (bossDeathImageRoot != null)
            bossDeathImageRoot.SetActive(false);

        if (introVideo != null)
        {
            introVideo.playOnAwake = false;
            introVideo.Stop();
        }

        if (blackScreenRoot != null)
            blackScreenRoot.SetActive(false);

        RefreshBossDeathImageVisibility();
    }

    public void SetBossDefeated(bool value)
    {
        bossDefeated = value;
        RefreshBossDeathImageVisibility();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInsideTrigger = true;
            RefreshBossDeathImageVisibility();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInsideTrigger = true;
            RefreshBossDeathImageVisibility();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInsideTrigger = false;
            RefreshBossDeathImageVisibility();
        }
    }

    private void Update()
    {
        if (triggered || !bossDefeated || !playerInsideTrigger || string.IsNullOrEmpty(nextSceneName))
            return;

        if (Input.GetMouseButtonDown(0))
        {
            triggered = true;
            playerInsideTrigger = false;

            if (triggerZone != null)
                triggerZone.enabled = false;

            StartCoroutine(TransitionSequence());
        }
    }

    private IEnumerator TransitionSequence()
    {
        // Проигрываем видео, если назначено
        yield return StartCoroutine(PlayIntroVideo());
    }

    private IEnumerator PlayIntroVideo()
    {
        if (introVideo == null)
        {
            SceneManager.LoadScene(nextSceneName);
            yield break;
        }

        if (videoRoot != null)
        {
            videoRoot.SetActive(true);
        }

        SetCanvasesActive(false);

        DontDestroyOnLoad(gameObject);
        if (blackScreenRoot != null)
            DontDestroyOnLoad(blackScreenRoot);

        if (pauseGameWhilePlaying)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        introVideo.playOnAwake = false;
        introVideo.Prepare();
        while (!introVideo.isPrepared)
        {
            yield return null;
        }

        introVideo.loopPointReached += OnVideoFinished;
        introVideo.errorReceived += OnVideoError;

        videoEnded = false;

        introVideo.time = 0d;
        introVideo.Play();

        // Ждём старта воспроизведения
        while (!introVideo.isPlaying && introVideo.frame <= 0)
        {
            yield return null;
        }

        double length = introVideo.length;
        if (length > 0d)
        {
            float timeToBlack = Mathf.Max(0f, (float)length - 1f);
            if (timeToBlack > 0f)
                yield return new WaitForSecondsRealtime(timeToBlack);
        }
        else
        {
            while (!videoEnded)
                yield return null;
        }

        if (blackScreenRoot != null)
            blackScreenRoot.SetActive(true);

        loadOperation = SceneManager.LoadSceneAsync(nextSceneName);
        if (loadOperation != null)
            loadOperation.allowSceneActivation = true;

        if (loadOperation != null)
        {
            while (!loadOperation.isDone)
                yield return null;
        }

        yield return new WaitForSecondsRealtime(2f);

        CleanupAfterVideo();
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        videoEnded = true;
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[SceneTransitionTrigger] Ошибка проигрывания видео: {message}");
        videoEnded = true;
    }

    private void CleanupAfterVideo()
    {
        if (introVideo != null)
        {
            introVideo.loopPointReached -= OnVideoFinished;
            introVideo.errorReceived -= OnVideoError;

            if (introVideo.isPlaying)
                introVideo.Stop();
        }

        if (videoRoot != null)
            videoRoot.SetActive(false);

        if (pauseGameWhilePlaying)
            Time.timeScale = previousTimeScale;

        if (blackScreenRoot != null)
            blackScreenRoot.SetActive(false);

        SetCanvasesActive(true);

        Destroy(gameObject);
    }

    private void RefreshBossDeathImageVisibility()
    {
        if (bossDeathImageRoot == null)
            return;

        bool shouldShow = bossDefeated && playerInsideTrigger && !triggered;
        bossDeathImageRoot.SetActive(shouldShow);
    }

    private void SetCanvasesActive(bool active)
    {
        if (canvasesToDisable == null)
            return;

        for (int i = 0; i < canvasesToDisable.Length; i++)
        {
            Canvas canvas = canvasesToDisable[i];
            if (canvas != null)
                canvas.gameObject.SetActive(active);
        }
    }

    // Автонастройка в редакторе
    private void Reset()
    {
        triggerZone = GetComponent<Collider>();
        if (triggerZone != null)
            triggerZone.isTrigger = true;
    }

    // Проверки в редакторе
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("Назначь имя следующей сцены в 'Next Scene Name'!", this);
        }

        if (triggerZone == null)
            triggerZone = GetComponent<Collider>();
    }
}