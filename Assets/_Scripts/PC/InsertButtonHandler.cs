using UnityEngine;
using UnityEngine.EventSystems;

public class InsertButtonHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ComputerInteraction manager; // Назначь в Inspector основной скрипт (GameObject компьютера)

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (manager != null)
        {
            manager.mouseOverButton = true;
            Debug.Log("Mouse entered button");
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager != null)
        {
            manager.mouseOverButton = false;
            Debug.Log("Mouse exited button");
        }
    }
}