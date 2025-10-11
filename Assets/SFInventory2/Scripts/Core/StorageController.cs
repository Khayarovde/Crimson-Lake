using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Parity.SFInventory2.Core
{
    public class StorageController : ContainerBase
    {
        [SerializeField] private RectTransform _storage;
        [SerializeField] private TextMeshProUGUI _storageName;

        private Chest _currentChest;

        // ✅ Это свойство должно быть в вашем классе
        public Chest CurrentChest => _currentChest;

        private void Start()
        {
            _storage.gameObject.SetActive(false);
        }

        public void OpenStorage(Chest chest)
        {
            SaveToChest();
            _currentChest = chest;
            _storageName.text = _currentChest.ChestName;

            var cells = _currentChest.GetCells();

            if (cells != null)
            {
                _storage.gameObject.SetActive(true);

                if (cells.Count > 0)
                {
                    for (int i = 0; i < inventoryCells.Count; i++)
                    {
                        if (i < cells.Count)
                            inventoryCells[i].MigrateCell(cells[i]);
                    }
                }
                else
                {
                    foreach (var cell in inventoryCells)
                    {
                        cell.SetInventoryItem(null);
                    }
                }
            }

            foreach (var cell in inventoryCells)
            {
                cell.UpdateCellUI();
            }
        }

        public void CloseStorage()
        {
            SaveToChest();
            _currentChest = null;
            _storage.gameObject.SetActive(false);
        }

        public void SaveToChest()
        {
            if (_currentChest == null) return;

            _currentChest.SaveItems(inventoryCells.Select(s => new StorageItem
            {
                item = s.Item,
                itemsCount = s.ItemsCount
            }).ToList());
        }
    }
}