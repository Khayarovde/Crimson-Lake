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
            if (!isChasing)
            {
                isPatrolling = false;
                isChasing = true;
            }
            isRandomPatrolling = false;
            if (navMeshAgent.enabled)
            {
                if (!isAttacking)
                    UpdateChaseSpeed(dist);


                if (dist <= Mathf.Max(stopBeforePlayerDistance, attackRange))
                {
                    StopAgentMovement();
                    if (facePlayerOnAttack)
                        FacePlayerOnY();
                }
                else
                {
                    ResumeAgentMovement();
                    navMeshAgent.SetDestination(player.position);
                }
            }
        }
        else if (isChasing)
        {
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

    private void UpdateChaseSpeed(float distance)
    {
        if (navMeshAgent == null) return;

        float minSpeed = Mathf.Max(0.1f, patrolSpeed * Mathf.Clamp01(chaseCloseSpeedMultiplier));
        float targetApproach = Mathf.Max(minSpeed, approachSpeed);
        float targetRun = Mathf.Max(targetApproach, chaseSpeed);

        float accelDistance = Mathf.Max(0f, approachDistance);
        float t = accelDistance > 0f
            ? Mathf.InverseLerp(attackRange, accelDistance, distance)
            : 0f;
        t = Mathf.Clamp01(t);
        t = Mathf.Pow(t, Mathf.Max(0.01f, chaseSpeedFalloffPower));

        navMeshAgent.speed = Mathf.Lerp(targetApproach, targetRun, t);
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
