using UnityEngine;

[RequireComponent(typeof(Collider))]
// Компонент-триггер: при входе игрока запускает катсцену через CutsceneManager
public class CutsceneTrigger : MonoBehaviour
{
    [Tooltip("Ключ катсцены из CutsceneManager")]
    public string cutsceneKey;

    [Tooltip("Тег объекта, который будет инициировать катсцену")]
    public string playerTag = "Player";

    [Tooltip("Если true — отключить этот компонент после первого срабатывания")]
    public bool singleUse = true;

    private void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col == null)
            Debug.LogError($"Компонент {nameof(CutsceneTrigger)} требует Collider на объекте '{gameObject.name}'.");
        else if (!col.isTrigger)
            Debug.LogWarning($"Collider на '{gameObject.name}' не помечен как IsTrigger — триггер может не сработать.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (CutsceneManager.Instance == null)
        {
            Debug.LogWarning("CutsceneManager.Instance == null при попытке запустить катсцену.");
            return;
        }

        CutsceneManager.Instance.StartCutscene(cutsceneKey);

        if (singleUse)
            enabled = false;
    }
}
