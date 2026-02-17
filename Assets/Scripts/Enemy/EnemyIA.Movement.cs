using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public partial class AdvancedEnemyAI
{
    private void StopChasing()
    {
        isChasing = false;
        isPatrolling = true;
        navMeshAgent.speed = patrolSpeed;
        if (navMeshAgent.enabled)
            BeginPatrol();
    }

    private void BeginPatrol()
    {
        if (HasWaypoints())
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            if (navMeshAgent.enabled)
                navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
            isRandomPatrolling = false;
            return;
        }

        if (useRandomPatrolWhenNoWaypoints)
        {
            isRandomPatrolling = true;
            PickRandomPatrolPoint();
        }
    }

    private void UpdatePatrol()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled) return;

        if (HasWaypoints())
        {
            if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && !navMeshAgent.pathPending)
                BeginPatrol();
            return;
        }

        if (!useRandomPatrolWhenNoWaypoints)
            return;

        if (Time.time < randomPatrolWaitEndTime)
            return;

        if (HasReachedDestination(randomPatrolPointTolerance))
        {
            randomPatrolWaitEndTime = Time.time + Mathf.Max(0f, randomPatrolWait);
            PickRandomPatrolPoint();
        }
    }

    private bool HasWaypoints()
    {
        return waypoints != null && waypoints.Length > 0;
    }

    private void PickRandomPatrolPoint()
    {
        Vector2 rand = Random.insideUnitCircle * Mathf.Max(1f, randomPatrolRadius);
        Vector3 candidate = transform.position + new Vector3(rand.x, 0f, rand.y);
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, randomPatrolRadius, NavMesh.AllAreas))
            currentRandomPatrolPoint = hit.position;
        else
            currentRandomPatrolPoint = transform.position;

        navMeshAgent.speed = patrolSpeed;
        navMeshAgent.SetDestination(currentRandomPatrolPoint);
    }

    private bool IsCloseToPlayer()
    {
        if (player == null) return false;
        float range = attackRange;
        if (navMeshAgent != null)
            range = Mathf.Max(range, navMeshAgent.stoppingDistance + 0.1f);
        return Vector3.Distance(transform.position, player.position) <= range;
    }

    private void UpdateMovementAnimation()
    {
        if (m_Animator == null) return;
        if (isAttacking || isStunned || isWakingUp) return;

        if (navMeshAgent != null && navMeshAgent.velocity.magnitude > 0.1f)
            PlayState(walkingStateName, baseAnimLayer);
    }

    private void PlayState(string stateName, int layer)
    {
        if (m_Animator == null) return;
        if (string.IsNullOrEmpty(stateName)) return;
        if (stateName == currentAnimState) return;
        if (!HasState(m_Animator, layer, stateName)) return;

        m_Animator.CrossFadeInFixedTime(stateName, animTransition, layer);
        currentAnimState = stateName;
    }

    private void StopAgentMovement()
    {
        if (navMeshAgent == null) return;
        navMeshAgent.isStopped = true;
        navMeshAgent.ResetPath();
        navMeshAgent.velocity = Vector3.zero;
    }

    private void ResumeAgentMovement()
    {
        if (navMeshAgent == null) return;
        navMeshAgent.isStopped = false;
    }

    private void EnsureAgentActiveForAttack()
    {
        if (navMeshAgent == null) return;
        navMeshAgent.isStopped = false;
    }

    private void BeginAttackSpeedSlowdown()
    {
        if (navMeshAgent == null) return;
        if (attackSpeedRoutine != null) StopCoroutine(attackSpeedRoutine);
        float target = baseNavSpeed * Mathf.Clamp01(attackMoveSpeedMultiplier);
        attackSpeedRoutine = StartCoroutine(SmoothAgentSpeed(target));
    }

    private void EndAttackSpeedSlowdown()
    {
        if (navMeshAgent == null) return;
        if (attackSpeedRoutine != null) StopCoroutine(attackSpeedRoutine);
        float target = isChasing ? speedRun : speedWalk;
        attackSpeedRoutine = StartCoroutine(SmoothAgentSpeed(target));
    }

    private IEnumerator SmoothAgentSpeed(float targetSpeed)
    {
        if (navMeshAgent == null) yield break;

        float t = 0f;
        float start = navMeshAgent.speed;
        float duration = 1f / Mathf.Max(0.01f, attackSpeedLerp);
        while (t < duration)
        {
            t += Time.deltaTime;
            navMeshAgent.speed = Mathf.Lerp(start, targetSpeed, t / duration);
            yield return null;
        }

        navMeshAgent.speed = targetSpeed;
    }

    private void ResumeAgentMovementAndRepath()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled) return;
        if (isStunned || caughtPlayer || isDead) return;

        navMeshAgent.isStopped = false;

        if (isChasing && player != null)
        {
            navMeshAgent.SetDestination(player.position);
        }
        else if (isPatrolling)
        {
            BeginPatrol();
        }
    }

    private void PlayStateWithFallback(string stateName, int preferredLayer)
    {
        if (m_Animator == null || string.IsNullOrEmpty(stateName)) return;
        if (HasState(m_Animator, preferredLayer, stateName))
        {
            PlayState(stateName, preferredLayer);
            return;
        }

        if (HasState(m_Animator, baseAnimLayer, stateName))
            PlayState(stateName, baseAnimLayer);
    }

    private static bool HasState(Animator animator, int layer, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;
        return animator.HasState(layer, Animator.StringToHash(stateName));
    }

    public void StartChasingAfterDiskette()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("Не найден игрок с тегом 'Player' для запуска преследования!");
                return;
            }
        }

        isPatrolling = false;
        isChasing = true;
        navMeshAgent.speed = chaseSpeed;

        if (navMeshAgent.enabled)
            navMeshAgent.SetDestination(player.position);

        m_Animator.SetBool("isChasing", true);

        Debug.Log("Враг активирован и начал преследование игрока после взятия дискеты!");
    }
}
