using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class MusicZoneTrigger : MonoBehaviour
{
    [System.Serializable]
    public class TrackEntry
    {
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;
    }

    private const float PlayerInsideGraceSeconds = 0.2f;
    private static readonly System.Collections.Generic.List<MusicZoneTrigger> Zones = new System.Collections.Generic.List<MusicZoneTrigger>(64);

    [Header("Музыка для этой зоны")]
    public AudioClip zoneMusic;             // Какой трек играть в этой зоне
    [Range(0f, 1f)]
    public float zoneVolume = 1f;           // Громкость в зоне

    [Header("Multi-track")]
    public bool useMultiTrack = false;
    public List<TrackEntry> tracks = new List<TrackEntry>();

    [Header("Поведение")]
    public bool loopMusic = true;           // Зацикливать музыку?
    public bool playOnEnter = true;         // Включать музыку при входе (обычно да)
    public bool stopOnExit = false;         // Остановить музыку полностью при выходе
    public bool fadePreviousOnEnter = true; // Плавно затухать предыдущую музыку

    [Header("Плавный переход")]
    public float fadeInTime = 1.5f;          // Время нарастания новой музыки
    public float fadeOutTime = 1.5f;        // Время затухания старой/при выходе

    public AudioSource zoneAudioSource;
    private readonly List<AudioSource> _trackSources = new List<AudioSource>();

    private static MusicZoneTrigger currentActiveZone;

    private float _fadeMult = 0f; // 0..1
    private int playerContactsInTrigger;
    private float lastEnterTime;
    private float lastPlayerTouchTime;
    private bool isFadingBecausePlayerOutsideZones;

    private void Awake()
    {
        if (useMultiTrack)
        {
            EnsureTrackSources();
        }
        else
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
        }

        ConfigureAudioSources();
        _fadeMult = 0f;
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
        if (!playOnEnter)
            return false;

        if (useMultiTrack)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                TrackEntry track = tracks[i];
                if (track != null && track.clip != null)
                    return true;
            }

            return false;
        }

        return zoneMusic != null;
    }

    private bool ShouldMuteBecauseSilentZoneIsActive()
    {
        return !IsPlayableZone() || IsAnySilentZoneContainingPlayer();
    }

    private void ActivateThisZoneMusic()
    {
        if (!IsPlayableZone())
            return;

        if (useMultiTrack)
        {
            if (currentActiveZone == this && AreMultiTrackSourcesAlreadyPlaying())
                return;

            bool switchedFromAnotherZone = currentActiveZone != null && currentActiveZone != this;

            StopAllOtherZoneMusic();
            StopCurrentAudioSources(false);

            double startTime = AudioSettings.dspTime + 0.1d;

            for (int i = 0; i < _trackSources.Count; i++)
            {
                AudioSource source = _trackSources[i];
                TrackEntry track = i < tracks.Count ? tracks[i] : null;

                if (source == null)
                    continue;

                source.Stop();
                source.clip = track != null ? track.clip : null;
                source.loop = loopMusic;

                if (source.clip != null)
                    source.PlayScheduled(startTime);
            }

            StopAllCoroutines();

            if (switchedFromAnotherZone)
            {
                _fadeMult = 1f;
                ApplyCurrentVolume();
            }
            else
            {
                _fadeMult = 0f;
                ApplyCurrentVolume();
                StartCoroutine(FadeIn(1f, fadeInTime));
            }

            currentActiveZone = this;
            isFadingBecausePlayerOutsideZones = false;

            NotifyEnemyScriptsPlayerEnteredZone();
            return;
        }

        // Уже активная и правильная музыка играет — ничего не делаем.
        if (currentActiveZone == this && zoneAudioSource != null && zoneAudioSource.isPlaying && zoneAudioSource.clip == zoneMusic)
            return;

        bool switchedFromAnotherZoneAfterStop = currentActiveZone != null && currentActiveZone != this;

        // Останавливаем все другие зоны
        StopAllOtherZoneMusic();

        // Запускаем свою музыку с fade in
        zoneAudioSource.Stop();
        zoneAudioSource.clip = zoneMusic;
        zoneAudioSource.loop = loopMusic;
        StopAllCoroutines();
        zoneAudioSource.Play();

        // При смене зоны переключаемся сразу, чтобы не оставался старый трек.
        if (switchedFromAnotherZoneAfterStop)
        {
            _fadeMult = 1f;
            ApplyCurrentVolume();
        }
        else
        {
            _fadeMult = 0f;
            ApplyCurrentVolume();
            StartCoroutine(FadeIn(1f, fadeInTime));
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
            if (zone == null || !zone.HasAnyAudioSourcePlaying())
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

        StopCurrentAudioSources(false);

        if (currentActiveZone == this)
            currentActiveZone = null;

        isFadingBecausePlayerOutsideZones = false;
    }

    public IEnumerator FadeIn(float targetFadeMult, float duration)
    {
        if (duration <= 0f)
        {
            _fadeMult = targetFadeMult;
            ApplyCurrentVolume();
            yield break;
        }

        float currentTime = 0f;
        float startLocal = _fadeMult;

        while (currentTime < duration)
        {
            currentTime += Time.unscaledDeltaTime;
            _fadeMult = Mathf.Lerp(startLocal, targetFadeMult, currentTime / duration);
            ApplyCurrentVolume();
            yield return null;
        }
        _fadeMult = targetFadeMult;
        ApplyCurrentVolume();
    }

    public IEnumerator FadeOut(float duration)
    {
        if (duration <= 0f)
        {
            _fadeMult = 0f;
            ApplyCurrentVolume();
            yield break;
        }

        float currentTime = 0f;
        float startLocal = _fadeMult;

        while (currentTime < duration)
        {
            currentTime += Time.unscaledDeltaTime;
            _fadeMult = Mathf.Lerp(startLocal, 0f, currentTime / duration);
            ApplyCurrentVolume();
            yield return null;
        }
        _fadeMult = 0f;
        ApplyCurrentVolume();
    }

    private IEnumerator FadeOutAndStop()
    {
        yield return StartCoroutine(FadeOut(fadeOutTime));
        StopCurrentAudioSources(true);
    }

    private void ApplyCurrentVolume()
    {
        float globalMusicVol = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetMusicVolume()
            : PlayerPrefs.GetFloat("MusicVol", 0.8f);

        if (useMultiTrack)
        {
            int count = Mathf.Min(_trackSources.Count, tracks.Count);
            for (int i = 0; i < count; i++)
            {
                AudioSource source = _trackSources[i];
                TrackEntry track = tracks[i];

                if (source == null)
                    continue;

                float trackVolume = track != null ? track.volume : 1f;
                source.volume = Mathf.Clamp01(trackVolume) * Mathf.Clamp01(globalMusicVol) * Mathf.Clamp01(_fadeMult);
            }

            return;
        }

        if (zoneAudioSource == null)
            return;

        zoneAudioSource.volume = Mathf.Clamp01(zoneVolume) * Mathf.Clamp01(globalMusicVol) * Mathf.Clamp01(_fadeMult);
    }

    public static void RefreshAllZoneVolumes()
    {
        foreach (var z in Zones)
        {
            if (z != null)
                z.ApplyCurrentVolume();
        }
    }

    // Останавливаем музыку во всех других зонах
    private void StopAllOtherZoneMusic()
    {
        foreach (var zone in Zones)
        {
            if (zone == null || zone == this || !zone.HasAnyAudioSourcePlaying())
                continue;

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
                zone.StopCurrentAudioSources(false);
                zone._fadeMult = 0f;
                zone.ApplyCurrentVolume();
            }
        }
    }

    private void EnsureTrackSources()
    {
        for (int i = 0; i < tracks.Count; i++)
        {
            if (i < _trackSources.Count && _trackSources[i] != null)
                continue;

            GameObject trackObj = new GameObject("ZoneTrack_" + i + "_" + gameObject.name);
            trackObj.transform.SetParent(transform, false);

            AudioSource trackSource = trackObj.AddComponent<AudioSource>();
            trackSource.playOnAwake = false;
            trackSource.loop = loopMusic;

            if (i < _trackSources.Count)
                _trackSources[i] = trackSource;
            else
                _trackSources.Add(trackSource);
        }

        for (int i = _trackSources.Count - 1; i >= tracks.Count; i--)
        {
            if (_trackSources[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(_trackSources[i].gameObject);
                else
                    DestroyImmediate(_trackSources[i].gameObject);
            }

            _trackSources.RemoveAt(i);
        }
    }

    private void ConfigureAudioSources()
    {
        if (useMultiTrack)
        {
            for (int i = 0; i < _trackSources.Count; i++)
            {
                if (_trackSources[i] != null)
                {
                    _trackSources[i].playOnAwake = false;
                    _trackSources[i].loop = loopMusic;
                }
            }

            return;
        }

        if (zoneAudioSource != null)
        {
            zoneAudioSource.playOnAwake = false;
            zoneAudioSource.loop = loopMusic;
        }
    }

    private bool HasAnyAudioSourcePlaying()
    {
        if (useMultiTrack)
        {
            for (int i = 0; i < _trackSources.Count; i++)
            {
                AudioSource source = _trackSources[i];
                if (source != null && source.isPlaying)
                    return true;
            }

            return false;
        }

        return zoneAudioSource != null && zoneAudioSource.isPlaying;
    }

    private bool AreMultiTrackSourcesAlreadyPlaying()
    {
        if (!useMultiTrack)
            return false;

        if (_trackSources.Count == 0)
            return false;

        int count = Mathf.Min(_trackSources.Count, tracks.Count);
        for (int i = 0; i < count; i++)
        {
            AudioSource source = _trackSources[i];
            TrackEntry track = tracks[i];

            if (source == null || track == null || track.clip == null)
                continue;

            if (!source.isPlaying || source.clip != track.clip)
                return false;
        }

        return true;
    }

    private void StopCurrentAudioSources(bool clearClip)
    {
        if (useMultiTrack)
        {
            for (int i = 0; i < _trackSources.Count; i++)
            {
                AudioSource source = _trackSources[i];
                if (source == null)
                    continue;

                source.Stop();
                if (clearClip)
                    source.clip = null;
            }

            return;
        }

        if (zoneAudioSource == null)
            return;

        zoneAudioSource.Stop();
        if (clearClip)
            zoneAudioSource.clip = null;
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

        for (int i = 0; i < _trackSources.Count; i++)
        {
            if (_trackSources[i] != null)
                _trackSources[i].loop = loopMusic;
        }
    }

    private void OnDisable()
    {
        Zones.Remove(this);
        if (currentActiveZone == this)
            currentActiveZone = null;
    }
}