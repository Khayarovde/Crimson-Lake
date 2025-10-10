using UnityEngine;
using UnityEngine.EventSystems;

public class Slot : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        var droppedItem = eventData.pointerDrag.GetComponent<DraggableItem>();
        if (droppedItem != null)
        {
            Debug.Log("Предмет успешно положен в слот!");
        }
    }
}