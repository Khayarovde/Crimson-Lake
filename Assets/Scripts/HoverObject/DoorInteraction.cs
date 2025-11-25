using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Если используете TextMeshPro для текста

public class DoorInteraction : MonoBehaviour
{
    public GameObject interactionText; // Ссылка на GameObject с текстом (TextMeshPro или TextMesh)
    public float interactionDistance = 2f; // Расстояние, на котором показывается текст (если используете сферу, настройте коллайдер)

    private bool isPlayerNear = false;
    private Transform player;

    void Start()
    {
        if (interactionText != null)
        {
            interactionText.SetActive(false); // Изначально скрываем текст
        }
    }

    void Update()
    {
        
    }

    // Используйте триггер коллайдер на двери для обнаружения игрока
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = true;
            player = other.transform;
            if (interactionText != null)
            {
                interactionText.SetActive(true); // Показываем текст "Press E to transition"
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNear = false;
            player = null;
            if (interactionText != null)
            {
                interactionText.SetActive(false); // Скрываем текст
            }
        }
    }
}