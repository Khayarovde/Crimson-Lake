using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform rectTransform;       // RectTransform нашего предмета
    private CanvasGroup canvasGroup;          // Группа Canvas для изменения прозрачности
    private Vector3 originalPosition;        // Исходная позиция предмета
    private Inventory inventory;              // Ссылка на инвентарь

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        inventory = FindObjectOfType<Inventory>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.anchoredPosition;
        canvasGroup.blocksRaycasts = false;   // Переключаем лучи на пропуск через объект
        canvasGroup.alpha = 0.6f;            // Сделаем слегка прозрачной
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;    // Включаем снова обработку лучей
        canvasGroup.alpha = 1f;              // Возвращаем обычный уровень прозрачности
        rectTransform.anchoredPosition = originalPosition; // Возвращаем на место
    }
}