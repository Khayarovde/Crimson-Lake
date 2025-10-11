using Parity.SFInventory2.Core.Commands;
using System.Collections.Generic;
using UnityEngine;

namespace Parity.SFInventory2.Core
{
    [CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/InventoryItems/Item")]
    public class InventoryItem : ScriptableObject
    {
        public Sprite icon;
        public string itemName;
        public string itemDescription;
        public int maxItemsCount = 8;

        // Change from strings to actual commands
        public List<SFInventoryCommand> availableCommands = new List<SFInventoryCommand>();
    }
}