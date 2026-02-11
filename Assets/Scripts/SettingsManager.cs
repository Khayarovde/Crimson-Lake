using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    public static SettingsManager GetOrCreate()
    {
        if (Instance != null) return Instance;

        var existing = FindObjectOfType<SettingsManager>();
        if (existing != null)
            return existing;

        var go = new GameObject(nameof(SettingsManager));
        return go.AddComponent<SettingsManager>();
    }

    private const string MusicKey = "MusicVol";
    private const string SFXKey   = "SFXVol";

    private float musicVolume = 0.8f;
    private float sfxVolume   = 0.8f;

    // Ссылки на слайдеры из разных сцен (заполняются вручную в Start каждой сцены)
    private Slider menuMusicSlider;
    private Slider menuSfxSlider;
    private Slider pauseMusicSlider;
    private Slider pauseSfxSlider;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadVolumes();
            ApplyVolumes();

            // Подписываемся на загрузку сцен
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void LoadVolumes()
    {
        musicVolume = PlayerPrefs.GetFloat(MusicKey, 0.8f);
        sfxVolume   = PlayerPrefs.GetFloat(SFXKey, 0.8f);
    }

    public void RegisterMenuSliders(Slider musicSlider, Slider sfxSlider)
    {
        menuMusicSlider = musicSlider;
        menuSfxSlider   = sfxSlider;
        UpdateSliderValues(menuMusicSlider, menuSfxSlider);
    }

    public void RegisterPauseSliders(Slider musicSlider, Slider sfxSlider)
    {
        pauseMusicSlider = musicSlider;
        pauseSfxSlider   = sfxSlider;
        UpdateSliderValues(pauseMusicSlider, pauseSfxSlider);
    }

    private void UpdateSliderValues(Slider musicSlider, Slider sfxSlider)
    {
        if (musicSlider != null) musicSlider.value = musicVolume;
        if (sfxSlider   != null)   sfxSlider.value   = sfxVolume;
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = value;
        PlayerPrefs.SetFloat(MusicKey, value);
        PlayerPrefs.Save();
        ApplyVolumes();
        MusicZoneTrigger.RefreshAllZoneVolumes();
        UpdateAllSliders();
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = value;
        PlayerPrefs.SetFloat(SFXKey, value);
        PlayerPrefs.Save();
        ApplyVolumes();
        UpdateAllSliders();
    }

    private void UpdateAllSliders()
    {
        UpdateSliderValues(menuMusicSlider, menuSfxSlider);
        UpdateSliderValues(pauseMusicSlider, pauseSfxSlider);
    }

    public void ApplyVolumes()
    {
        AudioSource[] sources = FindObjectsOfType<AudioSource>();
        foreach (var source in sources)
        {
            if (source == null) continue;

            // Музыка из MusicZoneTrigger управляется отдельно (там есть zoneVolume и fade)
            if (source.GetComponentInParent<MusicZoneTrigger>() != null)
                continue;

            if (source.gameObject.CompareTag("Music"))
                source.volume = musicVolume;
            else
                source.volume = sfxVolume;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyVolumes(); // Важно: переприменяем звук в новой сцене
        MusicZoneTrigger.RefreshAllZoneVolumes();
    }

    // Для получения текущих значений (если нужно)
    public float GetMusicVolume() => musicVolume;
    public float GetSFXVolume()   => sfxVolume;
}