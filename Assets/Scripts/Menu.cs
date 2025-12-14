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

    [Header("Источники звука (опционально, если нужно автоматически найти)")]
    [SerializeField] private AudioSource musicSource; // ← Перетащите AudioSource с фоновой музыкой (если есть)
    [SerializeField] private AudioSource[] sfxSources; // ← Все AudioSource с эффектами (можно оставить пустым)

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

        musicSlider.value = musicVol;
        sfxSlider.value   = sfxVol;

        ApplyVolumes();
    }

    // Применяем громкость ко всем источникам
    private void ApplyVolumes()
    {
        // Музыка
        if (musicSource != null)
        {
            musicSource.volume = musicSlider.value;
        }

        // Все звуковые эффекты
        foreach (AudioSource sfx in sfxSources)
        {
            if (sfx != null)
            {
                sfx.volume = sfxSlider.value;
            }
        }

        // Дополнительно: если какие-то эффекты создаются во время игры (например, стрельба, взрывы),
        // можно в их скриптах добавить: audioSource.volume = PlayerPrefs.GetFloat("SFXVol");
    }

    // Вызывается при изменении слайдера музыки
    public void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicVolKey, value);
        if (musicSource != null)
        {
            musicSource.volume = value;
        }
    }

    // Вызывается при изменении слайдера звуков
    public void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFXVolKey, value);

        foreach (AudioSource sfx in sfxSources)
        {
            if (sfx != null)
            {
                sfx.volume = value;
            }
        }
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