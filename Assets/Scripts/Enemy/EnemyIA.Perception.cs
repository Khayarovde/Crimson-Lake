using UnityEngine;
using UnityEngine.AI;

public partial class AdvancedEnemyAI
{
    private void CheckForPlayer()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool canSee = dist <= viewRadius && InSightCone(player.position) && HasLineOfSight(player.position);

        if (canSee)
        {
            playerLastPosition = player.position;
            isScanning = false;
            StopSearch();

            if (IsMovementDisabled())
            {
                SetChasingState(false);
                StopAgentMovement();
                return;
            }

            if (!isChasing)
            {
                SetChasingState(true);
            }
            isRandomPatrolling = false;

            if (navMeshAgent.enabled)
            {
                float desiredDistance = Mathf.Max(stopBeforePlayerDistance, attackRange + 0.35f);
                navMeshAgent.stoppingDistance = Mathf.Max(navMeshAgent.stoppingDistance, desiredDistance);
                navMeshAgent.speed = Mathf.Max(0.1f, speedWalk);

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
        else if (isChasing)
        {
            if (IsMovementDisabled())
            {
                SetChasingState(false);
                StopAgentMovement();
                return;
            }

            if (isSearching)
            {
                UpdateSearch();
                return;
            }

            if (navMeshAgent.enabled)
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


    private bool InSightCone(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        return Vector3.Angle(transform.forward, dir) <= viewAngle / 2f;
    }

    private bool HasLineOfSight(Vector3 targetPos)
    {
        Vector3 origin = transform.position + sightOriginOffset;
        Vector3 target = targetPos + sightTargetOffset;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir.normalized, dist, lineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
            return false;

        Transform hitTransform = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].transform;
            if (t == transform || t.IsChildOf(transform))
                continue;
            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward * viewRadius;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward * viewRadius;

        Gizmos.DrawRay(transform.position, left);
        Gizmos.DrawRay(transform.position, right);
    }
}
