using UnityEngine;
using System.Collections;

public partial class AdvancedEnemyAI
{
    public bool CanBeFinished()
    {
        return isStunned && !isDead;
    }

    public void ApplyStun(float duration)
    {
        if (isDead || isPermanentlyDead)
            return;

        isStunned = true;
        isWakingUp = false;
        isAttacking = false;
        SetState(EnemyState.Stunned);

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        StopAgentMovement();

        if (m_Animator != null)
            m_Animator.SetBool("IsStunned", true);

        ForceStunAnimatorState();
        PlayStateWithFallback(stunStateName, stunAnimLayer);

        if (m_Animator != null)
        {
            m_Animator.SetLayerWeight(stunAnimLayer, 1f);
            m_Animator.SetLayerWeight(baseAnimLayer, 0f);
        }

        if (stunRoutine != null)
            StopCoroutine(stunRoutine);

        stunRoutine = StartCoroutine(RevertFromStun(Mathf.Max(0.1f, duration)));
    }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f || isDead || isPermanentlyDead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
            Die();
    }

    public void KillDuringStun()
    {
        if (!CanBeFinished())
            return;

        Die();
    }

    public void Burn()
    {
        if (!isDead)
            return;

        isPermanentlyDead = true;

        if (resurrectionRoutine != null)
        {
            StopCoroutine(resurrectionRoutine);
            resurrectionRoutine = null;
        }

        if (destroyOnBurn)
            Destroy(gameObject);
    }

    private IEnumerator RevertFromStun(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (isDead || isPermanentlyDead)
            yield break;

        isStunned = false;
        isWakingUp = true;
        PlayStateWithFallback(wakeUpStateName, wakeUpAnimLayer);

        if (m_Animator != null)
        {
            m_Animator.SetLayerWeight(baseAnimLayer, 0f);
            m_Animator.SetLayerWeight(stunAnimLayer, 0f);
            m_Animator.SetLayerWeight(wakeUpAnimLayer, 1f);
        }

        yield return new WaitForSeconds(Mathf.Max(0.1f, wakeUpDuration));
        if (isDead || isPermanentlyDead)
            yield break;

        isWakingUp = false;
        isAttacking = false;
        SetState(EnemyState.Patrol);
        ResumeAgentMovement();

        if (m_Animator != null)
        {
            m_Animator.SetLayerWeight(wakeUpAnimLayer, 0f);
            m_Animator.SetLayerWeight(baseAnimLayer, 1f);
            m_Animator.SetBool("IsStunned", false);
        }

        stunRoutine = null;
    }

    private void ForceStunAnimatorState()
    {
        if (m_Animator == null)
            return;

        m_Animator.speed = baseAnimatorSpeed;
    }

    private void Die()
    {
        if (isDead || isPermanentlyDead)
            return;

        isDead = true;
        isStunned = false;
        isWakingUp = false;
        isAttacking = false;
        caughtPlayer = false;
        SetState(EnemyState.Dead);

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        StopAgentMovement();
        SetPlayerCollisionIgnored(false);

        if (enemyCollider != null)
            enemyCollider.enabled = false;

        if (navMeshAgent != null && navMeshAgent.enabled)
            navMeshAgent.enabled = false;

        if (m_Animator != null)
        {
            m_Animator.SetBool("IsStunned", false);
            m_Animator.speed = baseAnimatorSpeed;
            PlayStateWithFallback(deathStateName, baseAnimLayer);
        }

        if (resurrectionRoutine != null)
            StopCoroutine(resurrectionRoutine);

        if (reviveEnabled)
            resurrectionRoutine = StartCoroutine(ResurrectionSequence());
    }

    private IEnumerator ResurrectionSequence()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, deathDuration));

        if (m_Animator != null && !string.IsNullOrEmpty(deathEndStateName))
            PlayStateWithFallback(deathEndStateName, baseAnimLayer);

        yield return new WaitForSeconds(Mathf.Max(0.1f, deathEndDuration));

        if (isPermanentlyDead)
            yield break;

        float delay = Random.Range(Mathf.Min(reviveDelayMin, reviveDelayMax), Mathf.Max(reviveDelayMin, reviveDelayMax));
        yield return new WaitForSeconds(delay);

        if (isPermanentlyDead)
            yield break;

        Revive();
    }

    private void Revive()
    {
        if (!isDead || isPermanentlyDead)
            return;

        isDead = false;
        isStunned = false;
        isWakingUp = false;
        isAttacking = false;
        caughtPlayer = false;
        currentAnimState = null;

        float minHp = Mathf.Clamp01(reviveHealthPercentMin);
        float maxHp = Mathf.Clamp01(reviveHealthPercentMax);
        if (maxHp < minHp)
        {
            float tmp = minHp;
            minHp = maxHp;
            maxHp = tmp;
        }

        currentHealth = maxHealth * Random.Range(minHp, maxHp);

        if (enemyCollider != null)
            enemyCollider.enabled = true;

        if (navMeshAgent != null && !navMeshAgent.enabled)
            navMeshAgent.enabled = true;

        if (navMeshAgent != null)
        {
            navMeshAgent.Warp(transform.position);
            navMeshAgent.speed = Mathf.Max(0f, speedWalk);
            navMeshAgent.stoppingDistance = Mathf.Max(navMeshAgent.stoppingDistance, stopBeforePlayerDistance);
            navMeshAgent.isStopped = false;
        }

        if (m_Animator != null)
        {
            m_Animator.speed = baseAnimatorSpeed;
            m_Animator.SetBool("IsStunned", false);
            PlayStateWithFallback(idleStateName, baseAnimLayer);
        }

        SetState(EnemyState.Patrol);
        BeginPatrol();
        resurrectionRoutine = null;
    }
}
