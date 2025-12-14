using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("Панели паузы")]
    [SerializeField] private GameObject pausePanel;      // ← Основная панель паузы (изначально SetActive(false))
    [SerializeField] private GameObject settingsPanel;   // ← Подпанель настроек внутри паузы (изначально false)
    
    [Header("Слайдеры звука (дублируют главное меню)")]
    [SerializeField] private Slider musicSlider;         // ← Слайдер музыки
    [SerializeField] private Slider sfxSlider;           // ← Слайдер звуков
    
    [Header("Источники звука (опционально)")]
    [SerializeField] private AudioSource musicSource;    // ← Фоновая музыка в этой сцене
    [SerializeField] private AudioSource[] sfxSources;   // ← Массив SFX в этой сцене

    private bool isPaused = false;
    private const string MusicVolKey = "MusicVol";
    private const string SFXVolKey = "SFXVol";

    void Start()
    {
        LoadSettings(); // Загружаем настройки (сохранённые из меню)
        
        // Изначально: курсор заблокирован (для FPS/3D)
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }

    // Пауза: ESC нажали первый раз
    private void Pause()
    {
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
        // Cursor.lockState = CursorLockMode.None;
        // Cursor.visible = true;
    }

    // Продолжить: ESC второй раз или кнопка Resume
    public void Resume()
    {
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
    }

    // Настройки: переключить подпанель
    public void ToggleSettings()
    {
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    // Закрыть настройки
    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    // Вернуться в главное меню
    public void ToMainMenu()
    {
        Time.timeScale = 1f; // Снимаем паузу перед загрузкой
        SceneManager.LoadScene("Menu"); // ← ИЗМЕНИТЕ на точное имя вашей сцены меню (проверьте в File > Build Settings)
    }

    // Выход из игры
    public void QuitGame()
    {
        Application.Quit();
    }

    // Загрузка настроек из PlayerPrefs (по умолчанию 80%)
    private void LoadSettings()
    {
        float musicVol = PlayerPrefs.GetFloat(MusicVolKey, 0.8f);
        float sfxVol = PlayerPrefs.GetFloat(SFXVolKey, 0.8f);

        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null) sfxSlider.value = sfxVol;

        ApplyVolumes();
    }

    // Применить громкость
    private void ApplyVolumes()
    {
        float musicVol = musicSlider != null ? musicSlider.value : PlayerPrefs.GetFloat(MusicVolKey, 0.8f);
        float sfxVol = sfxSlider != null ? sfxSlider.value : PlayerPrefs.GetFloat(SFXVolKey, 0.8f);

        if (musicSource != null) musicSource.volume = musicVol;

        foreach (AudioSource sfx in sfxSources)
        {
            if (sfx != null) sfx.volume = sfxVol;
        }
    }

    // Изменение музыки (OnValueChanged слайдера)
    public void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicVolKey, value);
        ApplyVolumes();
    }

    // Изменение SFX
    public void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFXVolKey, value);
        ApplyVolumes();
    }
}