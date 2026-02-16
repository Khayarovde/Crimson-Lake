﻿using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class AdvancedEnemyAI : MonoBehaviour
{
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private float speedWalk = 6f;
    [SerializeField] private float speedRun = 9f;

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;

    [Header("Detection")]
    [SerializeField] private float viewRadius = 15f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField, Tooltip("Дистанция остановки перед игроком, чтобы не толкать")]
    private float stopBeforePlayerDistance = 1.2f;

    [Header("Catch Settings")]
    [SerializeField] private AudioClip catchSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField, Tooltip("Задержка от старта удара до нанесения урона (реальное время)")]
    private float attackWindupTime = 0.25f;
    [SerializeField, Tooltip("Когда именно в анимации удара должен быть урон (сек)")]
    private float attackHitDelay = 1.06f;
    [SerializeField, Tooltip("Длительность анимации удара (сек)")]
    private float attackAnimationDuration = 2.12f;
    [SerializeField, Tooltip("Сколько времени враг стоит на месте во время удара (реальное время)")]
    private float attackLockTime = 0.6f;
    [SerializeField, Tooltip("Множитель скорости анимации на время удара/захвата")]
    private float attackAnimationSpeed = 1.6f;
    [SerializeField, Tooltip("Максимальный угол (в градусах) между forward врага и направлением на игрока для атаки/урона")] 
    private float maxAttackAngle = 60f;
    [SerializeField, Tooltip("Автоповорот к игроку перед атакой (только по оси Y)")] 
    private bool facePlayerOnAttack = true;
    [SerializeField, Tooltip("Имя Trigger параметра атаки в Animator (если есть)")]
    private string attackTrigger = "Attack";
    [SerializeField, Tooltip("Имя Trigger параметра атаки 2 в Animator (если есть)")]
    private string attack2Trigger = "Attack2";
    [SerializeField, Tooltip("Имя Trigger параметра атаки 3 в Animator (если есть)")]
    private string attack3Trigger = "Attack3";
    [SerializeField, Tooltip("Имя анимационного стейта атаки (для принудительного CrossFade)")]
    private string attackStateName = "Attack";
    [SerializeField, Tooltip("Имя анимационного стейта атаки 2 (для принудительного CrossFade)")]
    private string attack2StateName = "Attack2";
    [SerializeField, Tooltip("Имя анимационного стейта атаки 3 (для принудительного CrossFade)")]
    private string attack3StateName = "Attack3";
    [SerializeField, Tooltip("Слой аниматора, где лежит атака")]
    private int attackStateLayer = 0;
    [SerializeField, Tooltip("Радиус хитбокса удара (OverlapSphere)")]
    private float attackHitRadius = 1.2f;
    [SerializeField, Tooltip("Смещение хитбокса вперёд вдоль forward")]
    private float attackHitForwardOffset = 0.6f;
    [SerializeField, Tooltip("Множитель скорости NavMeshAgent во время атаки")]
    private float attackMoveSpeedMultiplier = 0.15f;
    [SerializeField, Tooltip("Скорость плавного изменения скорости при атаке")]
    private float attackSpeedLerp = 6f;

    [Header("Attack Variants")]
    [SerializeField, Range(0f, 5f)] private float attack1Weight = 1f;
    [SerializeField, Range(0f, 5f)] private float attack2Weight = 1f;
    [SerializeField, Range(0f, 5f)] private float attack3Weight = 1f;

    [Header("Animation")]
    [SerializeField] private string walkingStateName = "walking";
    [SerializeField] private string stunStateName = "Stun";
    [SerializeField] private string wakeUpStateName = "wakeUp_stun";
    [SerializeField] private string deathStateName = "death";
    [SerializeField] private string deathEndStateName = "death_end";
    [SerializeField] private float animTransition = 0.15f;
    [SerializeField] private int baseAnimLayer = 0;
    [SerializeField] private int stunAnimLayer = 2;
    [SerializeField] private int wakeUpAnimLayer = 3;
    [SerializeField] private bool useWalkingAnimation = true;
    [SerializeField, Tooltip("Длительность анимации вставания после стана (сек)")]
    private float wakeUpDuration = 1.2f;
    [SerializeField, Tooltip("Длительность анимации смерти (сек)")]
    private float deathDuration = 2f;
    [SerializeField, Tooltip("Длительность анимации death_end (сек)")]
    private float deathEndDuration = 1f;

    [Header("Hitbox")]
    [SerializeField, Tooltip("Радиус капсулы коллайдера врага (для более лёгкого попадания)")]
    private float enemyColliderRadius = 0.7f;
    [SerializeField, Tooltip("Высота капсулы коллайдера врага")]
    private float enemyColliderHeight = 2.2f;
    [SerializeField, Tooltip("Центр капсулы коллайдера врага")]
    private Vector3 enemyColliderCenter = new Vector3(0f, 1.1f, 0f);

    private int currentWaypointIndex = 0;
    private bool isPatrolling = true;
    private bool isChasing = false;
    public bool caughtPlayer = false;

    private Transform player;
    private PlayerHealth playerHealth;
    private Vector3 playerLastPosition = Vector3.zero;
    private bool isStunned = false;

    public bool IsStunned => isStunned;

    private float nextAttackTime;
    private bool isAttacking;
    private float baseAnimatorSpeed = 1f;
    private string currentAnimState;
    private Coroutine attackRoutine;
    private bool isWakingUp;
    private float baseNavSpeed;
    private Coroutine attackSpeedRoutine;
    private bool isDead;
    private int lastAttackIndex = -1;

    void Start()
    {
        if (!gameObject.activeInHierarchy) return;

        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null) baseNavSpeed = navMeshAgent.speed;
        m_Animator = GetComponent<Animator>();
        if (m_Animator != null) baseAnimatorSpeed = m_Animator.speed;

        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();

            // Если компонент не повесили вручную — добавим сами, иначе враг не сможет "наносить удары".
            if (playerHealth == null)
                playerHealth = player.gameObject.AddComponent<PlayerHealth>();

        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        // Добавляем Collider и Rigidbody, если их нет
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule == null && GetComponent<Collider>() == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        if (capsule != null)
        {
            capsule.center = enemyColliderCenter;
            capsule.radius = enemyColliderRadius;
            capsule.height = enemyColliderHeight;
        }

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = true;
        }

        gameObject.layer = LayerMask.NameToLayer("Enemy");

        if (navMeshAgent != null)
            navMeshAgent.stoppingDistance = Mathf.Max(navMeshAgent.stoppingDistance, stopBeforePlayerDistance);

        GoToNextWaypoint();
    }

    void Update()
    {
        if (caughtPlayer || !gameObject.activeInHierarchy || isDead) return;

        if (isStunned)
        {
            StopAgentMovement();
            ForceStunAnimatorState();
            PlayStateWithFallback(stunStateName, stunAnimLayer);
            return;
        }

        if (isWakingUp)
        {
            StopAgentMovement();
            PlayStateWithFallback(wakeUpStateName, wakeUpAnimLayer);
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            caughtPlayer = true;
            if (navMeshAgent != null) navMeshAgent.isStopped = true;
            return;
        }

        if (isPatrolling && navMeshAgent.enabled && 
            navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && 
            !navMeshAgent.pathPending)
            GoToNextWaypoint();

        CheckForPlayer();

        m_Animator.SetFloat("Speed", navMeshAgent.velocity.magnitude);
        m_Animator.SetBool("isChasing", isChasing);

        if (useWalkingAnimation)
            UpdateMovementAnimation();

        if (IsCloseToPlayer())
            AttackPlayer();
    }

    void CheckForPlayer()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool canSee = dist <= viewRadius && InSightCone(player.position);

        if (canSee)
        {
            playerLastPosition = player.position;
            if (!isChasing)
            {
                isPatrolling = false;
                isChasing = true;
                navMeshAgent.speed = speedRun;
            }
            if (navMeshAgent.enabled)
            {
                if (dist <= stopBeforePlayerDistance)
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
            if (navMeshAgent.enabled && !navMeshAgent.hasPath)
                navMeshAgent.SetDestination(playerLastPosition);

            if (navMeshAgent.enabled && 
                navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && 
                !navMeshAgent.pathPending)
                StopChasing();
        }
    }

    bool InSightCone(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        return Vector3.Angle(transform.forward, dir) <= viewAngle / 2f;
    }

    void StopChasing()
    {
        isChasing = false;
        isPatrolling = true;
        navMeshAgent.speed = speedWalk;
        if (navMeshAgent.enabled)
            GoToNextWaypoint();
    }

    void GoToNextWaypoint()
    {
        if (waypoints.Length == 0) return;
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        if (navMeshAgent.enabled)
            navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
    }

    bool IsCloseToPlayer()
    {
        if (player == null) return false;
        float range = attackRange;
        if (navMeshAgent != null)
            range = Mathf.Max(range, navMeshAgent.stoppingDistance + 0.1f);
        return Vector3.Distance(transform.position, player.position) <= range;
    }

    void AttackPlayer()
    {
        if (isAttacking) return;
        if (isStunned) return;
        if (isWakingUp) return;
        if (isDead) return;
        if (playerHealth == null || playerHealth.IsDead) return;
        if (Time.time < nextAttackTime) return;
        if (!IsFacingPlayer()) return; // Атакуем только когда смотрим на игрока

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
        EnsureAgentActiveForAttack();

        // Повернуться к игроку перед ударом (только по оси Y)
        if (facePlayerOnAttack)
            FacePlayerOnY();

        int attackIndex = PickAttackIndex();
        if (m_Animator != null)
            PlayAttackAnimation(attackIndex);

        float hitDelay = Mathf.Max(0f, attackHitDelay);
        if (hitDelay > 0f)
            yield return new WaitForSecondsRealtime(hitDelay);

        if (!caughtPlayer && !isStunned)
            TryDealDamage();

        if (playerHealth != null && playerHealth.IsDead)
        {
            caughtPlayer = true;
            if (m_Animator != null) m_Animator.SetBool("IsCaughtPlayer", true);
            if (catchSound != null && audioSource != null) audioSource.PlayOneShot(catchSound);
        }

        float totalLock = Mathf.Max(attackLockTime, attackAnimationDuration);
        float remainingLock = Mathf.Max(0f, totalLock - hitDelay);
        if (remainingLock > 0f)
            yield return new WaitForSecondsRealtime(remainingLock);

        if (m_Animator != null)
            m_Animator.speed = baseAnimatorSpeed;
        if (!caughtPlayer) ResumeAgentMovement();

        EndAttackSpeedSlowdown();

        ResumeAgentMovementAndRepath();
        currentAnimState = null;

        isAttacking = false;
        attackRoutine = null;
    }

    private void PlayAttackAnimation(int attackIndex)
    {
        if (m_Animator == null) return;

        m_Animator.speed = baseAnimatorSpeed * Mathf.Max(0.1f, attackAnimationSpeed);

        string trigger = GetAttackTrigger(attackIndex);
        string stateName = GetAttackStateName(attackIndex);

        if (!string.IsNullOrEmpty(trigger) && HasTrigger(m_Animator, trigger))
            m_Animator.SetTrigger(trigger);

        if (!string.IsNullOrEmpty(stateName) && HasState(m_Animator, attackStateLayer, stateName))
        {
            m_Animator.Play(stateName, attackStateLayer, 0f);
            m_Animator.CrossFadeInFixedTime(stateName, 0.5f, attackStateLayer);
            currentAnimState = stateName;
        }
    }

    private int PickAttackIndex()
    {
        float w1 = Mathf.Max(0f, attack1Weight);
        float w2 = Mathf.Max(0f, attack2Weight);
        float w3 = Mathf.Max(0f, attack3Weight);
        float total = w1 + w2 + w3;

        if (total <= 0f)
            return 0;

        // Avoid repeating the same attack back-to-back when possible.
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

        // Fallback if we failed to pick a different one.
        lastAttackIndex = (lastAttackIndex + 1) % 3;
        return lastAttackIndex;
    }

    private string GetAttackTrigger(int attackIndex)
    {
        switch (attackIndex)
        {
            case 1: return attack2Trigger;
            case 2: return attack3Trigger;
            default: return attackTrigger;
        }
    }

    private string GetAttackStateName(int attackIndex)
    {
        switch (attackIndex)
        {
            case 1: return attack2StateName;
            case 2: return attack3StateName;
            default: return attackStateName;
        }
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

    private void TryDealDamage()
    {
        var ph = GetPlayerHealth();
        if (ph == null || ph.IsDead) return;

        // Урон только если враг смотрит на игрока
        if (!IsFacingPlayer()) return;

        Vector3 center = transform.position + transform.forward * attackHitForwardOffset;
        Collider[] hits = Physics.OverlapSphere(center, attackHitRadius, LayerMask.GetMask("Default", "Player"));
        foreach (var h in hits)
        {
            if (h == null) continue;
            var target = h.GetComponent<PlayerHealth>();
            if (target == null) continue;
            target.TakeEnemyHit(this);
            break;
        }
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

    private static bool HasTrigger(Animator animator, string param)
    {
        if (animator == null || string.IsNullOrWhiteSpace(param)) return false;
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == param)
                return true;
        return false;
    }

    private static bool HasState(Animator animator, int layer, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;
        return animator.HasState(layer, Animator.StringToHash(stateName));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward * viewRadius;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward * viewRadius;

        Gizmos.DrawRay(transform.position, left);
        Gizmos.DrawRay(transform.position, right);
    }

        // ← НОВЫЕ МЕТОДЫ ДЛЯ АКТИВАЦИИ ОХОТЫ ИЗВНЕ

    /// <summary>
    /// Телепортирует врага в указанную позицию (используется при взятии дискеты)
    /// </summary>
    public void TeleportToPosition(Vector3 newPosition)
    {
        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.enabled = false; // Отключаем на кадр, чтобы телепорт прошёл без ошибок
            transform.position = newPosition;
            navMeshAgent.enabled = true;
        }
        else
        {
            transform.position = newPosition;
        }
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
        m_Animator.SetBool("IsStunned", true); // Включаем анимацию стана

        ForceStunAnimatorState();
        PlayStateWithFallback(stunStateName, stunAnimLayer);

        // Активируем Stun Layer
        m_Animator.SetLayerWeight(stunAnimLayer, 1f); // Максимальный вес Stun Layer
        // Уменьшаем вес Base Layer до минимума (почти 0)
        m_Animator.SetLayerWeight(baseAnimLayer, 0f);
        StartCoroutine(RevertFromStun(duration));
    }

    IEnumerator RevertFromStun(float duration)
    {
        yield return new WaitForSeconds(duration); // Ждём конец стана
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
        m_Animator.SetLayerWeight(baseAnimLayer, 1f); // Возвращаем Base Layer
        m_Animator.SetBool("IsStunned", false); // Выключаем состояние стана
    }

    private void ForceStunAnimatorState()
    {
        if (m_Animator == null) return;
        // m_Animator.SetFloat("Speed", 0f);
        // m_Animator.SetBool("isChasing", false);
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
            GoToNextWaypoint();
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

    public bool CanBeFinished()
    {
        return isStunned && !isDead;
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

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, deathDuration));

        if (m_Animator != null)
            PlayStateWithFallback(deathEndStateName, baseAnimLayer);

        yield return new WaitForSeconds(Mathf.Max(0.1f, deathEndDuration));

        if (m_Animator != null)
            m_Animator.speed = 0f;
    }
    /// <summary>
    /// Принудительно запускает преследование игрока (активация охоты после взятия дискеты)
    /// </summary>
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

        // Сразу переходим в режим погони
        isPatrolling = false;
        isChasing = true;
        navMeshAgent.speed = speedRun;

        // Устанавливаем цель — игрок
        if (navMeshAgent.enabled)
            navMeshAgent.SetDestination(player.position);

        // Обновляем аниматор
        m_Animator.SetBool("isChasing", true);

        Debug.Log("Враг активирован и начал преследование игрока после взятия дискеты!");
    }
}