using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public InventoryItem item; // Ссылка на ScriptableObject предмета
    public float interactionDistance = 2f; // Дистанция для подбора
    private bool isPlayerNearby = false;
    private Transform player;

    private void Start()
    {
        if (item == null)
            Debug.LogError($"[ItemPickup] Предмет не назначен на объекте {gameObject.name}!");
        if (!gameObject.GetComponent<Collider>())
            Debug.LogError($"[ItemPickup] Коллайдер отсутствует на объекте {gameObject.name}!");
        if (!gameObject.GetComponent<Collider>().isTrigger)
            Debug.LogWarning($"[ItemPickup] Коллайдер на объекте {gameObject.name} не установлен как триггер!");
    }

    private void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"[ItemPickup] Нажата клавиша E для объекта {gameObject.name}");
            TryPickup();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[ItemPickup] Игрок вошёл в триггер объекта {gameObject.name}");
            isPlayerNearby = true;
            player = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[ItemPickup] Игрок покинул триггер объекта {gameObject.name}");
            isPlayerNearby = false;
            player = null;
        }
    }

    private void TryPickup()
    {
        if (player != null && Vector3.Distance(transform.position, player.position) <= interactionDistance)
        {
            Debug.Log($"[ItemPickup] Проверка расстояния пройдена для объекта {gameObject.name}");
            PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
            if (playerInventory != null)
            {
                Debug.Log($"[ItemPickup] Найден PlayerInventory на игроке для объекта {gameObject.name}");
                bool added = playerInventory.AddItemToInventory(item);
                if (added)
                {
                    Debug.Log($"[ItemPickup] Предмет {item.itemName} успешно подобран!");
                    Destroy(gameObject);
                }
                else
                {
                    Debug.LogWarning($"[ItemPickup] Не удалось добавить предмет {item.itemName} (возможно, инвентарь полон)");
                }
            }
            else
            {
                Debug.LogError($"[ItemPickup] PlayerInventory не найден на игроке для объекта {gameObject.name}!");
            }
        }
        else
        {
            Debug.LogWarning($"[ItemPickup] Игрок слишком далеко или не обнаружен для объекта {gameObject.name}!");
        }
    }
}