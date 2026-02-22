using UnityEngine;

public class FinishingAnimationEventRelay : MonoBehaviour
{
    [Header("References")]
    public FinishingManager manager;
    public Transform player;
    public Transform enemy;

    public void StartFinishingFromEvent()
    {
        if (manager == null)
        {
            Debug.LogWarning("FinishingAnimationEventRelay: manager is not assigned.");
            return;
        }

        if (manager.IsFinishingActive)
        {
            return;
        }

        Transform enemyTransform = enemy != null ? enemy : transform;
        manager.StartFinishingImmediate(player, enemyTransform);
    }

    public void StartFinishingEffectEvent()
    {
        if (manager == null)
        {
            Debug.LogWarning("FinishingAnimationEventRelay: manager is not assigned.");
            return;
        }

        manager.StartFinishingEffect();
    }

    public void EndFinishingEffectEvent()
    {
        if (manager == null)
        {
            Debug.LogWarning("FinishingAnimationEventRelay: manager is not assigned.");
            return;
        }

        manager.EndFinishingEffect();
    }
}
