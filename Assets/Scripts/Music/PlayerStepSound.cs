using System.Collections;
using UnityEngine;

public class PlayerStepSound : MonoBehaviour
{
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
    private Coroutine fadeRoutine;


    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();
        // Step_sound_play(); // Временный вызов для теста: звук проиграется при запуске сцены
    }

    public void Step_sound_play()
    {

        if (requireMovement && !IsMoving())
            return;

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

            Debug.Log("Событие шага сработало"); // Для отладки: смотрите в консоль Unity
            print("Звук шага"); // Ваш оригинальный print
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
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > moveSpeedThreshold * moveSpeedThreshold)
                return true;
        }

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

        return new AudioClip[0];
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