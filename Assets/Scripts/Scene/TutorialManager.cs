using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private GameObject hintIconGO;              // ← Объект с иконкой H (всегда виден)
    [SerializeField] private Image hintImage;                    // ← Image для спрайта клавиши H
    [SerializeField] private Sprite hintSprite;                  // ← Спрайт (например, клавиша H)

    [SerializeField] private GameObject tutorialPanelGO;         // ← Панель с обучением
    [SerializeField] private CanvasGroup tutorialCanvasGroup;    // ← CanvasGroup на tutorialPanelGO (для fade)

    [Header("Анимация")]
    [SerializeField] private float fadeDuration = 0.5f;          // ← Время fade-in/out

    [Header("Управление видимостью иконки H")]
    [SerializeField] private bool hintIconVisibleByDefault = true; // ← true = видна при старте

    private bool isTutorialOpen = false;
    private bool isHintIconVisible = true;

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
    }

    private void UpdateHintIconVisibility()
    {
        if (hintIconGO != null)
        {
            hintIconGO.SetActive(isHintIconVisible);
        }
    }

    private void OpenTutorial()
    {
        if (isTutorialOpen) return;

        isTutorialOpen = true;

        if (tutorialPanelGO != null)
        {
            tutorialPanelGO.SetActive(true);
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