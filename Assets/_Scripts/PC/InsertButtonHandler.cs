using UnityEngine;
using UnityEngine.EventSystems;

public class InsertButtonHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public ComputerInteraction manager; // Назначь в Inspector основной скрипт (GameObject компьютера)

    private void Awake()
    {
        if (manager == null)
            manager = GetComponentInParent<ComputerInteraction>();

        if (manager == null)
            Debug.LogWarning("[InsertButtonHandler] ComputerInteraction не найден. Назначь manager в Inspector.");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (manager != null && eventData != null && eventData.button == PointerEventData.InputButton.Left)
        {
            manager.StartInserting();
            manager.BeginInsertHold(true);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (manager != null && eventData != null && eventData.button == PointerEventData.InputButton.Left)
            manager.EndInsertHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager != null)
            manager.EndInsertHold();
    }
}