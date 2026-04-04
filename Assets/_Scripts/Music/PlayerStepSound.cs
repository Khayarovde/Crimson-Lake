using System.Collections;
using UnityEngine;

public class PlayerStepSound : MonoBehaviour
{
    private static readonly AudioClip[] EmptyClips = System.Array.Empty<AudioClip>();

    [System.Serializable]
    public struct SurfaceStepSounds
    {
        public string surfaceTag;
        public AudioClip[] clips;
        [Header("Soft Playback")]
        public float fadeInDuration;
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
    private AudioSource audioSource;
    private Rigidbody rb;

    [Header("Movement Gate")]
    public bool requireMovement = true;
    public float moveSpeedThreshold = 0.05f;
    public bool useInputFallback = true;
    public float inputThreshold = 0.1f;
    [Tooltip("Выводить в консоль факт вызова Step_sound_play и причины блокировки")]
    public bool logStepEvents = false;
    private Coroutine fadeRoutine;
    private Vector3 lastPosition;
    private float estimatedPlanarSpeed;


    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = GetComponentInChildren<AudioSource>();

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();

        lastPosition = transform.position;

        if (audioSource == null)
            Debug.LogWarning("PlayerStepSound: AudioSource не найден на объекте/детях.", this);

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

        if (clipsToPlay.Length > 0 && audioSource != null)
        {
            AudioClip clip = clipsToPlay[Random.Range(0, clipsToPlay.Length)];
            audioSource.pitch = Random.Range(surfaceSettings.minPitch, surfaceSettings.maxPitch);
            audioSource.clip = clip;
            audioSource.volume = 0f;
            audioSource.Play();

            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeInAudio(surfaceSettings.baseVolume, surfaceSettings.fadeInDuration));

            // Debug.Log("Событие шага сработало"); // Для отладки: смотрите в консоль Unity
            // print("Звук шага"); // Ваш оригинальный print
        }
        else
        {
            Debug.LogWarning("Нет звуков в массиве или AudioSource не найден!");
        }
    }

    private bool IsMoving()
    {
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            Vector3 v = rb.linearVelocity;
#else
            Vector3 v = rb.velocity;
#endif
            v.y = 0f;
            if (v.sqrMagnitude > moveSpeedThreshold * moveSpeedThreshold)
                return true;
        }

        if (estimatedPlanarSpeed > moveSpeedThreshold)
            return true;

        if (useInputFallback)
        {
            float vertical = Input.GetAxisRaw("Vertical");
            float horizontal = Input.GetAxisRaw("Horizontal");
            return Mathf.Abs(vertical) > inputThreshold || Mathf.Abs(horizontal) > inputThreshold;
        }

        return false;
    }

    private AudioClip[] GetSurfaceClips(out SurfaceStepSounds settings)
    {
        settings = default;
        // Raycast вниз, чтобы понять, какая поверхность под ногами
        Vector3 origin = transform.position + Vector3.up * surfaceCheckStartOffset;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, surfaceCheckDistance, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            string tag = hit.collider.tag;
            if (logSurfaceHit)
                Debug.Log("Surface hit: " + hit.collider.name + " tag=" + tag);
            for (int i = 0; i < surfaceClips.Length; i++)
            {
                if (surfaceClips[i].surfaceTag == tag && surfaceClips[i].clips != null)
                {
                    settings = surfaceClips[i];
                    return surfaceClips[i].clips;
                }
            }
        }

        return EmptyClips;
    }

    private IEnumerator FadeInAudio(float targetVolume, float duration)
    {
        if (duration <= 0f)
        {
            audioSource.volume = targetVolume;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, t / duration);
            yield return null;
        }

        audioSource.volume = targetVolume;
    }
}