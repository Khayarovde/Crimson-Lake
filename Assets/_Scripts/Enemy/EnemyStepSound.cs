using UnityEngine;

public class EnemyStepSound : MonoBehaviour
{
    private static readonly AudioClip[] EmptyClips = System.Array.Empty<AudioClip>();

    [System.Serializable]
    public struct SurfaceStepSounds
    {
        public string surfaceTag;
        public AudioClip[] clips;
        public float baseVolume;
        public float minPitch;
        public float maxPitch;
    }

    [Header("Surface Clips")]
    public SurfaceStepSounds[] surfaceClips;
    public LayerMask surfaceMask = ~0;
    public float surfaceCheckDistance = 1.5f;
    public float surfaceCheckStartOffset = 0.2f;

    [Header("Surface Cache")]
    public float surfaceCacheInterval = 0.3f;

    [Header("Occlusion")]
    public Transform player;
    public LayerMask wallMask;
    public float occlusionCacheInterval = 0.4f;

    [Header("Movement Gate")]
    public bool requireMovement = true;
    public float moveSpeedThreshold = 0.05f;

    [Header("Debug")]
    [Tooltip("Включить логирование для отладки (выключено в релизе)")]
    public bool enableDebug = false;

    private AudioSource audioSource;      // основной AudioSource врага (не трогаем)
    private AudioSource stepAudioSource;  // отдельный AudioSource только для шагов
    private Rigidbody rb;
    private Vector3 lastPosition;
    private float estimatedPlanarSpeed;
    private float lastSurfaceCheckTime = -Mathf.Infinity;
    private float lastOcclusionCheckTime = -Mathf.Infinity;
    private AudioClip[] cachedSurfaceClips = EmptyClips;
    private SurfaceStepSounds cachedSurfaceSettings;
    private bool cachedIsOccluded;
    private System.Collections.Generic.Dictionary<string, SurfaceStepSounds> surfaceLookup;
    private int surfaceClipsLength;

    private void Start()
    {
        // Ищем основной AudioSource врага
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = GetComponentInChildren<AudioSource>();

        // Создаём отдельный AudioSource для шагов, чтобы не мешать другим звукам
        GameObject stepSourceObj = new GameObject("StepAudioSource");
        stepSourceObj.transform.SetParent(transform, false);
        stepAudioSource = stepSourceObj.AddComponent<AudioSource>();
        stepAudioSource.spatialBlend = audioSource != null ? audioSource.spatialBlend : 1f;
        stepAudioSource.outputAudioMixerGroup = audioSource != null ? audioSource.outputAudioMixerGroup : null;
        stepAudioSource.rolloffMode = audioSource != null ? audioSource.rolloffMode : AudioRolloffMode.Logarithmic;
        stepAudioSource.minDistance = audioSource != null ? audioSource.minDistance : 1f;
        stepAudioSource.maxDistance = audioSource != null ? audioSource.maxDistance : 20f;
        stepAudioSource.playOnAwake = false;

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();

        lastPosition = transform.position;

        if (audioSource == null && enableDebug)
            Debug.LogWarning("EnemyStepSound: основной AudioSource не найден, 3D-настройки шагов будут по умолчанию.", this);

        if (requireMovement && rb == null && enableDebug)
            Debug.LogWarning("EnemyStepSound: Rigidbody не найден. Будет использоваться оценка движения по смещению Transform.", this);

        // Построить словарь для быстрого поиска по тегу поверхности
        surfaceLookup = new System.Collections.Generic.Dictionary<string, SurfaceStepSounds>(8);
        if (surfaceClips != null)
        {
            surfaceClipsLength = surfaceClips.Length;
            for (int i = 0; i < surfaceClipsLength; i++)
            {
                var s = surfaceClips[i];
                if (!string.IsNullOrEmpty(s.surfaceTag) && !surfaceLookup.ContainsKey(s.surfaceTag))
                    surfaceLookup.Add(s.surfaceTag, s);
            }
        }
    }

