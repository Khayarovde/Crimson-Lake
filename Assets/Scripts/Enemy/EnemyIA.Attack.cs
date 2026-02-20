using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public partial class AdvancedEnemyAI
{
    private void AttackPlayer()
    {
        if (isAttacking) return;
        if (isStunned) return;
        if (isWakingUp) return;
        if (isDead) return;
        if (playerHealth == null || playerHealth.IsDead) return;
        if (Time.time < nextAttackTime) return;
        if (!IsFacingPlayer()) return;

        nextAttackTime = Time.time + attackCooldown;
        attackRoutine = StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        isAttacking = true;

        if (isStunned)
        {
            isAttacking = false;
            yield break;
        }

        BeginAttackSpeedSlowdown();
        StopAgentMovement();

        if (facePlayerOnAttack)
            FacePlayerOnY();
        SetPlayerCollisionIgnored(true);

        float animDuration = Mathf.Max(0.01f, attackAnimationDuration);
        float windup = Mathf.Clamp(attackWindupTime, 0f, animDuration);
        if (windup > 0f)
            yield return new WaitForSecondsRealtime(windup);

        if (!IsCloseToPlayer())
        {
            CleanupAttack(interrupted: true);
            yield break;
        }

        int attackIndex = PickAttackIndex();
        if (m_Animator != null)
            PlayAttackAnimation(attackIndex);

        float hitTime = Mathf.Clamp01(attackHitNormalizedTime) * animDuration;
        float elapsed = 0f;
        while (elapsed < hitTime)
        {
            if (!IsCloseToPlayer())
            {
                CleanupAttack(interrupted: true);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!caughtPlayer && !isStunned && IsCloseToPlayer())
            TryDealDamage();

        float remainingAnim = Mathf.Max(0f, animDuration - hitTime);
        if (remainingAnim > 0f)
            yield return new WaitForSecondsRealtime(remainingAnim);

        if (playerHealth != null && playerHealth.IsDead)
        {
            caughtPlayer = true;
            if (m_Animator != null) m_Animator.SetBool("IsCaughtPlayer", true);
            if (catchSound != null && audioSource != null) audioSource.PlayOneShot(catchSound);
        }

        float totalLock = Mathf.Max(attackLockTime, animDuration);
        float remainingLock = Mathf.Max(0f, totalLock - animDuration);
        if (remainingLock > 0f)
            yield return new WaitForSecondsRealtime(remainingLock);

        if (m_Animator != null)
            m_Animator.speed = baseAnimatorSpeed;
        if (!caughtPlayer) ResumeAgentMovement();

        CleanupAttack(interrupted: false);
    }

    private void CleanupAttack(bool interrupted)
    {
        EndAttackSpeedSlowdown();
        SetPlayerCollisionIgnored(false);

        if (!caughtPlayer)
            ResumeAgentMovementAndRepath();

        currentAnimState = null;
        isAttacking = false;
        attackRoutine = null;
    }

    private IEnumerator PostAttackSideStep()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || player == null)
            yield break;

        float duration = Mathf.Max(0.05f, postAttackSideStepDuration);
        float distance = Mathf.Max(0.1f, postAttackSideStepDistance);
        float speed = Mathf.Max(0.1f, postAttackSideStepSpeed);

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            yield break;

        float dir = Random.value < 0.5f ? -1f : 1f;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized) * dir;
        Vector3 target = transform.position + side * distance;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, distance, NavMesh.AllAreas))
            target = hit.position;

        bool cachedUpdateRotation = navMeshAgent.updateRotation;
        navMeshAgent.updateRotation = false;
        navMeshAgent.speed = speed;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(target);

        float endTime = Time.time + duration;
        while (Time.time < endTime)
        {
            FacePlayerOnY();
            yield return null;
        }

        navMeshAgent.updateRotation = cachedUpdateRotation;
    }


    private void PlayAttackAnimation(int attackIndex)
    {
        if (m_Animator == null) return;

        m_Animator.speed = baseAnimatorSpeed * Mathf.Max(0.1f, attackAnimationSpeed);

        string stateName = GetAttackStateName(attackIndex);
        if (HasState(m_Animator, baseAnimLayer, stateName))
            PlayState(stateName, baseAnimLayer);
    }

    private int PickAttackIndex()
    {
        float w1 = Mathf.Max(0f, attack1Weight);
        float w2 = Mathf.Max(0f, attack2Weight);
        float w3 = Mathf.Max(0f, attack3Weight);
        float total = w1 + w2 + w3;

        if (total <= 0f)
            return 0;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            float roll = Random.Range(0f, total);
            int index;
            if (roll < w1) index = 0;
            else if (roll < w1 + w2) index = 1;
            else index = 2;

            if (index != lastAttackIndex || total == (index == 0 ? w1 : index == 1 ? w2 : w3))
            {
                lastAttackIndex = index;
                return index;
            }
        }

        lastAttackIndex = (lastAttackIndex + 1) % 3;
        return lastAttackIndex;
    }

    private string GetAttackStateName(int attackIndex)
    {
        switch (attackIndex)
        {
            case 1: return "Attack2";
            case 2: return "Attack3";
            default: return "Attack";
        }
    }

    private void TryDealDamage()
    {
        var ph = GetPlayerHealth();
        if (ph == null || ph.IsDead) return;
        if (!IsPlayerInHitRange()) return;
        ph.TakeEnemyHit(this);
    }

    private bool IsPlayerInHitRange()
    {
        if (player == null) return false;
        if (!IsFacingPlayer()) return false;

        Vector3 center = transform.position + transform.forward * attackHitForwardOffset;
        float radius = Mathf.Max(0.01f, attackHitRadius);

        if (playerCollider != null)
        {
            Vector3 closest = playerCollider.ClosestPoint(center);
            return (closest - center).sqrMagnitude <= radius * radius;
        }

        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            if (h == null) continue;
            if (h.transform == player || h.transform.IsChildOf(player))
                return true;
        }

        return false;
    }

    private bool IsFacingPlayer()
    {
        if (player == null) return false;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return false;
        toPlayer.Normalize();
        float dot = Vector3.Dot(transform.forward, toPlayer);
        float cosLimit = Mathf.Cos(maxAttackAngle * Mathf.Deg2Rad);
        return dot >= cosLimit;
    }

    private void FacePlayerOnY()
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private PlayerHealth GetPlayerHealth()
    {
        if (playerHealth != null) return playerHealth;
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
        return playerHealth;
    }

}
