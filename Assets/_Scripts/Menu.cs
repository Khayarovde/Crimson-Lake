using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    [Header("Панель настроек")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Кнопка настроек")]
    [SerializeField] private ButtonStateController settingsButtonState;

    [Header("Play Flow")]
    [SerializeField] private MenuSaveSlotsLauncher playFlowLauncher;

    [Header("Слайдеры звука")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private const string MusicVolKey = "MusicVol";
    private const string SFXVolKey   = "SFXVol";

    // Блокирует клики на один фрейм после закрытия панели
    private bool _ignoreNextClick = false;

    void Start()
    {
        SettingsManager.GetOrCreate();
        LoadSettings();
    }

    // Вызывай этот метод из EventSystem / кнопки-оверлея для закрытия по клику вне панели
    public void OnBackgroundClick()
    {
        if (_ignoreNextClick) return;
        CloseSettings();
    }

    private void LoadSettings()
    {
        float musicVol = PlayerPrefs.GetFloat(MusicVolKey, 0.8f);
        float sfxVol   = PlayerPrefs.GetFloat(SFXVolKey, 0.8f);

        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null) sfxSlider.value = sfxVol;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMusicVolume(musicVol);
            SettingsManager.Instance.SetSFXVolume(sfxVol);
        }
    }

    public void OnMusicChanged(float value)
    {
        SettingsManager.GetOrCreate().SetMusicVolume(value);
    }

    public void OnSFXChanged(float value)
    {
        SettingsManager.GetOrCreate().SetSFXVolume(value);
    }

    public void PlayOsnova()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f;

        if (playFlowLauncher != null)
        {
            playFlowLauncher.BeginPlayFlow();
            return;
        }

        SaveManager.GetOrCreate().LoadLatestSaveOrDefault("cameratest2");
    }

    public void Quit()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Application.Quit();
    }

    public void ToggleSettings()
    {
        if (settingsPanel == null) return;

        bool opening = !settingsPanel.activeSelf;
        settingsPanel.SetActive(opening);

        if (opening)
        {
            // Открываем — блокируем сквозной клик на один фрейм
            _ignoreNextClick = true;
            StartCoroutine(ClearIgnoreNextFrame());

            if (settingsButtonState != null)
                settingsButtonState.ForceNormal();
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // Блокируем сквозной клик — тот же PointerUp не долетит до кнопки
        _ignoreNextClick = true;
        StartCoroutine(ClearIgnoreNextFrame());

        if (settingsButtonState != null)
            settingsButtonState.ForceNormal();
    }

    private System.Collections.IEnumerator ClearIgnoreNextFrame()
    {
        // Ждём конца текущего фрейма — клик уже обработан, снимаем блок
        yield return new WaitForEndOfFrame();
        _ignoreNextClick = false;
    }
}