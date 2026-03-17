using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class DoorInteraction : MonoBehaviour
{
    [Header("Подсказка взаимодействия")]
    public GameObject interactionHint;           // Перетащи сюда UI Image (с твоим пиксельным спрайтом E)

    [Header("Позиционирование на экране")]
    [Tooltip("Смещение относительно объекта (можно двигать прямо в Scene View!)")]
    public Vector3 worldOffset = new Vector3(0, 2f, 0);

    [Header("Пиксельная анимация появления")]
    public bool pixelPopEffect = true;           // Включает крутой "пиксельный поп" эффект
    public float popDuration = 0.25f;            // Длительность анимации
    [Range(0.9f, 1.3f)] public float overshoot = 1.15f; // Насколько "прыгнет" иконка

    private bool playerNear = false;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 originalScale;

    private void Awake()
    {
        if (interactionHint != null)
        {
            canvasGroup = interactionHint.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = interactionHint.AddComponent<CanvasGroup>();

            rectTransform = interactionHint.GetComponent<RectTransform>();
            originalScale = interactionHint.transform.localScale;

            canvasGroup.alpha = 0f;
            interactionHint.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerNear = true;
        if (interactionHint != null)
        {
            interactionHint.SetActive(true);
            if (pixelPopEffect)
                StartPixelPop();
            else
                canvasGroup.alpha = 1f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerNear = false;
        if (interactionHint != null)
        {
            canvasGroup.alpha = 0f;
            interactionHint.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (playerNear && interactionHint != null && interactionHint.activeInHierarchy)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + worldOffset);
            if (screenPos.z > 0)
            {
                interactionHint.transform.position = screenPos;
            }
        }
    }

    // Крутая пиксельная анимация появления (как в старых играх!)
    private void StartPixelPop()
    {
        StopAllCoroutines();
        StartCoroutine(PixelPopCoroutine());
    }

    private System.Collections.IEnumerator PixelPopCoroutine()
    {
        interactionHint.transform.localScale = originalScale * 0.3f;
        canvasGroup.alpha = 1f;

        float t = 0;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = t / popDuration;

            // Bounce-эффект: пружинит и чуть перелетает
            float scale = Mathf.Sin(progress * Mathf.PI * (0.5f + progress)) * (overshoot - 1f) + 1f;
            interactionHint.transform.localScale = originalScale * scale;

            yield return null;
        }

        interactionHint.transform.localScale = originalScale;
    }

    // ВАЖНО: Чтобы видеть смещение в редакторе — даже когда игра не запущена!
    private void OnDrawGizmosSelected()
    {
        if (interactionHint != null && Camera.main != null)
        {
            Vector3 worldPos = transform.position + worldOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, worldPos);
            Gizmos.DrawWireSphere(worldPos, 0.2f);

            // Рисуем иконку в Scene View (примерно)
            // UnityEditor.Handles.Label(screenPos + new Vector3(20, 20), "E");
        }
    }
}