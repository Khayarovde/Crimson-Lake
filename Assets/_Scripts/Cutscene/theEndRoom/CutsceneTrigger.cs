using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CutsceneTrigger : MonoBehaviour
{
    [Tooltip("Ключ катсцены из CutsceneManager")]
    public string cutsceneKey;

    [Tooltip("Тег объекта, который будет инициировать катсцену")]
    public string playerTag = "Player";

    [Tooltip("Если true — сработает только один раз")]
    public bool singleUse = true;

    private bool _triggered = false;

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

        // Если одноразовый и уже сработал — игнорируем
        if (singleUse && _triggered)
            return;

        if (CutsceneManager.Instance == null)
        {
            Debug.LogWarning("CutsceneManager.Instance == null при попытке запустить катсцену.");
            return;
        }

        CutsceneManager.Instance.StartCutscene(cutsceneKey);

        if (singleUse)
        {
            _triggered = true;
            enabled = false; // OnTriggerEnter больше не будет вызываться
        }
    }
}