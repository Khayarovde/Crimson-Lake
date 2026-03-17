using System.Collections;
using TMPro;
using UnityEngine;

public class SaveWarningUI : MonoBehaviour
{
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private float displaySeconds = 2.5f;
    [SerializeField] private bool hideOnStart = true;

    private Coroutine hideRoutine;

    private void Awake()
    {
        if (hideOnStart)
            SetVisible(false);
    }

    public void ShowWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (warningText != null)
            warningText.text = message;

        SetVisible(true);

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        if (displaySeconds > 0f)
            hideRoutine = StartCoroutine(HideAfterDelay(displaySeconds));
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (warningText != null)
            warningText.gameObject.SetActive(visible);
    }
}
