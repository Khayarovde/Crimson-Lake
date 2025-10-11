using Parity.SFInventory2.Core;
using Parity.SFInventory2.Core.Commands;
using Parity.SFInventory2.Demo.Commands;
using System;
using UnityEngine;

namespace Parity.SFInventory2.Demo.Player
{
    public class ItemDrop : MonoBehaviour
    {
        private void Start()
        {
            SFInventoryCommandRouter.Instance.RegisterHandler<DropCommand>(HandleDrop);
        }

        private void HandleDrop(InventoryCell cell, object arg2)
        {
            Debug.LogError($"{cell.Item.itemName} dropped");
            cell.ItemsCount = 0;
            cell.SetInventoryItem(null);
            cell.UpdateCellUI();
        }
    }
}
