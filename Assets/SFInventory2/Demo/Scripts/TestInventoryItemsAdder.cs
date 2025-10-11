using UnityEngine;
using TMPro;
using Parity.SFInventory2.Core;
using System.Collections.Generic;

namespace Parity.SFInventory2.Testing
{
    public class TestInventoryItemsAdder : MonoBehaviour
    {
        [SerializeField] private AddingItem[] addingItems;
        [SerializeField] private ContainerBase container;
        [SerializeField] private TextMeshProUGUI infoText;

        private Dictionary<KeyCode, InventoryItem> keyMap = new Dictionary<KeyCode, InventoryItem>();

        private void Start()
        {
            foreach (var entry in addingItems)
            {
                keyMap[entry.key] = entry.item;
                infoText.text += $"{entry.key} - {entry.item.itemName}\n";
            }
        }

        private void Update()
        {
            foreach (var key in keyMap.Keys)
            {
                if (Input.GetKeyDown(key))
                {
                    var item = keyMap[key];
                    container.AddItemsCount(item, item.maxItemsCount, out var left);
                    if (left > 0)
                        Debug.Log($"Not enough space! {left} items left!");
                }
            }
        }

        [System.Serializable]
        public class AddingItem
        {
            public InventoryItem item;
            public KeyCode key;
        }
    }
}