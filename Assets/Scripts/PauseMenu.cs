using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;

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

    private bool isPaused = false;

    private const string MusicVolKey = "MusicVol";
    private const string SFXVolKey   = "SFXVol";

    private void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

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
    }

    public void Resume()
    {
        pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
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

        ApplyVolumeToAll(musicVol, true);
        ApplyVolumeToAll(sfxVol, false);
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
        PlayerPrefs.SetFloat(MusicVolKey, value);
        PlayerPrefs.Save();
        ApplyVolumeToAll(value, true);
    }

    public void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFXVolKey, value);
        PlayerPrefs.Save();
        ApplyVolumeToAll(value, false);
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
}