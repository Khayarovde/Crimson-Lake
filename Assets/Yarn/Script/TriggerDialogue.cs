using UnityEngine;
using Yarn.Unity;

public class TriggerDialogue : MonoBehaviour
{
    [Header("Yarn Spinner настройки")]
    [Tooltip("Имя Node'а, который нужно запустить")]
    public string dialogueNode = "Start";

    [Tooltip("Ссылка на Dialogue Runner")]
    public DialogueRunner dialogueRunner;

    [Header("Режим показа")]
    [Tooltip("Если включено — диалог будет запускаться каждый раз при входе в триггер")]
    public bool infiniteShow = false;

    [Header("Дополнительно")]
    [Tooltip("Тэг игрока (по умолчанию Player)")]
    public string playerTag = "Player";

    private bool hasPlayed = false;
    private bool isPlayerInside = false;   // ← Новое: отслеживаем, внутри ли игрок

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        isPlayerInside = true;

        // Если диалог уже был показан и infiniteShow выключен — выходим
        if (!infiniteShow && hasPlayed)
            return;

        // Не запускаем, если диалог уже идёт
        if (dialogueRunner.IsDialogueRunning)
            return;

        TryStartDialogue();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        isPlayerInside = false;
    }

    private void TryStartDialogue()
    {
        if (dialogueRunner == null)
        {
            Debug.LogError("DialogueRunner не назначен на " + gameObject.name);
            return;
        }

        

        dialogueRunner.StartDialogue(dialogueNode);
        hasPlayed = true;
    }

    // Для удобства в редакторе
    [ContextMenu("Reset HasPlayed")]
    private void ResetHasPlayed()
    {
        hasPlayed = false;
    }
}