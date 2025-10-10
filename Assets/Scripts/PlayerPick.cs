using UnityEngine;

public class PlayerPick : MonoBehaviour
{
    public Inventory inventory;                 // Инвентарь игрока
    public KeyCode pickupKey = KeyCode.E;      // Клавиша для подъёма предмета
    public LayerMask interactibleLayers;       // Маска слоёв предметов, которые можно подобрать

    private GameObject currentInteractiveItem; // Текущий предмет, доступный для взаимодействия

    private void Update()
    {
        if (currentInteractiveItem != null && Input.GetKeyDown(pickupKey))
        {
            var pickableItem = currentInteractiveItem.GetComponent<PickableItem>();
            if (pickableItem != null)
            {
                Debug.Log($"Поднят предмет '{pickableItem.itemData.name}'.");
                inventory.PickUp(pickableItem.itemData); // Добавляем предмет в инвентарь
                Destroy(currentInteractiveItem);         // Уничтожаем предмет на сцене
                currentInteractiveItem = null;           // Сбрасываем текущий предмет
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((interactibleLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            currentInteractiveItem = other.gameObject; // Получаем текущий предмет
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == currentInteractiveItem)
        {
            currentInteractiveItem = null; // Сбрасываем текущий предмет при уходе из зоны
        }
    }
}