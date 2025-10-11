using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Parity.SFInventory2.Core
{
    public class ContainerBase : MonoBehaviour
    {
        public List<InventoryCell> inventoryCells = new List<InventoryCell>();
        [SerializeField] private CellsCallbacksController _callbacksController;

        public Action<InventoryCell, PointerEventData> onBeginDrag => _callbacksController.onBeginDrag;
        public Action<InventoryCell, PointerEventData> onDrag => _callbacksController.onDrag;
        public Action<InventoryCell, PointerEventData> onEndDrag => _callbacksController.onEndDrag;
        public Action<InventoryCell, PointerEventData> onDrop => _callbacksController.onDrop;
        public Action<InventoryCell, PointerEventData> onClick => _callbacksController.onClick;

        private void Awake()
        {
            foreach (var cell in inventoryCells)
            {
                cell.Init(this);
            }
        }

        public void AddInventoryCell(InventoryCell cell) => inventoryCells.Add(cell);

        public bool TryGetEmptyCell(out InventoryCell cell)
        {
            foreach (var c in inventoryCells)
            {
                if (c.Item == null)
                {
                    cell = c;
                    return true;
                }
            }
            cell = null;
            return false;
        }

        public bool TryGetCellWithFreeSpace(InventoryItem item, out InventoryCell cell)
        {
            foreach (var c in inventoryCells)
            {
                if (c.Item == item && c.ItemsCount < item.maxItemsCount)
                {
                    cell = c;
                    return true;
                }
            }
            cell = null;
            return false;
        }

        public void AddItemsCount(InventoryItem item, int count, out int remaining)
        {
            remaining = count;

            while (remaining > 0)
            {
                if (TryGetCellWithFreeSpace(item, out var cell))
                {
                    remaining = FillExistingCell(cell, item, remaining);
                }
                else if (TryGetEmptyCell(out cell))
                {
                    remaining = FillNewCell(cell, item, remaining);
                }
                else break;
            }
        }

        private int FillExistingCell(InventoryCell cell, InventoryItem item, int availableCount)
        {
            int spaceLeft = item.maxItemsCount - cell.ItemsCount;
            int amountToAdd = Mathf.Min(spaceLeft, availableCount);

            cell.ItemsCount = cell.ItemsCount + amountToAdd;
            cell.UpdateCellUI();

            return availableCount - amountToAdd;
        }

        private int FillNewCell(InventoryCell cell, InventoryItem item, int availableCount)
        {
            int amountToAdd = Mathf.Min(item.maxItemsCount, availableCount);

            cell.SetInventoryItem(item);
            cell.ItemsCount = amountToAdd;
            cell.UpdateCellUI();

            return availableCount - amountToAdd;
        }
    }
}