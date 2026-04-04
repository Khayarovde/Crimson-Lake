using UnityEngine;

[DisallowMultipleComponent]
public class FinishingAnimationEventRelay : MonoBehaviour
{
    [Header("References")]
    public FinishingManager manager;
    public Transform player;
    public Transform enemy;
    [SerializeField] private bool requireFinishableEnemy = true;
    [SerializeField] private bool ignoreFrontSideCheckForEventStart = true;
    [SerializeField] private float autoFindEnemyRadius = 3.5f;
    [SerializeField] private LayerMask enemyLayerMask = ~0;

    private AdvancedEnemyAI advancedEnemy;
    private Enemytest enemyTest;

    private void Awake()
    {
        AutoResolveReferences();
    }

    private void OnValidate()
    {
        AutoResolveReferences();
    }

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

        Transform playerTransform = ResolvePlayerTransform();
        Transform enemyTransform = ResolveEnemyTransform(playerTransform);
        if (enemyTransform == null)
        {
            Debug.LogWarning("FinishingAnimationEventRelay: enemy is not assigned and could not be auto-resolved.");
            return;
        }

        if (playerTransform == null)
        {
            Debug.LogWarning("FinishingAnimationEventRelay: player is not assigned.");
            return;
        }

        if (requireFinishableEnemy && !CanFinishCurrentEnemy())
            return;

        bool previousFrontRequirement = manager.requireFrontSide;
        if (ignoreFrontSideCheckForEventStart)
            manager.requireFrontSide = false;

        manager.StartFinishingImmediate(playerTransform, enemyTransform);

        if (ignoreFrontSideCheckForEventStart)
            manager.requireFrontSide = previousFrontRequirement;
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

    private void AutoResolveReferences()
    {
        if (manager == null)
            manager = FindFirstObjectByType<FinishingManager>();

        if (player == null)
        {
            if (manager != null && manager.player != null)
                player = manager.player;

            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (enemy == null)
        {
            if (manager != null && manager.enemy != null)
                enemy = manager.enemy;

            advancedEnemy = GetComponentInParent<AdvancedEnemyAI>();
            if (advancedEnemy != null)
                enemy = advancedEnemy.transform;

            if (enemy == null)
            {
                enemyTest = GetComponentInParent<Enemytest>();
                if (enemyTest != null)
                    enemy = enemyTest.transform;
            }
        }

        RefreshEnemyCaches();
    }

    private Transform ResolvePlayerTransform()
    {
        if (player == null)
            AutoResolveReferences();

        return player;
    }

    private Transform ResolveEnemyTransform(Transform playerTransform)
    {
        if (enemy == null)
            AutoResolveReferences();

        if (enemy != null)
            return enemy;

        Transform autoFoundEnemy = FindClosestFinishableEnemy(playerTransform != null ? playerTransform.position : transform.position);
        if (autoFoundEnemy != null)
        {
            enemy = autoFoundEnemy;
            RefreshEnemyCaches();
            return enemy;
        }

        return null;
    }

    private void RefreshEnemyCaches()
    {
        Transform enemyRoot = enemy != null ? enemy : transform;
        advancedEnemy = enemyRoot.GetComponentInParent<AdvancedEnemyAI>();
        enemyTest = enemyRoot.GetComponentInParent<Enemytest>();
    }

    private bool CanFinishCurrentEnemy()
    {
        RefreshEnemyCaches();

        if (advancedEnemy != null)
            return advancedEnemy.CanBeFinished();

        if (enemyTest != null)
            return enemyTest.CanBeFinished();

        return true;
    }

    private Transform FindClosestFinishableEnemy(Vector3 origin)
    {
        Collider[] hits = Physics.OverlapSphere(origin, Mathf.Max(0.5f, autoFindEnemyRadius), enemyLayerMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        Transform best = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            AdvancedEnemyAI advanced = hit.GetComponentInParent<AdvancedEnemyAI>();
            if (advanced != null)
            {
                if (!advanced.CanBeFinished())
                    continue;

                float distance = Vector3.Distance(origin, advanced.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = advanced.transform;
                }

                continue;
            }

            Enemytest test = hit.GetComponentInParent<Enemytest>();
            if (test == null || !test.CanBeFinished())
                continue;

            float testDistance = Vector3.Distance(origin, test.transform.position);
            if (testDistance < bestDistance)
            {
                bestDistance = testDistance;
                best = test.transform;
            }
        }

        return best;
    }
}
