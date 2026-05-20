using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public GameObject promptUI;         // ← Перетащи сюда Canvas или Text с подсказкой "Нажми E"
    public float interactRange = 3f;
    public LayerMask interactLayer = -1; // ← Укажи слой, на котором находятся NPC/предметы с Interact

    private Interact currentTarget;
    private bool hidePromptUntilTargetChanges;

    void Update()
    {
        if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
        {
            currentTarget.StartDialogue();
            hidePromptUntilTargetChanges = true;
            currentTarget.SetInteractionHintVisible(false);
            HidePrompt();
        }
    }

    void FixedUpdate()                      // Можно использовать Update(), но FixedUpdate лучше для физики
    {
        Interact previousTarget = currentTarget;
        currentTarget = FindClosestTarget();

        if (currentTarget != previousTarget)
        {
            if (previousTarget != null)
            {
                previousTarget.SetInteractionHintVisible(false);
            }

            hidePromptUntilTargetChanges = false;
        }

        if (currentTarget != null && !hidePromptUntilTargetChanges)
        {
            ShowPrompt();
            currentTarget.SetInteractionHintVisible(true);
        }
        else
        {
            HidePrompt();
            if (currentTarget != null)
            {
                currentTarget.SetInteractionHintVisible(false);
            }
        }
    }

    private Collider[] overlapHits = new Collider[16];

    private Interact FindClosestTarget()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, interactRange, overlapHits, interactLayer);
        Interact closestInteract = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var hit = overlapHits[i];
            var interact = hit.GetComponent<Interact>();
            if (interact == null)
            {
                continue;
            }

            float distanceSqr = (interact.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestInteract = interact;
            }
        }

        return closestInteract;
    }

    // Эти два метода были пропущены — теперь они здесь
    private void ShowPrompt()
    {
        if (promptUI != null)
            promptUI.SetActive(true);
    }

    private void HidePrompt()
    {
        if (promptUI != null)
            promptUI.SetActive(false);
    }
}