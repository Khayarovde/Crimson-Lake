using Parity.SFInventory2.Core;
using UnityEngine;
namespace Parity.SFInventory2.Core.Commands
{
    [CreateAssetMenu(fileName = "NewCommand", menuName = "Inventory/Commands/New Command")]
    public class SFInventoryCommand : ScriptableObject, ISFInventoryCommand
    {
        [SerializeField] private string _commandName = "Use";

        public virtual string Name => _commandName;
    }
}