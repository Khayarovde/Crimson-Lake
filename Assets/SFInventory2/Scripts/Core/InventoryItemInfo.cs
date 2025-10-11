using Parity.SFInventory2.Core.Commands;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Parity.SFInventory2.Core
{
    public class InventoryItemInfo : MonoBehaviour
    {
        [SerializeField] private CellsCallbacksController _callbacksController;
        [SerializeField] private RectTransform _infoPanel;
        [SerializeField] private TextMeshProUGUI _itemName;
        [SerializeField] private TextMeshProUGUI _itemDescription;
        [SerializeField] private Image _icon;
        [SerializeField] private Transform _commandButtonsParent;
        [SerializeField] private Button _commandButtonPrefab;

        private InventoryCell _currentCell;

        private void Start()
        {
            _infoPanel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _callbacksController.onClick += OnClick;
            _callbacksController.onBeginDrag += OnBeginDrag;
        }

        private void OnDisable()
        {
            _callbacksController.onClick -= OnClick;
            _callbacksController.onBeginDrag -= OnBeginDrag;
        }

        private void OnClick(InventoryCell cell, PointerEventData eventData)
        {
            Logic(cell);

            if (cell?.Item != null)
            {
                ShowCommands(cell);
            }
        }

        private void OnBeginDrag(InventoryCell cell, PointerEventData eventData)
        {
            Logic(null);
        }

        private void Logic(InventoryCell cell)
        {
            _currentCell = cell;

            if (cell != null && cell.Item != null)
            {
                _infoPanel.gameObject.SetActive(true);
                _icon.sprite = cell.Item.icon;
                _itemName.text = cell.Item.itemName;
                _itemDescription.text = cell.Item.itemDescription;
            }
            else
            {
                _infoPanel.gameObject.SetActive(false);
            }
        }

        private void ShowCommands(InventoryCell cell)
        {
            foreach (Transform child in _commandButtonsParent)
                Destroy(child.gameObject);

            foreach (var command in cell.Item.availableCommands)
            {
                var button = Instantiate(_commandButtonPrefab, _commandButtonsParent);
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                text.text = command.Name;

                SFInventoryCommand cmd = command; // capture local copy
                InventoryCell capturedCell = cell;

                button.onClick.AddListener(() =>
                {
                    SFInventoryCommandRouter.Instance.ExecuteCommand(cmd.GetType(), capturedCell, this);
                });
            }
        }
    }
}