using UnityEngine;

public class PosterLight : MonoBehaviour
{
public Light lightSource;

    [Header("Flicker Settings")]
    public float minFlickerSpeed = 0.05f;     // минимальное время между изменениями интенсивности
    public float maxFlickerSpeed = 0.15f;     // максимальное время между изменениями
    public float minFlickerDuration = 0.5f;   // минимальная длительность фазы мигания
    public float maxFlickerDuration = 1.5f;   // максимальная длительность фазы мигания
    public float minOffTime = 1.0f;           // минимальная пауза перед новым миганием
    public float maxOffTime = 3.0f;           // максимальная пауза перед новым миганием
    public float maxIntensity = 1.5f;         // максимальная яркость
    public float fadeSpeed = 5f;              // скорость плавного перехода между яркостями

    private float timer;
    private bool isFlickering;
    private float targetIntensity;
    private float currentFlickerDuration;
    private float currentOffTime;
    private float flickerTimer;

    void Start()
    {
        if (lightSource == null)
            lightSource = GetComponent<Light>();

        PickRandomTimes();
        targetIntensity = maxIntensity;
        lightSource.intensity = maxIntensity;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (isFlickering)
        {
            flickerTimer -= Time.deltaTime;

            // Когда время пришло — задаём новую случайную яркость
            if (flickerTimer <= 0)
            {
                targetIntensity = Random.Range(0f, maxIntensity);
                flickerTimer = Random.Range(minFlickerSpeed, maxFlickerSpeed);
            }

            // Плавное приближение к цели
            lightSource.intensity = Mathf.Lerp(lightSource.intensity, targetIntensity, Time.deltaTime * fadeSpeed);

            currentFlickerDuration -= Time.deltaTime;
            if (currentFlickerDuration <= 0)
            {
                // Завершаем фазу мигания
                isFlickering = false;
                targetIntensity = maxIntensity;
                timer = currentOffTime;
            }
        }
        else
        {
            // фаза покоя
            lightSource.intensity = Mathf.Lerp(lightSource.intensity, maxIntensity, Time.deltaTime * fadeSpeed);

            if (timer <= 0)
            {
                // Начинаем новый цикл
                PickRandomTimes();
                isFlickering = true;
            }
        }
    }

    void PickRandomTimes()
    {
        currentFlickerDuration = Random.Range(minFlickerDuration, maxFlickerDuration);
        currentOffTime = Random.Range(minOffTime, maxOffTime);
        flickerTimer = Random.Range(minFlickerSpeed, maxFlickerSpeed);
        timer = 0;
    }
}

