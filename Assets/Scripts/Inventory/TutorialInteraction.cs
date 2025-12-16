using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class TutorialInteraction : MonoBehaviour
{
    [Header("Подсказка взаимодействия (E)")]
    public GameObject interactionHint;           // UI с иконкой/текстом "E" (как у дверей)

    [Header("Панель обучения")]
    public GameObject tutorialPanel;             // Полноценная панель с WASD, Tab и т.д.

    [Header("Fade анимация панели обучения")]
    public float fadeInDuration = 0.4f;
    public float fadeOutDuration = 0.4f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Автоматическое поведение")]
    public bool autoHideOnExit = true;           // Скрывать всё при выходе из триггера
    public float autoHideDelay = 0f;             // Автозакрытие панели через время (0 = выкл)

    private bool playerInTrigger = false;
    private bool tutorialShown = false;

    private CanvasGroup hintCanvasGroup;
    private CanvasGroup tutorialCanvasGroup;

    private void Awake()
    {
        // Настройка подсказки E
        if (interactionHint != null)
        {
            hintCanvasGroup = interactionHint.GetComponent<CanvasGroup>();
            if (hintCanvasGroup == null) hintCanvasGroup = interactionHint.AddComponent<CanvasGroup>();
            hintCanvasGroup.alpha = 0f;
            interactionHint.SetActive(false);
        }

        // Настройка панели обучения
        if (tutorialPanel != null)
        {
            tutorialCanvasGroup = tutorialPanel.GetComponent<CanvasGroup>();
            if (tutorialCanvasGroup == null) tutorialCanvasGroup = tutorialPanel.AddComponent<CanvasGroup>();

            tutorialCanvasGroup.alpha = 0f;
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
            tutorialPanel.SetActive(true); // Активна, но невидима
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInTrigger = true;

        // Показываем подсказку "E"
        if (interactionHint != null)
        {
            interactionHint.SetActive(true);
            hintCanvasGroup.alpha = 1f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInTrigger = false;
        tutorialShown = false;

        // Скрываем всё
        if (interactionHint != null)
        {
            hintCanvasGroup.alpha = 0f;
            interactionHint.SetActive(false);
        }

        if (tutorialPanel != null && autoHideOnExit)
        {
            StopAllCoroutines();
            StartCoroutine(FadeOutTutorial());
        }
    }

    private void Update()
    {
        // Проверяем нажатие E только внутри триггера
        if (playerInTrigger && Input.GetKeyDown(KeyCode.E) && !tutorialShown)
        {
            ShowTutorial();
        }
    }

    private void ShowTutorial()
    {
        tutorialShown = true;

        // Скрываем подсказку E (опционально — можно оставить)
        if (interactionHint != null)
        {
            hintCanvasGroup.alpha = 0f;
            // interactionHint.SetActive(false); // Раскомментируй, если хочешь полностью убрать
        }

        StopAllCoroutines();
        StartCoroutine(FadeInTutorial());

        if (autoHideDelay > 0f)
            Invoke(nameof(HideTutorial), autoHideDelay);
    }

    public void HideTutorial() // Можно вызвать с кнопки "Понятно"
    {
        if (!tutorialShown) return;

        tutorialShown = false;
        StopAllCoroutines();
        StartCoroutine(FadeOutTutorial());

        // Возвращаем подсказку E, если игрок всё ещё в триггере
        if (playerInTrigger && interactionHint != null)
        {
            hintCanvasGroup.alpha = 1f;
        }
    }

    private IEnumerator FadeInTutorial()
    {
        tutorialCanvasGroup.interactable = true;
        tutorialCanvasGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / fadeInDuration);
            tutorialCanvasGroup.alpha = fadeCurve.Evaluate(progress);
            yield return null;
        }

        tutorialCanvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutTutorial()
    {
        float t = 0f;
        float startAlpha = tutorialCanvasGroup.alpha;

        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / fadeOutDuration);
            tutorialCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, fadeCurve.Evaluate(progress));
            yield return null;
        }

        tutorialCanvasGroup.alpha = 0f;
        tutorialCanvasGroup.interactable = false;
        tutorialCanvasGroup.blocksRaycasts = false;
    }

    // Опционально: показать в начале игры без триггера
    public void ShowAtStart()
    {
        ShowTutorial();
    }
}