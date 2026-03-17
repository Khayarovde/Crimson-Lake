using UnityEngine;
using UnityEngine.VFX;

public class VfxTriggerZone : MonoBehaviour
{
    [Header("VFX")]
    [SerializeField] private VisualEffect targetVfx;
    [SerializeField] private bool stopOnExit = true;
    [SerializeField] private bool triggerOnlyOnce;

    [Header("Filter")]
    [SerializeField] private string actorTag = "Player";

    [Header("Optional VFX Events")]
    [SerializeField] private bool sendCustomEvents;
    [SerializeField] private string enterEventName = "OnPlay";
    [SerializeField] private string exitEventName = "OnStop";

    private bool hasTriggered;

    private void Reset()
    {
        if (targetVfx == null)
            targetVfx = GetComponentInChildren<VisualEffect>();

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(actorTag))
            return;

        if (triggerOnlyOnce && hasTriggered)
            return;

        if (targetVfx == null)
            return;

        targetVfx.Play();

        if (sendCustomEvents && !string.IsNullOrWhiteSpace(enterEventName))
            targetVfx.SendEvent(enterEventName);

        hasTriggered = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!stopOnExit)
            return;

        if (!other.CompareTag(actorTag))
            return;

        if (targetVfx == null)
            return;

        if (sendCustomEvents && !string.IsNullOrWhiteSpace(exitEventName))
            targetVfx.SendEvent(exitEventName);

        targetVfx.Stop();
    }
}