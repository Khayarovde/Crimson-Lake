using Parity.SFInventory2.Core;
using UnityEngine;

namespace Parity.SFInventory2.Custom
{
    //just an override of base item
    [CreateAssetMenu(fileName = "ArmorItem", menuName = "ScriptableObjects/InventoryItems/ArmorItem", order = 2)]
    public class ArmorItem : InventoryItem
    {
        public float defensePoints = 5;
    }
}