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
        if (manager != null)
            manager.BeginInsertHold();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (manager != null)
            manager.EndInsertHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager != null)
            manager.EndInsertHold();
    }
}