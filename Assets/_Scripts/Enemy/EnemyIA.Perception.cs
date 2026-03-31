using UnityEngine;
using UnityEngine.AI;

public partial class AdvancedEnemyAI
{
    private void CheckForPlayer()
    {
        Transform detectedPlayer = FindPlayerInFov();
        bool canSee = detectedPlayer != null;

        if (canSee)
        {
            player = detectedPlayer;
            player.TryGetComponent(out playerHealth);
            CachePlayerCollider();

            float dist = Vector3.Distance(transform.position, player.position);
            playerLastPosition = player.position;
            isScanning = false;
            StopSearch();

            if (IsMovementDisabled())
            {
                SetState(EnemyState.Patrol);
                StopAgentMovement();
                return;
            }

            if (currentState == EnemyState.Patrol)
            {
                BeginAlert(player.position);
                return;
            }

            if (currentState == EnemyState.Alert)
                return;

            SetState(EnemyState.Chase);
            isRandomPatrolling = false;

            if (navMeshAgent != null && navMeshAgent.enabled)
            {
                float desiredDistance = Mathf.Max(stopBeforePlayerDistance, attackRange + 0.35f);
                navMeshAgent.stoppingDistance = Mathf.Max(navMeshAgent.stoppingDistance, desiredDistance);

                if (dist <= desiredDistance)
                {
                    StopAgentMovement();
                    if (facePlayerOnAttack)
                        FacePlayerOnY();
                }
                else
                {
                    ResumeAgentMovement();
                    navMeshAgent.SetDestination(GetApproachDestination(desiredDistance));
                }
            }
        }
        else if (currentState == EnemyState.Chase)
        {
            if (IsMovementDisabled())
            {
                SetState(EnemyState.Patrol);
                StopAgentMovement();
                return;
            }

            if (isSearching)
            {
                UpdateSearch();
                return;
            }

            if (navMeshAgent != null && navMeshAgent.enabled)
            {
                if (Time.time >= nextRepathTime)
                {
                    nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);
                    navMeshAgent.SetDestination(playerLastPosition);
                }

                if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
                    BeginSearch();
            }
        }
    }

    private Transform FindPlayerInFov()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position, Mathf.Max(viewRadius, peripheralViewRadius), playerDetectionHits, playerDetectionMask, QueryTriggerInteraction.Collide);
        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits; i++)
        {
            Collider hit = playerDetectionHits[i];
            if (hit == null)
                continue;

            Transform candidate = hit.transform;
            Transform root = candidate.root;
            if (root == null || !root.CompareTag("Player"))
                continue;

            float dist = Vector3.Distance(transform.position, root.position);
            bool inMainVision = dist <= viewRadius && InSightCone(root.position, viewAngle);
            bool inPeripheralVision = dist <= peripheralViewRadius && InSightCone(root.position, peripheralViewAngle);
            bool inCloseAwareness = dist <= closeAwarenessRadius;
            if (!(inMainVision || inPeripheralVision || inCloseAwareness))
                continue;

            if (!HasLineOfSight(root.position))
                continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = root;
            }
        }

        return best;
    }


    private bool InSightCone(Vector3 targetPos, float angle)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        return Vector3.Angle(transform.forward, dir) <= Mathf.Clamp(angle, 1f, 360f) * 0.5f;
    }

    private bool HasLineOfSight(Vector3 targetPos)
    {
        Vector3 origin = transform.position + sightOriginOffset;
        Vector3 target = targetPos + sightTargetOffset;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;

        int hitCount = Physics.RaycastNonAlloc(origin, dir.normalized, lineOfSightHits, dist, lineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0)
            return false;

        if (hitCount >= lineOfSightHits.Length)
        {
            if (Physics.Raycast(origin, dir.normalized, out RaycastHit firstHit, dist, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                Transform firstTransform = firstHit.transform;
                if (firstTransform != null)
                    return firstTransform == player || firstTransform.IsChildOf(player);
            }
        }

        Transform hitTransform = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Transform t = lineOfSightHits[i].transform;
            if (t == transform || t.IsChildOf(transform))
                continue;
            if (lineOfSightHits[i].distance < bestDist)
            {
                bestDist = lineOfSightHits[i].distance;
                hitTransform = t;
            }
        }

        if (hitTransform == null)
            return false;

        return hitTransform == player || hitTransform.IsChildOf(player);
    }

    private Vector3 GetApproachDestination(float desiredDistance)
    {
        if (player == null)
            return transform.position;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return player.position;

        Vector3 destination = player.position - toPlayer.normalized * Mathf.Max(0.1f, desiredDistance);
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            return hit.position;

        return destination;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, closeAwarenessRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        Gizmos.color = new Color(1f, 0.55f, 0f);
        Gizmos.DrawWireSphere(transform.position, peripheralViewRadius);

        Vector3 pLeft = Quaternion.Euler(0, -peripheralViewAngle / 2f, 0) * transform.forward * peripheralViewRadius;
        Vector3 pRight = Quaternion.Euler(0, peripheralViewAngle / 2f, 0) * transform.forward * peripheralViewRadius;
        Gizmos.DrawRay(transform.position, pLeft);
        Gizmos.DrawRay(transform.position, pRight);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward * viewRadius;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward * viewRadius;

        Gizmos.DrawRay(transform.position, left);
        Gizmos.DrawRay(transform.position, right);

        if (waypoints != null && waypoints.Length > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Length; i++)
            {
                Transform wp = waypoints[i];
                if (wp == null)
                    continue;

                Gizmos.DrawSphere(wp.position, 0.15f);
                int next = i + 1;
                if (next < waypoints.Length && waypoints[next] != null)
                    Gizmos.DrawLine(wp.position, waypoints[next].position);
            }

            if (loopPatrol && waypoints.Length > 1 && waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
                Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }
    }
}
