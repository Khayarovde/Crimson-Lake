using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class MusicZoneTrigger : MonoBehaviour
{
    [Header("Музыка для этой зоны")]
    public AudioClip zoneMusic;             // Какой трек играть в этой зоне
    [Range(0f, 1f)]
    public float zoneVolume = 1f;           // Громкость в зоне

    [Header("Поведение")]
    public bool loopMusic = true;           // Зацикливать музыку?
    public bool playOnEnter = true;         // Включать музыку при входе (обычно да)
    public bool stopOnExit = false;         // Остановить музыку полностью при выходе
    public bool fadePreviousOnEnter = true; // Плавно затухать предыдущую музыку

    [Header("Плавный переход")]
    public float fadeInTime = 1.5f;          // Время нарастания новой музыки
    public float fadeOutTime = 1.5f;        // Время затухания старой/при выходе

    public AudioSource zoneAudioSource;

    private float currentLocalVolume = 0f; // 0..zoneVolume

    private void Awake()
    {
        // Создаём отдельный AudioSource для этой зоны
        GameObject audioObj = new GameObject("ZoneMusic_" + gameObject.name);
        audioObj.transform.parent = transform;
        zoneAudioSource = audioObj.AddComponent<AudioSource>();
        zoneAudioSource.playOnAwake = false;
        zoneAudioSource.loop = loopMusic;
        currentLocalVolume = 0f;
        ApplyCurrentVolume();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!playOnEnter || zoneMusic == null) return;
        if (!other.CompareTag("Player")) return;

        // Останавливаем все другие зоны
        StopAllOtherZoneMusic();

        // Запускаем свою музыку с fade in
        zoneAudioSource.clip = zoneMusic;
        currentLocalVolume = 0f;
        ApplyCurrentVolume();
        zoneAudioSource.loop = loopMusic;
        zoneAudioSource.Play();

        StopAllCoroutines();
        StartCoroutine(FadeIn(zoneVolume, fadeInTime));

        // ← ВАЖНО: Уведомляем все активные скрипты подбора дискеты,
        // что игрок вошёл в новую зону (это нужно для остановки chase-музыки)
        DiskettePickupWithInteraction[] disketteScripts = FindObjectsByType<DiskettePickupWithInteraction>(FindObjectsSortMode.InstanceID);
        foreach (var diskette in disketteScripts)
        {
            diskette.OnPlayerEnteredNewZone(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (stopOnExit)
        {
            // Полностью останавливаем музыку этой зоны
            StopAllCoroutines();
            StartCoroutine(FadeOutAndStop());
        }
        else
        {
            // Просто плавно затухаем эту зону (другая зона сама включится при входе)
            StopAllCoroutines();
            StartCoroutine(FadeOut(fadeOutTime));
        }
    }

    public IEnumerator FadeIn(float targetVolume, float duration)
    {
        float currentTime = 0f;
        float startLocal = currentLocalVolume;

        while (currentTime < duration)
        {
            currentTime += Time.unscaledDeltaTime;
            currentLocalVolume = Mathf.Lerp(startLocal, targetVolume, currentTime / duration);
            ApplyCurrentVolume();
            yield return null;
        }
        currentLocalVolume = targetVolume;
        ApplyCurrentVolume();
    }

    public IEnumerator FadeOut(float duration)
    {
        float currentTime = 0f;
        float startLocal = currentLocalVolume;

        while (currentTime < duration)
        {
            currentTime += Time.unscaledDeltaTime;
            currentLocalVolume = Mathf.Lerp(startLocal, 0f, currentTime / duration);
            ApplyCurrentVolume();
            yield return null;
        }
        currentLocalVolume = 0f;
        ApplyCurrentVolume();
    }

    private IEnumerator FadeOutAndStop()
    {
        yield return StartCoroutine(FadeOut(fadeOutTime));
        zoneAudioSource.Stop();
        zoneAudioSource.clip = null;
    }

    private void ApplyCurrentVolume()
    {
        if (zoneAudioSource == null) return;

        float globalMusicVol = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetMusicVolume()
            : PlayerPrefs.GetFloat("MusicVol", 0.8f);

        zoneAudioSource.volume = Mathf.Clamp01(currentLocalVolume) * Mathf.Clamp01(globalMusicVol);
    }

    public static void RefreshAllZoneVolumes()
    {
        var zones = FindObjectsByType<MusicZoneTrigger>(FindObjectsSortMode.InstanceID);
        foreach (var z in zones)
            z.ApplyCurrentVolume();
    }

    // Останавливаем музыку во всех других зонах
    private void StopAllOtherZoneMusic()
    {
        MusicZoneTrigger[] allZones = FindObjectsByType<MusicZoneTrigger>(FindObjectsSortMode.InstanceID);
        foreach (var zone in allZones)
        {
            if (zone != this && zone.zoneAudioSource != null && zone.zoneAudioSource.isPlaying)
            {
                zone.StopAllCoroutines();
                if (fadePreviousOnEnter)
                {
                    zone.StartCoroutine(zone.FadeOut(fadeOutTime));
                }
                else
                {
                    zone.zoneAudioSource.Stop();
                }
            }
        }
    }

    // Для удобства в редакторе
    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        if (zoneAudioSource != null)
        {
            zoneAudioSource.loop = loopMusic;
        }
    }
}