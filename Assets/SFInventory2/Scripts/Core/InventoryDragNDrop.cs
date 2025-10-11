using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Parity.SFInventory2.Core
{
    public class InventoryDragNDrop : MonoBehaviour
    {
        [SerializeField] private CellsCallbacksController callbacksController;
        [SerializeField] private Image dragIcon;

        private InventoryCell draggingCell;

        private void OnEnable()
        {
            callbacksController.onBeginDrag += OnStartDrag;
            callbacksController.onDrag += OnDrag;
            callbacksController.onDrop += OnDrop;
            callbacksController.onEndDrag += OnEndDrag;
        }

        private void OnDisable()
        {
            callbacksController.onBeginDrag -= OnStartDrag;
            callbacksController.onDrag -= OnDrag;
            callbacksController.onDrop -= OnDrop;
            callbacksController.onEndDrag -= OnEndDrag;
        }

        private void OnStartDrag(InventoryCell cell, PointerEventData data)
        {
            if (cell.Item == null) return;

            draggingCell = cell;
            dragIcon.sprite = cell.Icon.sprite;
            dragIcon.gameObject.SetActive(true);
            dragIcon.transform.position = cell.Icon.transform.position;
        }

        private void OnDrag(InventoryCell cell, PointerEventData data)
        {
            dragIcon.transform.position = data.position;
        }

        private void OnDrop(InventoryCell targetCell, PointerEventData data)
        {
            if (draggingCell == null || targetCell == null || draggingCell == targetCell) return;

            if (!targetCell.CanBeDropped(draggingCell)) return;

            if (data.button == PointerEventData.InputButton.Middle)
            {
                HandleMiddleMouseDrop(targetCell, draggingCell);
            }
            else
            {
                HandleRegularDrop(targetCell, draggingCell);
            }

            targetCell.UpdateCellUI();
            draggingCell.UpdateCellUI();
        }

        private void HandleMiddleMouseDrop(InventoryCell target, InventoryCell source)
        {
            if (target.Item == null)
            {
                SplitItems(target, source);
            }
            else if (target.Item == source.Item)
            {
                MoveHalfItems(target, source);
            }
        }

        private void HandleRegularDrop(InventoryCell target, InventoryCell source)
        {
            if (target.Item != source.Item)
            {
                SwapCells(target, source);
            }
            else
            {
                MoveAllItems(target, source);
            }
        }

        private void SplitItems(InventoryCell target, InventoryCell source)
        {
            int half = source.ItemsCount / 2;
            if (half > 0)
            {
                target.SetInventoryItem(source.Item);
                target.ItemsCount = half;
                source.ItemsCount = source.ItemsCount - half;
            }
        }

        private void MoveHalfItems(InventoryCell target, InventoryCell source)
        {
            MoveItemsCount(target, source, source.ItemsCount / 2);
        }

        private void MoveAllItems(InventoryCell target, InventoryCell source)
        {
            MoveItemsCount(target, source, source.ItemsCount);
        }

        private void MoveItemsCount(InventoryCell target, InventoryCell source, int count)
        {
            if (count <= 0) return;

            int availableInTarget = target.Item == null ? 0 : target.Item.maxItemsCount - target.ItemsCount;

            if (availableInTarget >= count)
            {
                target.ItemsCount = target.ItemsCount + count;
                source.ItemsCount = source.ItemsCount - count;
                if (source.ItemsCount <= 0) source.SetInventoryItem(null);
            }
            else if (availableInTarget > 0)
            {
                target.ItemsCount = target.ItemsCount + availableInTarget;
                source.ItemsCount = source.ItemsCount - availableInTarget;
            }
        }

        private void SwapCells(InventoryCell cellA, InventoryCell cellB)
        {
            var tempItem = cellA.Item;
            var tempCount = cellA.ItemsCount;

            cellA.SetInventoryItem(cellB.Item);
            cellA.ItemsCount = cellB.ItemsCount;

            cellB.SetInventoryItem(tempItem);
            cellB.ItemsCount = tempCount;
        }

        private void OnEndDrag(InventoryCell cell, PointerEventData data)
        {
            draggingCell = null;
            dragIcon.gameObject.SetActive(false);
        }
    }
}