    private void Update()
    {
        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - lastPosition;
        delta.y = 0f;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        estimatedPlanarSpeed = delta.magnitude / dt;
        lastPosition = currentPos;
    }

    public void PlayStepSound()
    {
        if (enableDebug)
            Debug.Log("EnemyStepSound: PlayStepSound вызван", this);

        if (requireMovement && !IsMoving())
        {
            if (enableDebug)
                Debug.Log("EnemyStepSound: шаг заблокирован, враг считается неподвижным", this);
            return;
        }

        if (IsOccludedByWall())
        {
            if (enableDebug)
                Debug.Log("EnemyStepSound: шаг заблокирован, игрок за стеной", this);
            return;
        }

        SurfaceStepSounds surfaceSettings;
        AudioClip[] clipsToPlay = GetSurfaceClips(out surfaceSettings);

        if (clipsToPlay.Length > 0)
        {
            AudioClip clip = clipsToPlay[Random.Range(0, clipsToPlay.Length)];
            // Питч и громкость применяются только к stepAudioSource — основной AudioSource не затрагивается
            stepAudioSource.pitch = Random.Range(surfaceSettings.minPitch, surfaceSettings.maxPitch);
            stepAudioSource.PlayOneShot(clip, surfaceSettings.baseVolume);
        }
    }

    public void Step_sound_play()
    {
        PlayStepSound();
    }

    public void PlayFootstep()
    {
        PlayStepSound();
    }

    private bool IsMoving()
    {
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            Vector3 velocity = rb.linearVelocity;
#else
            Vector3 velocity = rb.velocity;
#endif
            velocity.y = 0f;
            if (velocity.sqrMagnitude > moveSpeedThreshold * moveSpeedThreshold)
                return true;
        }

        return estimatedPlanarSpeed > moveSpeedThreshold;
    }

    private AudioClip[] GetSurfaceClips(out SurfaceStepSounds settings)
    {
        if (Time.time - lastSurfaceCheckTime <= surfaceCacheInterval)
        {
            settings = cachedSurfaceSettings;
            return cachedSurfaceClips;
        }

        lastSurfaceCheckTime = Time.time;
        settings = default;
        cachedSurfaceSettings = default;
        cachedSurfaceClips = EmptyClips;

        Vector3 origin = transform.position + Vector3.up * surfaceCheckStartOffset;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, surfaceCheckDistance, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            if (enableDebug)
                Debug.Log("EnemyStepSound: surface hit = " + hit.collider.name + ", tag = " + hit.collider.tag, this);

            if (surfaceLookup != null)
            {
                if (surfaceLookup.TryGetValue(hit.collider.tag, out SurfaceStepSounds entry) && entry.clips != null && entry.clips.Length > 0)
                {
                    settings = entry;
                    cachedSurfaceSettings = settings;
                    cachedSurfaceClips = entry.clips;
                    return cachedSurfaceClips;
                }
            }
            else
            {
                for (int i = 0; i < surfaceClips.Length; i++)
                {
                    SurfaceStepSounds entry = surfaceClips[i];
                    if (hit.collider.CompareTag(entry.surfaceTag) && entry.clips != null && entry.clips.Length > 0)
                    {
                        settings = entry;
                        cachedSurfaceSettings = settings;
                        cachedSurfaceClips = entry.clips;
                        return cachedSurfaceClips;
                    }
                }
            }
        }

        return EmptyClips;
    }

    private bool IsOccludedByWall()
    {
        if (player == null)
            return false;

        if (Time.time - lastOcclusionCheckTime <= occlusionCacheInterval)
            return cachedIsOccluded;

        lastOcclusionCheckTime = Time.time;

        Vector3 origin = transform.position + Vector3.up;
        Vector3 toPlayer = player.position - origin;
        float distance = toPlayer.magnitude;

        if (distance <= 0.0001f)
        {
            cachedIsOccluded = false;
            return false;
        }

        Vector3 direction = toPlayer / distance;
        cachedIsOccluded = Physics.Raycast(origin, direction, distance, wallMask, QueryTriggerInteraction.Ignore);
        return cachedIsOccluded;
    }
}