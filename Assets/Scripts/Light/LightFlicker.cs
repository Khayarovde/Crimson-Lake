using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    public Light lightSource;

    [Header("Flicker Settings")]
    public float minFlickerSpeed = 0.05f;    // минимальная задержка между морганиями
    public float maxFlickerSpeed = 0.2f;     // максимальная задержка между морганиями

    public float minFlickerDuration = 0.3f;  // минимальная длительность периода мигания
    public float maxFlickerDuration = 1.0f;  // максимальная длительность периода мигания

    public float minOffTime = 1.0f;          // минимальная пауза перед следующим морганием
    public float maxOffTime = 3.0f;          // максимальная пауза перед следующим морганием

    private float timer;
    private bool isFlickering;
    private float currentFlickerSpeed;
    private float currentFlickerDuration;
    private float currentOffTime;

    void Start()
    {
        if (lightSource == null)
            lightSource = GetComponent<Light>();

        // сразу выбираем случайные значения
        PickRandomTimes();
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (isFlickering)
        {
            // Включаем / выключаем свет случайно
            if (timer <= 0)
            {
                lightSource.enabled = !lightSource.enabled;
                timer = Random.Range(minFlickerSpeed, maxFlickerSpeed);
                currentFlickerDuration -= Time.deltaTime;

                if (currentFlickerDuration <= 0)
                {
                    // Завершили фазу мигания
                    isFlickering = false;
                    lightSource.enabled = true;
                    timer = currentOffTime;
                }
            }
        }
        else
        {
            if (timer <= 0)
            {
                // Начинаем новую фазу мигания
                PickRandomTimes();
                isFlickering = true;
            }
        }
    }

    void PickRandomTimes()
    {
        currentFlickerSpeed = Random.Range(minFlickerSpeed, maxFlickerSpeed);
        currentFlickerDuration = Random.Range(minFlickerDuration, maxFlickerDuration);
        currentOffTime = Random.Range(minOffTime, maxOffTime);
        timer = currentFlickerSpeed;
    }
}
