using UnityEngine;
using UnityEngine.AI;

public partial class AdvancedEnemyAI
{
    private void BeginScan()
    {
        isScanning = true;
        scanEndTime = Time.time + Mathf.Max(0.1f, scanDuration);
        StopAgentMovement();
    }

    private void UpdateScan()
    {
        if (!isScanning) return;

        transform.Rotate(0f, scanTurnSpeed * Time.deltaTime, 0f);
        if (Time.time >= scanEndTime)
        {
            isScanning = false;
            if (!isChasing)
                BeginPatrol();
        }
    }

    private void BeginSearch()
    {
        if (IsMovementDisabled())
        {
            StopSearch();
            StopAgentMovement();
            return;
        }

        isSearching = true;
        currentSearchIndex = 0;
        searchWaitEndTime = 0f;
        searchPoints = BuildSearchPoints(playerLastPosition);
        if (navMeshAgent != null)
            navMeshAgent.speed = Mathf.Max(0.1f, speedWalk);
        MoveToSearchPoint();
    }

    private void StopSearch()
    {
        isSearching = false;
        searchPoints = null;
        currentSearchIndex = 0;
        searchWaitEndTime = 0f;
        if (navMeshAgent != null && !IsMovementDisabled())
            navMeshAgent.speed = Mathf.Max(0.1f, speedWalk);
    }

    private void UpdateSearch()
    {
        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }

        if (!isSearching || navMeshAgent == null || !navMeshAgent.enabled)
            return;

        if (Time.time < searchWaitEndTime)
            return;

        if (HasReachedDestination(searchPointTolerance))
        {
            searchWaitEndTime = Time.time + Mathf.Max(0f, searchPointWait);
            currentSearchIndex++;
            MoveToSearchPoint();
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);
            if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && !navMeshAgent.pathPending)
                MoveToSearchPoint();
        }
    }

    private void MoveToSearchPoint()
    {
        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }

        if (searchPoints == null || searchPoints.Length == 0)
        {
            StopChasing();
            return;
        }

        if (currentSearchIndex >= searchPoints.Length)
        {
            StopSearch();
            StopChasing();
            return;
        }

        navMeshAgent.SetDestination(searchPoints[currentSearchIndex]);
    }

    private Vector3[] BuildSearchPoints(Vector3 center)
    {
        int count = Mathf.Max(1, searchPointsCount);
        Vector3[] points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 rand = Random.insideUnitCircle * Mathf.Max(0.5f, searchRadius);
            Vector3 candidate = center + new Vector3(rand.x, 0f, rand.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
                points[i] = hit.position;
            else
                points[i] = center;
        }

        return points;
    }

    private bool HasReachedDestination(float tolerance)
    {
        if (navMeshAgent == null) return false;
        if (navMeshAgent.pathPending) return false;
        return navMeshAgent.remainingDistance <= Mathf.Max(tolerance, navMeshAgent.stoppingDistance);
    }
}
