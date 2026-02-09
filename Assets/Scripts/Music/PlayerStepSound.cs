using UnityEngine;

public class PlayerStepSound : MonoBehaviour
{
    public AudioClip[] stepSounds_AR; // массив звуков текущий
    private AudioSource audioSource;
    private Rigidbody rb;

    [Header("Movement Gate")]
    public bool requireMovement = true;
    public float moveSpeedThreshold = 0.05f;
    public bool useInputFallback = true;
    public float inputThreshold = 0.1f;


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

        if (stepSounds_AR.Length > 0 && audioSource != null)
        {
            audioSource.PlayOneShot(stepSounds_AR[Random.Range(0, stepSounds_AR.Length)]);
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
}