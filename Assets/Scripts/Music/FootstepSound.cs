using UnityEngine;

public class FootstepSound : MonoBehaviour
{
    [Header("Sound Settings")]
    public AudioClip[] footstepClips;
    
    [Header("Step Timing for Normal Walking")]
    public float stepsPerMinute = 110f; // Нормальный темп ходьбы человека
    public float stepIntervalVariance = 0.1f; // Небольшая вариация между шагами
    
    [Header("Speed Detection")]
    public float walkSpeedThreshold = 0.1f; // Минимальная скорость для воспроизведения шагов
    public float normalWalkSpeed = 1.4f; // Нормальная скорость ходьбы человека (1.4 м/с = 5 км/ч)
    
    [Header("Sound Variation")]
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;
    public float minVolume = 0.6f;
    public float maxVolume = 0.9f;
    
    [Header("Ground Detection")]
    public LayerMask groundMask = 1;
    public float groundCheckDistance = 0.2f;
    
    private AudioSource audioSource;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private float stepTimer;
    private bool isGrounded;
    private Vector3 lastPosition;
    private float currentSpeed;
    private float baseStepInterval;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        
        // Автоматическое создание AudioSource если отсутствует
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            SetupAudioSource();
        }
        
        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing!");
            enabled = false;
            return;
        }
        
        if (capsuleCollider == null)
        {
            Debug.LogError("CapsuleCollider component is missing!");
            enabled = false;
            return;
        }

        // Рассчитываем базовый интервал между шагами на основе темпа ходьбы
        baseStepInterval = 60f / stepsPerMinute;
        
        lastPosition = transform.position;
        
        Debug.Log($"Footstep system initialized: {stepsPerMinute} steps/min = {baseStepInterval:F2}s interval");
    }

    private void SetupAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D звук
        audioSource.volume = 0.7f;
        audioSource.maxDistance = 15f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    void Update()
    {
        CheckGrounded();
        CalculateSpeed();
        HandleFootsteps();
    }

    private void CheckGrounded()
    {
        // Проверка земли через Raycast от нижней точки коллайдера
        Vector3 spherePosition = transform.position + Vector3.up * (capsuleCollider.radius - groundCheckDistance);
        isGrounded = Physics.CheckSphere(spherePosition, capsuleCollider.radius * 0.9f, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void CalculateSpeed()
    {
        // Вычисляем скорость на основе изменения позиции
        Vector3 positionChange = transform.position - lastPosition;
        positionChange.y = 0; // Игнорируем вертикальное движение
        
        currentSpeed = positionChange.magnitude / Time.deltaTime;
        lastPosition = transform.position;
    }

    private void HandleFootsteps()
    {
        if (!isGrounded) 
        {
            stepTimer = 0;
            return;
        }

        // Проверяем, движется ли персонаж с достаточной скоростью
        bool isMoving = currentSpeed > walkSpeedThreshold;

        if (isMoving)
        {
            // Небольшая вариация интервала для естественности
            float variedStepInterval = baseStepInterval + Random.Range(-stepIntervalVariance, stepIntervalVariance);
            
            stepTimer += Time.deltaTime;

            if (stepTimer >= variedStepInterval)
            {
                PlayFootstepSound();
                stepTimer = 0;
            }
        }
        else
        {
            // Сбрасываем таймер при остановке
            stepTimer = baseStepInterval * 0.5f;
        }
    }

    private void PlayFootstepSound()
    {
        if (footstepClips == null || footstepClips.Length == 0)
        {
            Debug.LogWarning("No footstep clips assigned!");
            return;
        }

        // Выбираем случайный звук
        int randomIndex = Random.Range(0, footstepClips.Length);
        AudioClip selectedClip = footstepClips[randomIndex];
        
        if (selectedClip == null)
        {
            Debug.LogWarning("One of footstep clips is null!");
            return;
        }

        // Настраиваем параметры звука
        float volume = Random.Range(minVolume, maxVolume);
        float pitch = Random.Range(minPitch, maxPitch);
        
        audioSource.volume = volume;
        audioSource.pitch = pitch;

        audioSource.PlayOneShot(selectedClip);
        
        Debug.Log($"Footstep played: {selectedClip.name}, Speed: {currentSpeed:F2} m/s");
    }

    // Визуализация области проверки земли в редакторе
    private void OnDrawGizmosSelected()
    {
        if (capsuleCollider != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 spherePosition = transform.position + Vector3.up * (capsuleCollider.radius - groundCheckDistance);
            Gizmos.DrawWireSphere(spherePosition, capsuleCollider.radius * 0.9f);
        }
    }
    
    // Метод для принудительного воспроизведения шага (можно вызвать из анимации)
    public void PlayFootstep()
    {
        if (isGrounded)
        {
            PlayFootstepSound();
        }
    }
    
    // Метод для настройки темпа ходьбы в реальном времени
    public void SetWalkTempo(float newStepsPerMinute)
    {
        stepsPerMinute = newStepsPerMinute;
        baseStepInterval = 60f / stepsPerMinute;
    }
}