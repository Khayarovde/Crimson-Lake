using Parity.SFInventory2.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Parity.SFInventory2.Core.Commands
{
    public class SFInventoryCommandRouter : MonoBehaviour
    {
        public static SFInventoryCommandRouter Instance { get; private set; }

        private Dictionary<Type, Action<InventoryCell, object>> _handlers = new();

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        public void RegisterHandler<T>(Action<InventoryCell, object> handler) where T : ISFInventoryCommand
        {
            _handlers[typeof(T)] = handler;
        }

        public void ExecuteCommand(Type commandType, InventoryCell cell, object context = null)
        {
            if (_handlers.TryGetValue(commandType, out var handler))
            {
                handler(cell, context);
            }
            else
            {
                Debug.LogWarning($"No handler registered for {commandType}");
            }
        }
    }
}