using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class PauseMenu : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Слайдеры звука")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("=== МУЗЫКА ===")]
    [SerializeField] private AudioSource[] musicSources;        // Прямые ссылки на AudioSource
    [SerializeField] private AudioClip[] musicClips;            // Или по клипам

    [Header("=== ЗВУКОВЫЕ ЭФФЕКТЫ (SFX) ===")]
    [SerializeField] private AudioSource[] sfxSources;          // Прямые ссылки на AudioSource
    [SerializeField] private AudioClip[] sfxClips;              // Или по клипам (для PlayOneShot и динамики)

    [Header("Pause Music Effect")]
    [SerializeField] private float pauseLowPassCutoff = 800f;
    [SerializeField] private float pauseLowPassFadeTime = 0.15f;

    private readonly List<AudioLowPassFilter> pauseMusicFilters = new List<AudioLowPassFilter>();
    private readonly Dictionary<AudioLowPassFilter, Coroutine> filterFades = new Dictionary<AudioLowPassFilter, Coroutine>();

    private bool isPaused = false;

    private const string MusicVolKey = "MusicVol";
    private const string SFXVolKey   = "SFXVol";

    private void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        SettingsManager.GetOrCreate();
        LoadSettings();

        Debug.Log($"[PauseMenu] Музыка: {musicSources.Length} источников + {musicClips.Length} клипов | " +
                  $"SFX: {sfxSources.Length} источников + {sfxClips.Length} клипов");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    private void Pause()
    {
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;

        ForcePausePanelToTop();
        EnableAllButtonsInPause();
        StartCoroutine(SelectFirstButtonDelayed());

        ApplyPauseMusicEffect(true);
    }

    public void Resume()
    {
        pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;

        ApplyPauseMusicEffect(false);
    }

    // === UI фиксы ===
    private void ForcePausePanelToTop()
    {
        if (pausePanel == null) return;

        Canvas canvas = pausePanel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 1000;
            canvas.overrideSorting = true;
        }

        pausePanel.transform.SetAsLastSibling();
        if (settingsPanel != null && settingsPanel.activeSelf)
            settingsPanel.transform.SetAsLastSibling();
    }

    private void EnableAllButtonsInPause()
    {
        if (pausePanel == null) return;

        Button[] buttons = pausePanel.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            btn.interactable = true;
            Image img = btn.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = true;
        }

        Slider[] sliders = pausePanel.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            if (slider.fillRect != null)
            {
                Image fill = slider.fillRect.GetComponent<Image>();
                if (fill != null)
                    fill.raycastTarget = true;
            }

            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                if (handle != null)
                    handle.raycastTarget = true;
            }
        }
    }

    private IEnumerator SelectFirstButtonDelayed()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (EventSystem.current == null)
        {
            Debug.LogError("Нет EventSystem в сцене!");
            yield break;
        }

        var first = pausePanel.GetComponentInChildren<Button>(true);
        if (first != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }

    // === УНИВЕРСАЛЬНОЕ УПРАВЛЕНИЕ ЗВУКОМ ===

    private void LoadSettings()
    {
        float musicVol = PlayerPrefs.GetFloat(MusicVolKey, 0.8f);
        float sfxVol   = PlayerPrefs.GetFloat(SFXVolKey, 0.8f);

        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null)   sfxSlider.value   = sfxVol;

        // Применяем через единый менеджер (и не ломаем zoneVolume в MusicZoneTrigger)
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMusicVolume(musicVol);
            SettingsManager.Instance.SetSFXVolume(sfxVol);
        }
    }

    private void ApplyVolumeToAll(float volume, bool isMusic)
    {
        var sourcesArray = isMusic ? musicSources : sfxSources;
        var clipsArray   = isMusic ? musicClips   : sfxClips;

        int affected = 0;

        // 1. Сначала прямые AudioSource
        foreach (var source in sourcesArray)
        {
            if (source != null)
            {
                source.volume = volume;
                affected++;
            }
        }

        // 2. Потом по клипам (для PlayOneShot и динамических объектов)
        AudioSource[] allSources = FindObjectsOfType<AudioSource>();
        foreach (var source in allSources)
        {
            if (source == null || source.clip == null) continue;

            bool matchesClip = false;
            foreach (var clip in clipsArray)
            {
                if (clip != null && source.clip == clip)
                {
                    matchesClip = true;
                    break;
                }
            }

            if (matchesClip)
            {
                source.volume = volume;
                affected++;
            }
        }

        Debug.Log($"[PauseMenu] {(isMusic ? "Музыка" : "SFX")} громкость {volume} применена к {affected} источникам.");
    }

    public void OnMusicChanged(float value)
    {
        SettingsManager.GetOrCreate().SetMusicVolume(value);
    }

    public void OnSFXChanged(float value)
    {
        SettingsManager.GetOrCreate().SetSFXVolume(value);
    }

    // === Остальные кнопки ===
    public void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
            if (settingsPanel.activeSelf)
                settingsPanel.transform.SetAsLastSibling();
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void ToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu");
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void ApplyPauseMusicEffect(bool paused)
    {
        if (paused)
        {
            pauseMusicFilters.Clear();

            AudioSource[] allSources = FindObjectsOfType<AudioSource>();
            foreach (var source in allSources)
            {
                if (source == null || !IsMusicSource(source))
                    continue;

                var filter = source.GetComponent<AudioLowPassFilter>();
                if (filter == null)
                    filter = source.gameObject.AddComponent<AudioLowPassFilter>();

                filter.enabled = true;
                pauseMusicFilters.Add(filter);
                StartFilterFade(filter, pauseLowPassCutoff);
            }
        }
        else
        {
            foreach (var filter in pauseMusicFilters)
            {
                if (filter == null) continue;
                StartFilterFade(filter, 22000f, disableAtEnd: true);
            }
            pauseMusicFilters.Clear();
        }
    }

    private bool IsMusicSource(AudioSource source)
    {
        if (source.gameObject.CompareTag("Music"))
            return true;

        return source.GetComponentInParent<MusicZoneTrigger>() != null;
    }

    private void StartFilterFade(AudioLowPassFilter filter, float targetCutoff, bool disableAtEnd = false)
    {
        if (filterFades.TryGetValue(filter, out var running) && running != null)
            StopCoroutine(running);

        var routine = StartCoroutine(FadeLowPass(filter, targetCutoff, disableAtEnd));
        filterFades[filter] = routine;
    }

    private IEnumerator FadeLowPass(AudioLowPassFilter filter, float targetCutoff, bool disableAtEnd)
    {
        if (filter == null) yield break;

        float startCutoff = filter.cutoffFrequency;
        float t = 0f;
        float duration = Mathf.Max(0.01f, pauseLowPassFadeTime);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            filter.cutoffFrequency = Mathf.Lerp(startCutoff, targetCutoff, t / duration);
            yield return null;
        }

        filter.cutoffFrequency = targetCutoff;

        if (disableAtEnd)
            filter.enabled = false;
    }
}