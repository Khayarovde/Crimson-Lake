using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ObjectActivator : MonoBehaviour
{
    [Header("Взаимодействие")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionDistance = 3f;

    [Header("UI подсказка")]
    [SerializeField] private GameObject interactionUI;
    [SerializeField] private string interactionText = "Нажмите E, чтобы активировать";

    [Header("Звук активации")]
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Предметы, которые исчезнут НАВСЕГДА")]
    [SerializeField] private GameObject[] objectsToDisappear;

    [Header("Свет, который выключится на 5 секунд")]
    [SerializeField] private Light[] lightsToTurnOff;

    // Хронометраж событий
    [Header("Тайминги")]
    [SerializeField] private float delayBeforeLightsOff = 0.5f;   // Задержка от звука до выключения света
    [SerializeField] private float delayBeforeObjectsDisappear = 1.0f; // Задержка от выключения света до исчезновения предметов
    [SerializeField] private float lightsOffDuration = 5f;        // Сколько секунд свет выключен

    // Для хранения исходных состояний света
    private Color[] originalColors;
    private float[] originalIntensities;
    private bool[] originalEnabled;

    private bool alreadyActivated = false;
    private bool playerInRange = false;
    private Transform player;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        SaveOriginalLightStates();

        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
            Text uiText = interactionUI.GetComponent<Text>();
            if (uiText != null)
                uiText.text = interactionText;
        }
    }

    private void Update()
    {
        if (player == null || alreadyActivated) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= interactionDistance)
        {
            if (!playerInRange)
            {
                playerInRange = true;
                ShowPrompt(true);
            }

            if (Input.GetKeyDown(interactKey))
            {
                Activate();
            }
        }
        else
        {
            if (playerInRange)
            {
                playerInRange = false;
                ShowPrompt(false);
            }
        }
    }

    private void ShowPrompt(bool show)
    {
        if (interactionUI != null)
            interactionUI.SetActive(show);
    }

    private void SaveOriginalLightStates()
    {
        if (lightsToTurnOff == null || lightsToTurnOff.Length == 0) return;

        originalColors = new Color[lightsToTurnOff.Length];
        originalIntensities = new float[lightsToTurnOff.Length];
        originalEnabled = new bool[lightsToTurnOff.Length];

        for (int i = 0; i < lightsToTurnOff.Length; i++)
        {
            if (lightsToTurnOff[i] != null)
            {
                originalColors[i] = lightsToTurnOff[i].color;
                originalIntensities[i] = lightsToTurnOff[i].intensity;
                originalEnabled[i] = lightsToTurnOff[i].enabled;
            }
        }
    }

    private void Activate()
    {
        if (alreadyActivated) return;
        alreadyActivated = true;

        ShowPrompt(false);
        StartCoroutine(ActivationSequence());
    }

    private IEnumerator ActivationSequence()
    {
        // 1. Проигрываем звук активации СНАЧАЛА
        if (activationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSound);
        }

        // 2. Ждём немного — даём звуку "зайти"
        yield return new WaitForSeconds(delayBeforeLightsOff);

        // 3. Выключаем свет
        foreach (Light light in lightsToTurnOff)
        {
            if (light != null)
            {
                light.enabled = false;
                // Альтернатива: light.intensity = 0f; если хочешь плавное угасание — сделаем по запросу
            }
        }

        Debug.Log("Свет выключен после звука.");

        // 4. Ждём ещё — напряжение в темноте
        yield return new WaitForSeconds(delayBeforeObjectsDisappear);

        // 5. Исчезают предметы
        foreach (GameObject obj in objectsToDisappear)
        {
            if (obj != null)
            {
                obj.SetActive(false); // Или Destroy(obj);
            }
        }

        Debug.Log("Предметы исчезли в темноте.");

        // 6. Ждём основное время темноты
        yield return new WaitForSeconds(lightsOffDuration);

        // 7. Включаем свет обратно
        for (int i = 0; i < lightsToTurnOff.Length; i++)
        {
            Light light = lightsToTurnOff[i];
            if (light != null)
            {
                light.enabled = originalEnabled[i];
                light.color = originalColors[i];
                light.intensity = originalIntensities[i];
            }
        }

        Debug.Log("Свет вернулся. Активация завершена.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}