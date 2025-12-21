using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    [Header("Панель настроек")]
    [SerializeField] private GameObject settingsPanel; // ← Перетащите панель настроек

    [Header("Слайдеры звука")]
    [SerializeField] private Slider musicSlider; // ← Слайдер музыки
    [SerializeField] private Slider sfxSlider;   // ← Слайдер звуков эффектов

    private const string MusicVolKey = "MusicVol";
    private const string SFXVolKey   = "SFXVol";

    void Start()
    {
        LoadSettings();
    }

    // Загружаем сохранённые значения (по умолчанию 80%)
    private void LoadSettings()
    {
        float musicVol = PlayerPrefs.GetFloat(MusicVolKey, 0.8f);
        float sfxVol   = PlayerPrefs.GetFloat(SFXVolKey, 0.8f);

        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null) sfxSlider.value = sfxVol;

        ApplyVolumes();
    }

    // Универсальное применение громкости ко ВСЕМ AudioSource в сцене
    // Тегайте GameObject с музыкой тегом "Music" (остальные = SFX)
    private void ApplyVolumes()
    {
        float musicVol = musicSlider != null ? musicSlider.value : 0.8f;
        float sfxVol   = sfxSlider != null ? sfxSlider.value : 0.8f;

        AudioSource[] allSources = FindObjectsOfType<AudioSource>();
        foreach (AudioSource source in allSources)
        {
            if (source != null)
            {
                if (source.gameObject.CompareTag("Music"))
                {
                    source.volume = musicVol;
                }
                else
                {
                    source.volume = sfxVol;
                }
            }
        }
    }

    // Вызывается при изменении слайдера музыки (привяжите в инспекторе!)
    public void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicVolKey, value);
        ApplyVolumes();
    }

    // Вызывается при изменении слайдера звуков
    public void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFXVolKey, value);
        ApplyVolumes();
    }

    // Кнопка Play / Start
    public void PlayOsnova()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f;
        SceneManager.LoadScene("cameratest2");
    }

    // Кнопка Quit
    public void Quit()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Application.Quit();
    }

    // Переключение панели настроек
    public void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }
    }

    // Кнопка закрытия внутри панели
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
}