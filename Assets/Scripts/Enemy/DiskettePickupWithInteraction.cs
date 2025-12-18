using UnityEngine;
using UnityEngine.UI;

public class DiskettePickupWithInteraction : MonoBehaviour
{
    [Header("Настройки подбора")]
    [SerializeField] private GameObject pickupEffect;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Взаимодействие")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionDistance = 3f;

    [Header("UI подсказка (опционально)")]
    [SerializeField] private GameObject interactionUI;
    [SerializeField] private string interactionText = "Нажмите E, чтобы взять дискету";

    [Header("Враг (ИЗНАЧАЛЬНО АКТИВЕН И ВИДЕН)")]
    [SerializeField] private AdvancedEnemyAI enemyAI; // ← Перетащи объект врага с компонентом AdvancedEnemyAI

    [SerializeField] private Transform enemySpawnPoint; // Опционально: телепорт врага в эту точку при активации охоты

    [Header("Освещение (меняет цвет на красный при активации охоты)")]
    [SerializeField] private Light[] lightsToChangeColor;

    private bool alreadyPickedUp = false;
    private bool playerInRange = false;
    private Transform player;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Подсказка
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
        if (player == null || alreadyPickedUp) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= interactionDistance)
        {
            if (!playerInRange)
            {
                playerInRange = true;
                ShowInteractionPrompt(true);
            }

            if (Input.GetKeyDown(interactKey))
            {
                PickupDiskette();
                ActivateEnemyChase();
            }
        }
        else
        {
            if (playerInRange)
            {
                playerInRange = false;
                ShowInteractionPrompt(false);
            }
        }
    }

    private void ShowInteractionPrompt(bool show)
    {
        if (interactionUI != null)
            interactionUI.SetActive(show);
    }

    private void PickupDiskette()
    {
        alreadyPickedUp = true;

        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        if (pickupSound != null && audioSource != null)
            audioSource.PlayOneShot(pickupSound);

        ShowInteractionPrompt(false);
        gameObject.SetActive(false); // Дискета/монитор исчезает
    }

    private void ActivateEnemyChase()
{
    if (enemyAI == null)
    {
        Debug.LogError("Enemy AI не назначен в DiskettePickupWithInteraction!");
        return;
    }

    // Опционально: телепортируем врага в точку спауна
    if (enemySpawnPoint != null)
    {
        enemyAI.TeleportToPosition(enemySpawnPoint.position);
        enemyAI.transform.rotation = enemySpawnPoint.rotation;
    }

    // Запускаем преследование
    enemyAI.StartChasingAfterDiskette();

    // Меняем освещение на красное
    foreach (Light light in lightsToChangeColor)
    {
        if (light != null)
        {
            light.color = Color.red;
        }
    }

    Debug.Log("Дискета взята → враг начал охоту, освещение стало красным!");
}

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);

        if (enemySpawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(enemySpawnPoint.position, Vector3.one * 0.5f);
        }
    }
}