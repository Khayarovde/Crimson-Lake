using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class Inventory : MonoBehaviour
{
    public List<Item> items = new List<Item>(); // Список предметов
    public Image[] slots;                       // Массив изображений слотов (UI Image)
    public GameObject inventoryPanel;           // Панель инвентаря (UI)
    public KeyCode toggleInventoryKey = KeyCode.I; // Клавиша для открытия инвентаря

    private bool isOpen = false; // Флаг открытого состояния инвентаря

    private void Start()
    {
        inventoryPanel.SetActive(false); // Скрываем инвентарь по умолчанию
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleInventoryKey))
        {
            Debug.Log("Клавиша I нажата");
            ToggleInventory();
        }
    }

    // Метод для добавления предмета в инвентарь
    public void PickUp(Item item)
    {
        items.Add(item);                        // Добавляем предмет в список
        RefreshSlots();                         // Обновляем визуальное отображение слотов
    }

    // Метод обновления отображения слотов
    public void RefreshSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < items.Count)
            {
                if (slots[i] != null)
                {
                    slots[i].sprite = items[i].sprite; // Показываем спрайт предмета
                    slots[i].enabled = true;           // Активируем слот
                }
            }
            else
            {
                if (slots[i] != null)
                {
                    slots[i].enabled = false;          // Деактивируем неиспользуемые слоты
                }
            }
        }
    }

    // Возможность удаления предмета по индексу
    public void RemoveItemAtIndex(int index)
    {
        if (index >= 0 && index < items.Count)
        {
            items.RemoveAt(index);
            RefreshSlots();
        }
    }

    // Метод для переключения состояния инвентаря
    public void ToggleInventory()
    {
        isOpen = !isOpen; // Переключаем состояние
        Debug.Log("Состояние инвентаря: " + (isOpen ? "открыт" : "закрыт"));

        inventoryPanel.SetActive(isOpen); // Показываем или скрываем панель
    }
}