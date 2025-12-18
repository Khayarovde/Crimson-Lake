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

    private AudioSource zoneAudioSource;

    private void Awake()
    {
        // Создаём отдельный AudioSource для этой зоны
        GameObject audioObj = new GameObject("ZoneMusic_" + gameObject.name);
        audioObj.transform.parent = transform;
        zoneAudioSource = audioObj.AddComponent<AudioSource>();
        zoneAudioSource.playOnAwake = false;
        zoneAudioSource.loop = loopMusic;
        zoneAudioSource.volume = 0f; // Начинаем с нулевой громкости для fade in
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!playOnEnter || zoneMusic == null) return;
        if (!other.CompareTag("Player")) return;

        // Останавливаем все другие зоны (опционально можно сделать приоритет, но пока просто останавливаем все)
        StopAllOtherZoneMusic();

        // Запускаем свою музыку с fade in
        zoneAudioSource.clip = zoneMusic;
        zoneAudioSource.volume = 0f;
        zoneAudioSource.loop = loopMusic;
        zoneAudioSource.Play();

        StopAllCoroutines();
        StartCoroutine(FadeIn(zoneVolume, fadeInTime));
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

    private IEnumerator FadeIn(float targetVolume, float duration)
    {
        float currentTime = 0f;
        float startVolume = zoneAudioSource.volume;

        while (currentTime < duration)
        {
            currentTime += Time.unscaledDeltaTime;
            zoneAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, currentTime / duration);
            yield return null;
        }
        zoneAudioSource.volume = targetVolume;
    }

    private IEnumerator FadeOut(float duration)
    {
        float currentTime = 0f;
        float startVolume = zoneAudioSource.volume;

        while (currentTime < duration)
        {
            currentTime += Time.unscaledDeltaTime;
            zoneAudioSource.volume = Mathf.Lerp(startVolume, 0f, currentTime / duration);
            yield return null;
        }
        zoneAudioSource.volume = 0f;
        // Не останавливаем воспроизведение — другая зона может запуститься
    }

    private IEnumerator FadeOutAndStop()
    {
        yield return StartCoroutine(FadeOut(fadeOutTime));
        zoneAudioSource.Stop();
        zoneAudioSource.clip = null;
    }

    // Останавливаем музыку во всех других зонах
    private void StopAllOtherZoneMusic()
    {
        MusicZoneTrigger[] allZones = FindObjectsOfType<MusicZoneTrigger>();
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