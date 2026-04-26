using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UniversalHelpUI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Tooltip("Кнопка открытия панели, когда игрок в зоне триггера")]
    private KeyCode openKey = KeyCode.E;
    [SerializeField, Tooltip("Если включено и Canvas уже активен в инспекторе, UI не будет скрыт в Awake")]
    private bool keepCanvasStateOnStart = false;

    [Header("UI Refs")]
    [SerializeField] private Canvas canvasRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image itemSpriteImage;
    [SerializeField] private Sprite itemSprite;
    [SerializeField, Tooltip("Текст стрелки-подсказки. Показывается только если есть страницы")]
    private TMP_Text arrowHintText;
    [SerializeField] private string arrowHintValue = "-->";
    [SerializeField] private TMP_Text pageCounterText;
    [SerializeField] private Image darkOverlayImage;
    [SerializeField] private CanvasGroup darkOverlayGroup;
    [SerializeField] private TMP_Text pageContentText;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float canvasFadeDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float darkOverlayFadeDuration = 0.18f;

    [Header("Player Lock")]
    [SerializeField, Tooltip("Компоненты игрока, которые будут отключены, пока UI открыт")]
    private Behaviour[] playerComponentsToDisable;
    [SerializeField, Tooltip("Опционально: Rigidbody игрока для заморозки")]
    private Rigidbody playerRigidbody;
    [SerializeField, Tooltip("Замораживать Rigidbody игрока, пока UI открыт")]
    private bool freezePlayerRigidbody = true;

    [Header("Pages")]
    [SerializeField, TextArea(2, 8)] private string[] pages;
    [SerializeField, Tooltip("Формат нумерации: 0..N-1. Если выключено, будет 1..N")]
    private bool zeroBasedPageIndex = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pageFlipClip;
    [SerializeField, Range(0f, 1f)] private float pageFlipVolume = 1f;

    private int currentPageIndex;
    private bool playerInside;
    private bool uiOpened;
    private bool[] disabledComponentPreviousState;
    private RigidbodyConstraints cachedRigidbodyConstraints;
    private bool hasCachedRigidbody;
    private Coroutine canvasFadeCoroutine;
    private Coroutine darkOverlayFadeCoroutine;

    private bool HasPages => pages != null && pages.Length > 0;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        ApplySprite();
        EnsureCanvasGroups();

        if (keepCanvasStateOnStart && canvasRoot != null && (canvasRoot.gameObject.activeSelf || canvasRoot.enabled))
        {
            uiOpened = true;
            currentPageIndex = 0;
            SetGraphicVisible(itemSpriteImage, true);

            if (arrowHintText != null)
            {
                arrowHintText.text = arrowHintValue;
                arrowHintText.enabled = HasPages;
            }

            SetDarkOverlayVisibleImmediate(false);
            SetTextVisible(pageContentText, false);
            UpdatePageCounter(initialState: true);
            return;
        }

        HideAllUIImmediate();
    }

    private void Update()
    {
        if (!uiOpened)
        {
            if (playerInside && Input.GetKeyDown(openKey))
                OpenUI();

            return;
        }

        if (Input.GetKeyDown(openKey))
        {
            CloseUIByKey();
            return;
        }

        if (!HasPages)
            return;

        if (Input.GetKeyDown(KeyCode.D))
        {
            NextPage();
            return;
        }

        if (Input.GetKeyDown(KeyCode.A))
            PreviousPage();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInside = true;

        if (playerRigidbody == null)
        {
            if (other.attachedRigidbody != null)
                playerRigidbody = other.attachedRigidbody;
            else
                playerRigidbody = other.GetComponentInParent<Rigidbody>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInside = false;
        CloseUI();
    }

    public void OpenUI()
    {
        if (uiOpened)
            return;

        uiOpened = true;
        currentPageIndex = 0;
        LockPlayerControls();
        ApplySprite();

        StopCanvasFade();
        SetCanvasVisible(true);
        StartCanvasFade(1f, canvasFadeDuration, null);
        SetGraphicVisible(itemSpriteImage, itemSprite != null);

        if (arrowHintText != null)
        {
            arrowHintText.text = arrowHintValue;
            arrowHintText.enabled = HasPages;
        }

        // При открытии показываем только спрайт и подсказку, без затемнения и текста.
        SetDarkOverlayVisibleImmediate(false);
        SetTextVisible(pageContentText, false);

        if (pageContentText != null)
            pageContentText.text = string.Empty;

        UpdatePageCounter(initialState: true);
    }

    public void CloseUI()
    {
        if (!uiOpened && (canvasRoot == null || !canvasRoot.gameObject.activeSelf))
            return;

        uiOpened = false;
        UnlockPlayerControls();
        HideContentKeepCanvasForFade();

        StopCanvasFade();
        if (canvasGroup != null)
            StartCanvasFade(0f, canvasFadeDuration, HideAllUIImmediate);
        else
            HideAllUIImmediate();
    }

    private void CloseUIByKey()
    {
        CloseUI();
    }

    private void LockPlayerControls()
    {
        if (playerComponentsToDisable != null && playerComponentsToDisable.Length > 0)
        {
            int count = playerComponentsToDisable.Length;
            if (disabledComponentPreviousState == null || disabledComponentPreviousState.Length != count)
                disabledComponentPreviousState = new bool[count];

            for (int i = 0; i < count; i++)
            {
                Behaviour behaviour = playerComponentsToDisable[i];
                if (behaviour == null)
                    continue;

                disabledComponentPreviousState[i] = behaviour.enabled;
                behaviour.enabled = false;
            }
        }

        if (freezePlayerRigidbody && playerRigidbody != null)
        {
            cachedRigidbodyConstraints = playerRigidbody.constraints;
            hasCachedRigidbody = true;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    private void UnlockPlayerControls()
    {
        if (playerComponentsToDisable != null && disabledComponentPreviousState != null)
        {
            int count = Mathf.Min(playerComponentsToDisable.Length, disabledComponentPreviousState.Length);
            for (int i = 0; i < count; i++)
            {
                Behaviour behaviour = playerComponentsToDisable[i];
                if (behaviour == null)
                    continue;

                behaviour.enabled = disabledComponentPreviousState[i];
            }
        }

        if (playerRigidbody != null && hasCachedRigidbody)
        {
            playerRigidbody.constraints = cachedRigidbodyConstraints;
            hasCachedRigidbody = false;
        }
    }

    private void NextPage()
    {
        if (!HasPages)
            return;

        if (!IsTextPageVisible())
        {
            currentPageIndex = 0;
            ShowCurrentPage();
            PlayFlipSound();
            return;
        }

        if (currentPageIndex >= pages.Length - 1)
        {
            CloseUI();
            return;
        }

        int nextIndex = Mathf.Clamp(currentPageIndex + 1, 0, pages.Length - 1);
        if (nextIndex == currentPageIndex)
            return;

        currentPageIndex = nextIndex;
        ShowCurrentPage();
        PlayFlipSound();
    }

    private void PreviousPage()
    {
        if (!HasPages)
            return;

        if (!IsTextPageVisible())
            return;

        if (currentPageIndex <= 0)
        {
            ReturnToSpriteOnlyView();
            return;
        }

        int prevIndex = Mathf.Clamp(currentPageIndex - 1, 0, pages.Length - 1);
        if (prevIndex == currentPageIndex)
            return;

        currentPageIndex = prevIndex;
        ShowCurrentPage();
        PlayFlipSound();
    }

    private void ShowCurrentPage()
    {
        FadeDarkOverlayTo(1f, darkOverlayFadeDuration);
        SetTextVisible(pageContentText, true);

        if (pageContentText != null)
            pageContentText.text = pages[currentPageIndex];

        UpdatePageCounter(initialState: false);
    }

    private void ReturnToSpriteOnlyView()
    {
        FadeDarkOverlayTo(0f, darkOverlayFadeDuration);
        SetTextVisible(pageContentText, false);

        if (pageContentText != null)
            pageContentText.text = string.Empty;

        currentPageIndex = 0;
        UpdatePageCounter(initialState: true);
    }

    private void UpdatePageCounter(bool initialState)
    {
        if (pageCounterText == null)
            return;

        if (!HasPages)
        {
            pageCounterText.enabled = false;
            return;
        }

        pageCounterText.enabled = true;

        int shownIndex = initialState
            ? (zeroBasedPageIndex ? 0 : 1)
            : (currentPageIndex + 1);

        pageCounterText.text = shownIndex + " из " + pages.Length;
    }

    private void HideAllUIImmediate()
    {
        StopCanvasFade();
        StopDarkOverlayFade();
        SetCanvasVisible(false);
        SetGraphicVisible(itemSpriteImage, false);
        SetDarkOverlayVisibleImmediate(false);
        SetTextVisible(pageContentText, false);

        if (arrowHintText != null)
            arrowHintText.enabled = false;

        if (pageCounterText != null)
            pageCounterText.enabled = false;
    }

    private void ApplySprite()
    {
        if (itemSpriteImage == null)
            return;

        itemSpriteImage.sprite = itemSprite;
        itemSpriteImage.enabled = itemSprite != null;
    }

    private void PlayFlipSound()
    {
        if (audioSource == null || pageFlipClip == null)
            return;

        audioSource.PlayOneShot(pageFlipClip, pageFlipVolume);
    }

    private bool IsTextPageVisible()
    {
        if (darkOverlayImage != null)
            return darkOverlayImage.enabled;

        if (pageContentText != null)
            return pageContentText.enabled;

        return false;
    }

    private void SetCanvasVisible(bool visible)
    {
        if (canvasRoot == null)
            return;

        if (canvasRoot.gameObject.activeSelf != visible)
            canvasRoot.gameObject.SetActive(visible);

        canvasRoot.enabled = visible;
    }

    private void HideContentKeepCanvasForFade()
    {
        StopDarkOverlayFade();
        SetGraphicVisible(itemSpriteImage, false);
        SetDarkOverlayVisibleImmediate(false);
        SetTextVisible(pageContentText, false);

        if (arrowHintText != null)
            arrowHintText.enabled = false;

        if (pageCounterText != null)
            pageCounterText.enabled = false;
    }

    private void EnsureCanvasGroups()
    {
        if (canvasGroup == null && canvasRoot != null)
            canvasGroup = canvasRoot.GetComponent<CanvasGroup>();

        if (darkOverlayGroup == null && darkOverlayImage != null)
            darkOverlayGroup = darkOverlayImage.GetComponent<CanvasGroup>();
    }

    private void SetDarkOverlayVisibleImmediate(bool visible)
    {
        if (darkOverlayImage != null)
            darkOverlayImage.enabled = visible;

        if (darkOverlayGroup != null)
            darkOverlayGroup.alpha = visible ? 1f : 0f;
    }

    private void FadeDarkOverlayTo(float targetAlpha, float duration)
    {
        StopDarkOverlayFade();

        if (darkOverlayImage == null || darkOverlayGroup == null)
        {
            SetDarkOverlayVisibleImmediate(targetAlpha > 0.001f);
            return;
        }

        if (targetAlpha > 0.001f)
            darkOverlayImage.enabled = true;

        darkOverlayFadeCoroutine = StartCoroutine(FadeCanvasGroup(
            darkOverlayGroup,
            targetAlpha,
            duration,
            () =>
            {
                if (targetAlpha <= 0.001f)
                    darkOverlayImage.enabled = false;
            }));
    }

    private void StartCanvasFade(float targetAlpha, float duration, System.Action onComplete)
    {
        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            return;
        }

        canvasFadeCoroutine = StartCoroutine(FadeCanvasGroup(canvasGroup, targetAlpha, duration, onComplete));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, float duration, System.Action onComplete)
    {
        if (group == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float startAlpha = group.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        group.alpha = targetAlpha;
        onComplete?.Invoke();
    }

    private void StopCanvasFade()
    {
        if (canvasFadeCoroutine != null)
        {
            StopCoroutine(canvasFadeCoroutine);
            canvasFadeCoroutine = null;
        }
    }

    private void StopDarkOverlayFade()
    {
        if (darkOverlayFadeCoroutine != null)
        {
            StopCoroutine(darkOverlayFadeCoroutine);
            darkOverlayFadeCoroutine = null;
        }
    }

    private void SetGraphicVisible(Graphic graphic, bool visible)
    {
        if (graphic != null)
            graphic.enabled = visible;
    }

    private void SetTextVisible(TMP_Text text, bool visible)
    {
        if (text != null)
            text.enabled = visible;
    }
}
