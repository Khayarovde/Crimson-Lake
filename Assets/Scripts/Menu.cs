using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    [Header("Панель настроек")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Play Flow")]
    [SerializeField] private MenuSaveSlotsLauncher playFlowLauncher;

    [Header("Слайдеры звука")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private const string MusicVolKey = "MusicVol";
    private const string SFXVolKey   = "SFXVol";

    void Start()
    {
        SettingsManager.GetOrCreate();
        LoadSettings();
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
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
}