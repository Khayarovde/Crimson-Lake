using UnityEngine;
using UnityEngine.InputSystem;

public class ItemPickup : MonoBehaviour
{
    public InventoryItem item; // Ссылка на ScriptableObject предмета
    public float interactionDistance = 2f; // Дистанция для подбора
    [Header("Save")]
    [PickupId]
    [SerializeField] private string pickupId;
    private bool isPlayerNearby = false;
    private Transform player;
    private PlayerInventory playerInventory;

    private void Start()
    {
        if (!string.IsNullOrWhiteSpace(pickupId) && SaveManager.HasPickedUpItem(pickupId))
        {
            Destroy(gameObject);
            return;
        }

        if (item == null)
            Debug.LogError($"[ItemPickup] Предмет не назначен на объекте {gameObject.name}!");
        if (!gameObject.GetComponent<Collider>())
            Debug.LogError($"[ItemPickup] Коллайдер отсутствует на объекте {gameObject.name}!");
        if (!gameObject.GetComponent<Collider>().isTrigger)
            Debug.LogWarning($"[ItemPickup] Коллайдер на объекте {gameObject.name} не установлен как триггер!");
    }

    private void Update()
    {
        if (isPlayerNearby && (Input.GetKeyDown(KeyCode.E) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)))
        {
            TryPickup();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Debug.Log($"[ItemPickup] Игрок вошёл в триггер объекта {gameObject.name}");
            isPlayerNearby = true;
            player = other.transform;
            playerInventory = other.GetComponentInParent<PlayerInventory>();
            if (playerInventory == null)
            {
                playerInventory = other.GetComponentInChildren<PlayerInventory>();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Debug.Log($"[ItemPickup] Игрок покинул триггер объекта {gameObject.name}");
            isPlayerNearby = false;
            player = null;
            playerInventory = null;
        }
    }

    public bool TryPickupFromGamepad()
    {
        return TryPickup();
    }

    private bool TryPickup()
    {
        if (item == null)
        {
            Debug.LogError($"[ItemPickup] Предмет не назначен на объекте {gameObject.name}!");
            return false;
        }

        if (player != null && Vector3.Distance(transform.position, player.position) <= interactionDistance)
        {
            // Debug.Log($"[ItemPickup] Проверка расстояния пройдена для объекта {gameObject.name}");
            if (playerInventory != null)
            {
                // Debug.Log($"[ItemPickup] Найден PlayerInventory на игроке для объекта {gameObject.name}");
                bool added = playerInventory.AddItemToInventory(item);
                if (added)
                {
                    if (!string.IsNullOrWhiteSpace(pickupId))
                        SaveManager.MarkItemPickedUp(pickupId);
                    // Debug.Log($"[ItemPickup] Предмет {item.itemName} успешно подобран!");
                    Destroy(gameObject);
                    return true;
                }

                Debug.LogWarning($"[ItemPickup] Не удалось добавить предмет {item.itemName} (возможно, инвентарь полон)");
            }
            else
            {
                Debug.LogError($"[ItemPickup] PlayerInventory не найден в иерархии игрока для объекта {gameObject.name}!");
            }
        }
        else
        {
            Debug.LogWarning($"[ItemPickup] Игрок слишком далеко или не обнаружен для объекта {gameObject.name}!");
        }

        return false;
    }
}