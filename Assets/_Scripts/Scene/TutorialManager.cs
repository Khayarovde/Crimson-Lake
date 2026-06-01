using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private GameObject hintIconGO;              // ← Объект с иконкой H (всегда виден)
    [SerializeField] private Image hintImage;                    // ← Image для спрайта клавиши H
    [SerializeField] private Sprite hintSprite;                  // ← Спрайт (например, клавиша H)
    [SerializeField] private TextMeshProUGUI hintText;            // ← Текст рядом с иконкой H

    [SerializeField] private GameObject tutorialPanelGO;         // ← Панель с обучением
    [SerializeField] private CanvasGroup tutorialCanvasGroup;    // ← CanvasGroup на tutorialPanelGO (для fade)

    [Header("Анимация")]
    [SerializeField] private float fadeDuration = 0.5f;          // ← Время fade-in/out

    [Header("Управление видимостью иконки H")]
    [SerializeField] private bool hintIconVisibleByDefault = true; // ← true = видна при старте

    private bool isTutorialOpen = false;
    private bool isHintIconVisible = true;
    // Глобальный флаг, который другие UI-скрипты должны проверять
    // перед тем, как открывать инвентарь, паузу или другие оверлеи.
    // Пример использования в другом скрипте:
    // if (TutorialManager.TutorialIsOpen) return; // не открывать UI
    public static bool TutorialIsOpen { get; private set; } = false;
    // Вспомогательные поля для управления порядком канвы и игнорирования закрытия
    private Canvas _runtimeCanvas; // Canvas на панели туториала (можем создать временно)
    private bool _createdRuntimeCanvas = false;
    private bool _ignoreAnyKeyThisFrame = false;
    private bool _prevOverrideSorting;
    private int _prevSortingOrder;

    void Start()
    {
        // Настройка иконки H
        if (hintImage != null && hintSprite != null)
        {
            hintImage.sprite = hintSprite;
        }

        // Изначально tutorial скрыт
        if (tutorialPanelGO != null)
        {
            tutorialPanelGO.SetActive(false);
        }
        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = 0f;
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
        }

        // Устанавливаем видимость иконки H
        isHintIconVisible = hintIconVisibleByDefault;
        UpdateHintIconVisibility();
    }

    void Update()
    {
        // F1 — вкл/выкл иконку H
        if (Input.GetKeyDown(KeyCode.F1))
        {
            isHintIconVisible = !isHintIconVisible;
            UpdateHintIconVisibility();
            Debug.Log("Иконка H: " + (isHintIconVisible ? "Включена" : "Выключена"));
        }

        // H — переключение tutorial
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (isTutorialOpen)
            {
                CloseTutorial();
            }
            else
            {
                OpenTutorial();
            }
        }

        // Если туториал открыт, любую клавишу можно использовать для закрытия,
        // но игнорируем событие в том же кадре, когда открыли туториал.
        if (isTutorialOpen && !_ignoreAnyKeyThisFrame && Input.anyKeyDown)
        {
            CloseTutorial();
        }

        // Сброс флага игнорирования в конце кадра
        _ignoreAnyKeyThisFrame = false;
    }

    private void UpdateHintIconVisibility()
    {
        if (hintIconGO != null)
        {
            hintIconGO.SetActive(isHintIconVisible);
        }

        if (hintText != null)
        {
            hintText.gameObject.SetActive(isHintIconVisible);
        }
    }

    public bool IsHintIconVisible()
    {
        return isHintIconVisible;
    }

    public void SetHintIconVisibleFromSave(bool visible)
    {
        isHintIconVisible = visible;
        UpdateHintIconVisibility();
    }

    private void OpenTutorial()
    {
        if (isTutorialOpen) return;

        isTutorialOpen = true;
        TutorialIsOpen = true;
        _ignoreAnyKeyThisFrame = true;

        if (tutorialPanelGO != null)
        {
            tutorialPanelGO.SetActive(true);
            // Поднимаем панель туториала на самый верх иерархии, чтобы
            // другие UI не отображались поверх него.
            tutorialPanelGO.transform.SetAsLastSibling();
            // Обеспечим, что панель рендерится поверх других Canvas'ов.
            _runtimeCanvas = tutorialPanelGO.GetComponent<Canvas>();
            if (_runtimeCanvas == null)
            {
                _runtimeCanvas = tutorialPanelGO.AddComponent<Canvas>();
                _createdRuntimeCanvas = true;
            }
            // Сохраним прежние значения, если Canvas был
            _prevOverrideSorting = _runtimeCanvas.overrideSorting;
            _prevSortingOrder = _runtimeCanvas.sortingOrder;
            _runtimeCanvas.overrideSorting = true;
            _runtimeCanvas.sortingOrder = 1000;
        }

        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.interactable = true;
            tutorialCanvasGroup.blocksRaycasts = true;
            StartCoroutine(FadeCanvasGroup(tutorialCanvasGroup, 1f, fadeDuration));
        }

        Debug.Log("Обучение открыто (нажмите H для закрытия)");
    }

    private void CloseTutorial()
    {
        if (!isTutorialOpen) return;

        isTutorialOpen = false;
        TutorialIsOpen = false;

        if (tutorialCanvasGroup != null)
        {
            StartCoroutine(FadeCanvasGroup(tutorialCanvasGroup, 0f, fadeDuration, () =>
            {
                if (tutorialPanelGO != null)
                {
                    tutorialPanelGO.SetActive(false);
                }
                tutorialCanvasGroup.interactable = false;
                tutorialCanvasGroup.blocksRaycasts = false;
            }));
        }
        else
        {
            if (tutorialPanelGO != null)
            {
                tutorialPanelGO.SetActive(false);
            }
        }

        // Восстановим Canvas
        if (_runtimeCanvas != null)
        {
            if (_createdRuntimeCanvas)
            {
                Destroy(_runtimeCanvas);
            }
            else
            {
                _runtimeCanvas.overrideSorting = _prevOverrideSorting;
                _runtimeCanvas.sortingOrder = _prevSortingOrder;
            }
            _runtimeCanvas = null;
            _createdRuntimeCanvas = false;
        }

        Debug.Log("Обучение закрыто (нажмите H для открытия)");
    }

    // Универсальный fade
    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration, System.Action onComplete = null)
    {
        float startAlpha = cg.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        cg.alpha = targetAlpha;
        onComplete?.Invoke();
    }

    // Для теста: сброс в контекстном меню
    [ContextMenu("Toggle Hint Icon (Test)")]
    private void ToggleHintTest()
    {
        isHintIconVisible = !isHintIconVisible;
        UpdateHintIconVisibility();
    }
}