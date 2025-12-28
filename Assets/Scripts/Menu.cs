using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menu : MonoBehaviour
{
    [Header("Панель настроек")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Слайдеры звука")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private const string MusicVolKey = "MusicVol";
    private const string SFXVolKey   = "SFXVol";

    void Start()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        float musicVol = PlayerPrefs.GetFloat(MusicVolKey, 0.8f);
        float sfxVol   = PlayerPrefs.GetFloat(SFXVolKey, 0.8f);

        if (musicSlider != null) musicSlider.value = musicVol;
        if (sfxSlider != null) sfxSlider.value = sfxVol;

        ApplyVolumes();
    }

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
                    source.volume = musicVol;
                else
                    source.volume = sfxVol;
            }
        }
    }

    public void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicVolKey, value);
        ApplyVolumes();
    }

    public void OnSFXChanged(float value)
    {
        PlayerPrefs.SetFloat(SFXVolKey, value);
        ApplyVolumes();
    }

    public void PlayOsnova()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f;
        SceneManager.LoadScene("cameratest2");
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