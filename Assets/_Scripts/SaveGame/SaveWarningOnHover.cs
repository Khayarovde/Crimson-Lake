using UnityEngine;
using UnityEngine.EventSystems;

public class SaveWarningOnHover : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private SaveWarningUI warningUI;
    [SerializeField] private string warningMessage = "последние изменения не сохранены";
    [SerializeField] private bool onlyIfUnsaved = true;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (onlyIfUnsaved && (SaveManager.Instance == null || !SaveManager.Instance.HasUnsavedChanges))
            return;

        string pendingMessage = SaveManager.PeekPendingWarningMessage();
        string messageToShow = string.IsNullOrWhiteSpace(pendingMessage) ? warningMessage : pendingMessage;

        if (warningUI == null)
            warningUI = FindFirstObjectByType<SaveWarningUI>();

        if (warningUI != null)
        {
            warningUI.ShowWarning(messageToShow);
            SaveManager.ClearPendingWarningMessage();
        }
    }
}
