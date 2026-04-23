using UnityEngine;

// Этот скрипт должен быть на объекте, который мы хотим подсвечивать.
// Ему нужен компонент Outline (или аналогичный с полями color и width)
[RequireComponent(typeof(Outline))]
public class InteractiveHighlighter : MonoBehaviour {
  [Header("Outline Settings")]
  [SerializeField] private Color outlineColor = Color.yellow;
  [SerializeField] private float outlineWidth = 5f;

  private Outline outline;
  private bool isPlayerNearby = false;

  void Start() {
    // Получаем компонент Outline при старте игры
    outline = GetComponent<Outline>();

    if (outline == null) {
      Debug.LogError("Компонент Outline не найден на объекте " + gameObject.name);
      enabled = false; // Отключаем скрипт, если нет Outline
      return;
    }

    // Устанавливаем начальные настройки цвета и ширины из полей скрипта
    ApplyOutlineSettings();

    // Убеждаемся, что изначально обводка выключена
    outline.enabled = false;
  }

  // Применяет настройки цвета и ширины к компоненту Outline
  private void ApplyOutlineSettings() {
    if (outline != null) {
      outline.OutlineColor = outlineColor;
      outline.OutlineWidth = outlineWidth;
    }
  }

  // Вызывается, когда другой объект входит в триггер
  private void OnTriggerEnter(Collider other) {
    // Проверяем, вошедший объект - игрок (по тегу "Player")
    if (other.CompareTag("Player")) {
      isPlayerNearby = true;
      // Включаем обводку
      if (outline != null) {
        outline.enabled = true;
      }
    }
  }

  // Вызывается, когда другой объект покидает триггер
  private void OnTriggerExit(Collider other) {
    // Проверяем, вышедший объект - игрок
    if (other.CompareTag("Player")) {
      isPlayerNearby = false;
      // Выключаем обводку
      if (outline != null) {
        outline.enabled = false;
      }
    }
  }

  // Это позволит нам менять цвет и ширину прямо во время игры в редакторе
  private void OnValidate() {
    // Эта функция вызывается, когда меняются значения в инспекторе
    if (Application.isPlaying && outline != null) {
      ApplyOutlineSettings();
    }
  }
}