using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlotPointerHandler : MonoBehaviour, IPointerClickHandler
{
    public int SlotIndex { get; set; }
    public Action<int, PointerEventData.InputButton> OnSlotPointerClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        OnSlotPointerClick?.Invoke(SlotIndex, eventData.button);
    }
}
