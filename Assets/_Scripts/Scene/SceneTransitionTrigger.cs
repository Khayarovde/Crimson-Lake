using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Переход")]
    [Tooltip("Имя следующей сцены (должна быть добавлена в Build Settings!)")]
    public string nextSceneName = "NextScene";

    [Header("Задержка и анимация")]
    [Tooltip("Сколько секунд ждать после входа в триггер")]
    public float delayBeforeFade = 3f;

    [Tooltip("Длительность затемнения (секунды)")]
    public float fadeDuration = 2f;

    [Header("UI для затемнения (создай Canvas с чёрным Image и текстом)")]
    [Tooltip("Canvas с чёрным фоном (Image color = black) — изначально SetActive(false)")]
    public Canvas fadeCanvas;

    [Tooltip("CanvasGroup на Canvas/Panel для плавного fade (или скрипт сам найдёт Image)")]
    public CanvasGroup fadeCanvasGroup;

    [Tooltip("Текст по центру Canvas (TextMeshProUGUI)")]
    public TextMeshProUGUI fadeText;

    [Tooltip("Текст, который покажется во время fade")]
    public string transitionMessage = "Загрузка следующей сцены...";

    private bool triggered = false; // Чтобы срабатывало только раз

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !triggered && !string.IsNullOrEmpty(nextSceneName))
        {
            triggered = true;
            StartCoroutine(TransitionSequence());
        }
    }

    private IEnumerator TransitionSequence()
    {
        // Ждём 3 секунды
        yield return new WaitForSeconds(delayBeforeFade);

        // Активируем Canvas
        if (fadeCanvas != null)
        {
            fadeCanvas.gameObject.SetActive(true);
        }

        // Устанавливаем текст
        if (fadeText != null)
        {
            fadeText.text = transitionMessage;
            fadeText.gameObject.SetActive(true);
        }

        // Плавное затемнение
        yield return StartCoroutine(FadeToBlack(fadeDuration));

        // Загружаем новую сцену
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator FadeToBlack(float duration)
    {
        // Если CanvasGroup назначен — используем его (проще)
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }
        else
        {
            // Альтернатива: ищем Image в Canvas и fade alpha
            Image fadeImage = GetFadeImage();
            if (fadeImage != null)
            {
                Color color = fadeImage.color;
                color.a = 0f;
                fadeImage.color = color;

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    color.a = Mathf.Lerp(0f, 1f, elapsed / duration);
                    fadeImage.color = color;
                    yield return null;
                }
                color.a = 1f;
                fadeImage.color = color;
            }
        }
    }

    private Image GetFadeImage()
    {
        if (fadeCanvas == null) return null;
        Image[] images = fadeCanvas.GetComponentsInChildren<Image>();
        foreach (var img in images)
        {
            if (img.color == Color.black || img.name.Contains("Fade") || img.name.Contains("Black"))
                return img;
        }
        return fadeCanvas.GetComponent<Image>();
    }

    // Автонастройка в редакторе
    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    // Проверки в редакторе
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("Назначь имя следующей сцены в 'Next Scene Name'!", this);
        }
    }
}