using Parity.SFInventory2.Core;
using UnityEngine;

namespace Parity.SFInventory2.Custom
{
    //just an override of base item
    [CreateAssetMenu(fileName = "FoodItem", menuName = "ScriptableObjects/InventoryItems/FoodItem", order = 1)]
    public class FoodItem : InventoryItem
    {
        public float healAmount = 25;
    }
}