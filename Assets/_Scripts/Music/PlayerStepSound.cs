using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStepSound : MonoBehaviour
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
    public bool logSurfaceHit = false;
    [Header("Surface Cache")]
    public float surfaceCacheInterval = 0.3f;
    private AudioSource audioSource;
    private AudioSource stepAudioSource;
    private bool stepAudioSourceInitialized;
    private Rigidbody rb;

    [Header("Movement Gate")]
    public bool requireMovement = true;
    public float moveSpeedThreshold = 0.05f;
    [Tooltip("Небольшое окно времени, чтобы шаг не пропадал на каждом втором событии анимации")]
    public float movementGraceTime = 0.12f;
    public bool useInputFallback = true;
    public float inputThreshold = 0.1f;
    [Tooltip("Выводить в консоль факт вызова Step_sound_play и причины блокировки")]
    public bool logStepEvents = false;
    private Vector3 lastPosition;
    private float estimatedPlanarSpeed;
    private float lastMovingTime = -Mathf.Infinity;
    private float lastSurfaceCheckTime = -Mathf.Infinity;
    private AudioClip[] cachedSurfaceClips = EmptyClips;
    private SurfaceStepSounds cachedSurfaceSettings;


    private void Awake()
    {
        EnsureStepAudioSource();
    }

    private void Start()
    {
        EnsureStepAudioSource();

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();

        lastPosition = transform.position;

        if (audioSource == null)
            Debug.LogWarning("PlayerStepSound: основной AudioSource не найден, 3D-настройки шагов будут по умолчанию.", this);

        if (rb == null && requireMovement)
            Debug.LogWarning("PlayerStepSound: Rigidbody не найден на объекте/родителях. Будет использоваться оценка движения по смещению Transform и Input fallback.", this);

        // Step_sound_play(); // Временный вызов для теста: звук проиграется при запуске сцены
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

    public void Step_sound_play()
    {
        EnsureStepAudioSource();

        if (logStepEvents)
            Debug.Log("PlayerStepSound: Step_sound_play вызван", this);

        if (requireMovement && !IsMoving())
        {
            if (logStepEvents)
                Debug.Log("PlayerStepSound: шаг заблокирован, персонаж считается неподвижным", this);
            return;
        }

        SurfaceStepSounds surfaceSettings;
        AudioClip[] clipsToPlay = GetSurfaceClips(out surfaceSettings);

        if (clipsToPlay.Length > 0 && stepAudioSource != null)
        {
            AudioClip clip = clipsToPlay[Random.Range(0, clipsToPlay.Length)];
            stepAudioSource.pitch = Random.Range(surfaceSettings.minPitch, surfaceSettings.maxPitch);
            stepAudioSource.volume = 1f;
            stepAudioSource.PlayOneShot(clip, surfaceSettings.baseVolume);

            // Debug.Log("Событие шага сработало"); // Для отладки: смотрите в консоль Unity
            // print("Звук шага"); // Ваш оригинальный print
        }
        else
        {
//            Debug.LogWarning("Нет звуков в массиве или AudioSource не найден!");
        }
    }

    private bool IsMoving()
    {
        bool movingNow = false;

        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            Vector3 v = rb.linearVelocity;
#else
            Vector3 v = rb.velocity;
#endif
            v.y = 0f;
            if (v.sqrMagnitude > moveSpeedThreshold * moveSpeedThreshold)
                movingNow = true;
        }

        if (estimatedPlanarSpeed > moveSpeedThreshold)
            movingNow = true;

        if (useInputFallback)
        {
            // Legacy Input Manager fallback.
            float vertical = Input.GetAxisRaw("Vertical");
            float horizontal = Input.GetAxisRaw("Horizontal");

            if (Mathf.Abs(vertical) > inputThreshold || Mathf.Abs(horizontal) > inputThreshold)
                movingNow = true;

            // Input System fallback for gamepad/keyboard projects without old axes.
            if (!movingNow)
            {
                Vector2 stick = Vector2.zero;
                if (Gamepad.current != null)
                    stick = Gamepad.current.leftStick.ReadValue();

                if (stick.sqrMagnitude > inputThreshold * inputThreshold)
                    movingNow = true;
            }
        }

        if (movingNow)
        {
            lastMovingTime = Time.time;
            return true;
        }

        return Time.time - lastMovingTime <= movementGraceTime;
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
        // Raycast вниз, чтобы понять, какая поверхность под ногами
        Vector3 origin = transform.position + Vector3.up * surfaceCheckStartOffset;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, surfaceCheckDistance, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            if (logSurfaceHit)
//                Debug.Log("Surface hit: " + hit.collider.name + " tag=" + tag);
            for (int i = 0; i < surfaceClips.Length; i++)
            {
                if (hit.collider.CompareTag(surfaceClips[i].surfaceTag) && surfaceClips[i].clips != null)
                {
                    settings = surfaceClips[i];
                    cachedSurfaceSettings = settings;
                    cachedSurfaceClips = surfaceClips[i].clips;
                    return cachedSurfaceClips;
                }
            }
        }

        return EmptyClips;
    }

    private void EnsureStepAudioSource()
    {
        if (stepAudioSourceInitialized)
            return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = GetComponentInChildren<AudioSource>();
        }

        GameObject stepSourceObj = new GameObject("StepAudioSource");
        stepSourceObj.transform.SetParent(transform, false);
        stepAudioSource = stepSourceObj.AddComponent<AudioSource>();
        stepAudioSource.spatialBlend = audioSource != null ? audioSource.spatialBlend : 1f;
        stepAudioSource.outputAudioMixerGroup = audioSource != null ? audioSource.outputAudioMixerGroup : null;
        stepAudioSource.rolloffMode = audioSource != null ? audioSource.rolloffMode : AudioRolloffMode.Logarithmic;
        stepAudioSource.minDistance = audioSource != null ? audioSource.minDistance : 1f;
        stepAudioSource.maxDistance = audioSource != null ? audioSource.maxDistance : 20f;
        stepAudioSource.playOnAwake = false;
        stepAudioSource.loop = false;
        stepAudioSource.volume = 1f;

        stepAudioSourceInitialized = true;
    }
}