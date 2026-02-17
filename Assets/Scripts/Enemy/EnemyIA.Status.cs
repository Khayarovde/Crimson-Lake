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
        if (isDead) return;
        isStunned = true;
        isWakingUp = false;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        StopAgentMovement();
        m_Animator.SetBool("IsStunned", true);

        ForceStunAnimatorState();
        PlayStateWithFallback(stunStateName, stunAnimLayer);

        m_Animator.SetLayerWeight(stunAnimLayer, 1f);
        m_Animator.SetLayerWeight(baseAnimLayer, 0f);
        StartCoroutine(RevertFromStun(duration));
    }

    private IEnumerator RevertFromStun(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (isDead) yield break;
        isStunned = false;
        isWakingUp = true;
        PlayStateWithFallback(wakeUpStateName, wakeUpAnimLayer);
        m_Animator.SetLayerWeight(baseAnimLayer, 0f);
        m_Animator.SetLayerWeight(stunAnimLayer, 0f);
        m_Animator.SetLayerWeight(wakeUpAnimLayer, 1f);

        yield return new WaitForSeconds(Mathf.Max(0.1f, wakeUpDuration));
        isWakingUp = false;
        isAttacking = false;
        ResumeAgentMovement();
        m_Animator.SetLayerWeight(wakeUpAnimLayer, 0f);
        m_Animator.SetLayerWeight(baseAnimLayer, 1f);
        m_Animator.SetBool("IsStunned", false);
    }

    private void ForceStunAnimatorState()
    {
        if (m_Animator == null) return;
    }

    public void KillDuringStun()
    {
        if (!CanBeFinished()) return;

        isDead = true;
        isStunned = false;
        isWakingUp = false;
        isAttacking = false;
        caughtPlayer = false;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        StopAllCoroutines();
        StopAgentMovement();

        if (m_Animator != null)
        {
            m_Animator.SetBool("IsStunned", false);
            PlayStateWithFallback(deathStateName, baseAnimLayer);
        }

        StartCoroutine(DeathAndMaybeReviveSequence());
    }

    private IEnumerator DeathAndMaybeReviveSequence()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, deathDuration));

        if (m_Animator != null)
            PlayStateWithFallback(deathEndStateName, baseAnimLayer);

        yield return new WaitForSeconds(Mathf.Max(0.1f, deathEndDuration));

        if (m_Animator != null)
            m_Animator.speed = 0f;

        if (!reviveEnabled) yield break;

        float delay = Random.Range(Mathf.Min(reviveDelayMin, reviveDelayMax), Mathf.Max(reviveDelayMin, reviveDelayMax));
        yield return new WaitForSeconds(delay);
        Revive();
    }

    private void Revive()
    {
        if (!isDead) return;

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

        if (m_Animator != null)
        {
            m_Animator.speed = baseAnimatorSpeed;
            m_Animator.SetBool("IsStunned", false);
        }

        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.speed = speedWalk;
        }

        isPatrolling = true;
        isChasing = false;
        ResumeAgentMovementAndRepath();
    }
}
