using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class MusicZoneTrigger : MonoBehaviour
{
    private const float PlayerInsideGraceSeconds = 0.2f;
    private static readonly System.Collections.Generic.List<MusicZoneTrigger> Zones = new System.Collections.Generic.List<MusicZoneTrigger>(64);

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

    private static MusicZoneTrigger currentActiveZone;

    private float currentLocalVolume = 0f; // 0..zoneVolume
    private int playerContactsInTrigger;
    private float lastEnterTime;
    private float lastPlayerTouchTime;
    private bool isFadingBecausePlayerOutsideZones;

    private void Awake()
    {
        // Создаём отдельный AudioSource для этой зоны
        if (zoneAudioSource == null)
        {
            GameObject audioObj = new GameObject("ZoneMusic_" + gameObject.name);
            audioObj.transform.parent = transform;
            zoneAudioSource = audioObj.AddComponent<AudioSource>();
        }
        zoneAudioSource.playOnAwake = false;
        zoneAudioSource.loop = loopMusic;
        currentLocalVolume = 0f;
        playerContactsInTrigger = 0;
        lastEnterTime = -1f;
        lastPlayerTouchTime = -999f;
        isFadingBecausePlayerOutsideZones = false;
        ApplyCurrentVolume();
    }

    private void OnEnable()
    {
        if (!Zones.Contains(this))
            Zones.Add(this);
    }

    private void Update()
    {
        // Музыка должна играть только пока игрок внутри текущей музыкальной зоны.
        if (currentActiveZone != this)
            return;

        if (isFadingBecausePlayerOutsideZones)
            return;

        if (IsPlayerInsideThisZone())
            return;

        playerContactsInTrigger = 0;

        MusicZoneTrigger fallbackZone = FindBestZonePlayerIsIn(this);
        if (fallbackZone != null)
        {
            fallbackZone.ActivateThisZoneMusic();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(FadeOutActiveZoneBecausePlayerOutside());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerContactsInTrigger++;
        if (playerContactsInTrigger > 1) return;
        lastEnterTime = Time.unscaledTime;
        lastPlayerTouchTime = Time.unscaledTime;

        if (ShouldMuteBecauseSilentZoneIsActive())
        {
            FadeOutAllZoneMusicForSilentZone();
            return;
        }

        ActivateThisZoneMusic();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        lastPlayerTouchTime = Time.unscaledTime;

        // Защита на случай, если OnTriggerEnter был пропущен физикой/порядком кадров.
        if (playerContactsInTrigger <= 0)
            playerContactsInTrigger = 1;

        if (ShouldMuteBecauseSilentZoneIsActive())
        {
            FadeOutAllZoneMusicForSilentZone();
            return;
        }

        if (currentActiveZone != this)
            ActivateThisZoneMusic();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerContactsInTrigger = Mathf.Max(0, playerContactsInTrigger - 1);
        if (playerContactsInTrigger == 0)
            lastPlayerTouchTime = Time.unscaledTime - PlayerInsideGraceSeconds - 0.01f;

        if (playerContactsInTrigger > 0) return;

        // Выход из неактивной зоны не должен влиять на текущую музыку.
        if (currentActiveZone != this)
            return;

        MusicZoneTrigger fallbackZone = FindBestZonePlayerIsIn(this);
        if (fallbackZone != null)
        {
            fallbackZone.ActivateThisZoneMusic();
            return;
        }

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

        currentActiveZone = null;
    }

    private bool IsPlayableZone()
    {
        return playOnEnter && zoneMusic != null;
    }

    private bool ShouldMuteBecauseSilentZoneIsActive()
    {
        return !IsPlayableZone() || IsAnySilentZoneContainingPlayer();
    }

    private void ActivateThisZoneMusic()
    {
        if (!IsPlayableZone())
            return;

        // Уже активная и правильная музыка играет — ничего не делаем.
        if (currentActiveZone == this && zoneAudioSource != null && zoneAudioSource.isPlaying && zoneAudioSource.clip == zoneMusic)
            return;

        bool switchedFromAnotherZone = currentActiveZone != null && currentActiveZone != this;

        // Останавливаем все другие зоны
        StopAllOtherZoneMusic();

        // Запускаем свою музыку с fade in
        zoneAudioSource.Stop();
        zoneAudioSource.clip = zoneMusic;
        zoneAudioSource.loop = loopMusic;
        StopAllCoroutines();
        zoneAudioSource.Play();

        // При смене зоны переключаемся сразу, чтобы не оставался старый трек.
        if (switchedFromAnotherZone)
        {
            currentLocalVolume = zoneVolume;
            ApplyCurrentVolume();
        }
        else
        {
            currentLocalVolume = 0f;
            ApplyCurrentVolume();
            StartCoroutine(FadeIn(zoneVolume, fadeInTime));
        }

        currentActiveZone = this;
        isFadingBecausePlayerOutsideZones = false;

        NotifyEnemyScriptsPlayerEnteredZone();
    }

    private void NotifyEnemyScriptsPlayerEnteredZone()
    {
        // Уведомляем скрипты подбора дискеты о смене музыкальной зоны.
        EnemyPickupInteraction[] disketteScripts = FindObjectsByType<EnemyPickupInteraction>(FindObjectsSortMode.InstanceID);
        foreach (var diskette in disketteScripts)
        {
            diskette.OnPlayerEnteredNewZone(this);
        }
    }

    private static MusicZoneTrigger FindBestZonePlayerIsIn(MusicZoneTrigger excludeZone)
    {
        if (IsAnySilentZoneContainingPlayer())
            return null;
        MusicZoneTrigger bestZone = null;
        float latestEnterTime = float.MinValue;

        foreach (var zone in Zones)
        {
            if (zone == null || zone == excludeZone)
                continue;

            if (!zone.IsPlayerInsideThisZone())
                continue;

            if (!zone.IsPlayableZone())
                continue;

            if (!zone.isActiveAndEnabled)
                continue;

            if (zone.lastEnterTime > latestEnterTime)
            {
                latestEnterTime = zone.lastEnterTime;
                bestZone = zone;
            }
        }

        return bestZone;
    }

    private bool IsPlayerInsideThisZone()
    {
        return playerContactsInTrigger > 0 || (Time.unscaledTime - lastPlayerTouchTime) <= PlayerInsideGraceSeconds;
    }

    private static bool IsAnySilentZoneContainingPlayer()
    {
        foreach (var zone in Zones)
        {
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            if (zone.IsPlayableZone())
                continue;

            if (zone.IsPlayerInsideThisZone())
                return true;
        }

        return false;
    }

    private static void FadeOutAllZoneMusicForSilentZone()
    {
        foreach (var zone in Zones)
        {
            if (zone == null || zone.zoneAudioSource == null || !zone.zoneAudioSource.isPlaying)
                continue;

            zone.StopAllCoroutines();
            zone.StartCoroutine(zone.FadeOut(zone.fadeOutTime));
        }

        currentActiveZone = null;
    }

    private IEnumerator FadeOutActiveZoneBecausePlayerOutside()
    {
        isFadingBecausePlayerOutsideZones = true;
        yield return StartCoroutine(FadeOut(fadeOutTime));

        if (zoneAudioSource != null)
            zoneAudioSource.Stop();

        if (currentActiveZone == this)
            currentActiveZone = null;

        isFadingBecausePlayerOutsideZones = false;
    }

    public IEnumerator FadeIn(float targetVolume, float duration)
    {
        if (duration <= 0f)
        {
            currentLocalVolume = targetVolume;
            ApplyCurrentVolume();
            yield break;
        }

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
        if (duration <= 0f)
        {
            currentLocalVolume = 0f;
            ApplyCurrentVolume();
            yield break;
        }

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
        foreach (var z in Zones)
            z.ApplyCurrentVolume();
    }

    // Останавливаем музыку во всех других зонах
    private void StopAllOtherZoneMusic()
    {
        foreach (var zone in Zones)
        {
            if (zone != this && zone.zoneAudioSource != null && zone.zoneAudioSource.isPlaying)
            {
                zone.StopAllCoroutines();

                // Текущая ранее активная зона всегда останавливается сразу,
                // чтобы новая зона звучала немедленно.
                bool shouldStopImmediately = zone == currentActiveZone || !fadePreviousOnEnter;

                if (!shouldStopImmediately)
                {
                    zone.StartCoroutine(zone.FadeOut(fadeOutTime));
                }
                else
                {
                    zone.zoneAudioSource.Stop();
                    zone.currentLocalVolume = 0f;
                    zone.ApplyCurrentVolume();
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

    private void OnDisable()
    {
        Zones.Remove(this);
        if (currentActiveZone == this)
            currentActiveZone = null;
    }
}