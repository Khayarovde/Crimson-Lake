using UnityEngine;
using UnityEngine.AI;

public partial class AdvancedEnemyAI
{
    private bool IsMovementDisabled()
    {
        return disableMovement;
    }

    private void SetChasingState(bool chasing)
    {
        SetState(chasing ? EnemyState.Chase : EnemyState.Patrol);
    }

    private void StopChasing()
    {
        SetChasingState(false);
        if (navMeshAgent == null) return;
        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }
        if (navMeshAgent.enabled)
            BeginPatrol();
    }

    private void BeginPatrol()
    {
        SetState(EnemyState.Patrol);

        if (IsMovementDisabled())
        {
            StopAgentMovement();
            isRandomPatrolling = false;
            return;
        }

        if (waypoints != null && waypoints.Length > 0)
        {
            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Length - 1);
            MoveToCurrentWaypoint();
            return;
        }

        isRandomPatrolling = true;
        PickRandomPatrolPoint();
    }

    private void UpdatePatrol()
    {
        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }

        if (navMeshAgent == null || !navMeshAgent.enabled) return;

        if (waypoints != null && waypoints.Length > 0)
        {
            if (Time.time < randomPatrolWaitEndTime)
                return;

            if (HasReachedDestination(randomPatrolPointTolerance))
            {
                randomPatrolWaitEndTime = Time.time + Mathf.Max(0f, waypointPauseTime);
                AdvanceWaypointIndex();
                MoveToCurrentWaypoint();
            }
            return;
        }

        if (!isRandomPatrolling)
            return;

        if (Time.time < randomPatrolWaitEndTime)
            return;

        if (HasReachedDestination(randomPatrolPointTolerance))
        {
            randomPatrolWaitEndTime = Time.time + Mathf.Max(0f, randomPatrolWait);
            PickRandomPatrolPoint();
        }
    }

    private void MoveToCurrentWaypoint()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
            return;

        if (waypoints == null || waypoints.Length == 0)
            return;

        Transform point = waypoints[currentWaypointIndex];
        if (point == null)
            return;

        navMeshAgent.isStopped = false;
        navMeshAgent.speed = Mathf.Max(0f, speedWalk);
        navMeshAgent.SetDestination(point.position);
    }

    private void AdvanceWaypointIndex()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        int next = currentWaypointIndex + 1;
        if (next >= waypoints.Length)
            next = loopPatrol ? 0 : waypoints.Length - 1;

        currentWaypointIndex = next;
    }

    private void PickRandomPatrolPoint()
    {
        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }

        Vector2 rand = Random.insideUnitCircle * Mathf.Max(1f, randomPatrolRadius);
        Vector3 candidate = transform.position + new Vector3(rand.x, 0f, rand.y);
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, randomPatrolRadius, NavMesh.AllAreas))
            currentRandomPatrolPoint = hit.position;
        else
            currentRandomPatrolPoint = transform.position;

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
        if (Time.time < nextMovementAnimSwitchTime) return;

        float speed = navMeshAgent != null ? navMeshAgent.velocity.magnitude : 0f;
        bool shouldWalk = movementAnimIsWalking
            ? speed > Mathf.Max(0.01f, movementAnimStopSpeed)
            : speed >= Mathf.Max(movementAnimStopSpeed + 0.01f, movementAnimStartSpeed);

        if (shouldWalk != movementAnimIsWalking)
        {
            movementAnimIsWalking = shouldWalk;
            nextMovementAnimSwitchTime = Time.time + Mathf.Max(0f, movementAnimSwitchCooldown);
        }

        if (movementAnimIsWalking)
            PlayState(walkingStateName, baseAnimLayer);
        else
            PlayState(idleStateName, baseAnimLayer);
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
        if (navMeshAgent == null || !navMeshAgent.enabled) return;
        navMeshAgent.isStopped = true;
        if (navMeshAgent.hasPath)
            navMeshAgent.ResetPath();
        if (navMeshAgent.velocity.sqrMagnitude > 0.0001f)
            navMeshAgent.velocity = Vector3.zero;
    }

    private void ResumeAgentMovement()
    {
        if (navMeshAgent == null) return;
        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }
        navMeshAgent.isStopped = false;
    }

    private void EnsureAgentActiveForAttack()
    {
        if (navMeshAgent == null) return;
        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }
        navMeshAgent.isStopped = false;
    }

    private void ResumeAgentMovementAndRepath()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled) return;
        if (isStunned || caughtPlayer || isDead) return;

        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }

        navMeshAgent.isStopped = false;

        if (isChasing && player != null)
        {
            float desiredDistance = Mathf.Max(stopBeforePlayerDistance, attackRange + 0.35f);
            navMeshAgent.SetDestination(GetApproachDestination(desiredDistance));
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
        EnsureInitialized();

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("Не найден игрок с тегом 'Player' для запуска преследования!");
                return;
            }
        }

        if (navMeshAgent == null)
        {
            Debug.LogWarning("NavMeshAgent не найден: запуск преследования невозможен.");
            return;
        }

        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }

        playerLastPosition = player.position;
        SetChasingState(true);

        if (navMeshAgent.enabled)
        {
            float desiredDistance = Mathf.Max(stopBeforePlayerDistance, attackRange + 0.35f);
            navMeshAgent.SetDestination(GetApproachDestination(desiredDistance));
        }

        Debug.Log("Враг активирован и начал преследование игрока после взятия дискеты!");
    }
}
