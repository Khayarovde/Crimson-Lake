﻿using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public partial class AdvancedEnemyAI : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Alert,
        Chase,
        Attack,
        Stunned,
        Dead
    }

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
    private Coroutine stunRoutine;
    private bool isWakingUp;
    private bool isDead;
    private int lastAttackIndex = -1;

    private bool isScanning;
    private float scanEndTime;
    private bool isSearching;
    private Vector3[] searchPoints;
    private int currentSearchIndex;
    private float searchWaitEndTime;
    private float nextRepathTime;
    private Collider enemyCollider;
    private Collider playerCollider;

    private float currentHealth;
    private bool isRandomPatrolling;
    private Vector3 currentRandomPatrolPoint;
    private float randomPatrolWaitEndTime;
    private readonly RaycastHit[] lineOfSightHits = new RaycastHit[16];
    private bool isInitialized;
    private float shotInvestigationEndTime;
    private bool rootMotionApplied;
    private bool movementAnimIsWalking;
    private float nextMovementAnimSwitchTime;
    private float stateEndTime;
    private float nextRunNoiseCheckTime;
    private Vector3 lastHeardRunPosition;
    private Coroutine resurrectionRoutine;
    private readonly Collider[] playerDetectionHits = new Collider[24];
    private EnemyState currentState = EnemyState.Patrol;

    public bool IsDead => isDead;
    public EnemyState CurrentState => currentState;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        WeaponHandler.PlayerShotFired += HandlePlayerShotFired;
    }

    private void OnDisable()
    {
        WeaponHandler.PlayerShotFired -= HandlePlayerShotFired;
        SetPlayerCollisionIgnored(false);
    }

    private void OnDestroy()
    {
        WeaponHandler.PlayerShotFired -= HandlePlayerShotFired;
        SetPlayerCollisionIgnored(false);
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
            return;

        if (navMeshAgent == null)
            TryGetComponent(out navMeshAgent);

        if (m_Animator == null)
            TryGetComponent(out m_Animator);

        if (m_Animator != null)
        {
            baseAnimatorSpeed = m_Animator.speed;
            rootMotionApplied = !disableMovement;
            m_Animator.applyRootMotion = rootMotionApplied;
        }

        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
        {
            player.TryGetComponent(out playerHealth);
            player.TryGetComponent(out playerCollider);

            if (playerHealth == null)
                Debug.LogWarning("У игрока отсутствует компонент PlayerHealth. Враг не сможет нанести урон, пока компонент не добавлен вручную.", this);

            playerLastPosition = player.position;
        }

        isInitialized = true;
    }

    private void Start()
    {
        if (!gameObject.activeInHierarchy) return;

        EnsureInitialized();
        currentHealth = maxHealth;

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

        enemyCollider = capsule != null ? capsule : GetComponent<Collider>();

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = true;
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;
        else
            Debug.LogWarning("Слой 'Enemy' не найден в проекте. Назначение слоя пропущено.", this);

        if (navMeshAgent != null)
        {
            navMeshAgent.speed = Mathf.Max(0f, speedWalk);
            navMeshAgent.stoppingDistance = Mathf.Max(navMeshAgent.stoppingDistance, stopBeforePlayerDistance);
            navMeshAgent.autoBraking = true;
        }

        SetState(scanOnSpawn ? EnemyState.Alert : EnemyState.Patrol);

        if (scanOnSpawn)
            BeginScan();
        else
            BeginPatrol();
    }

    private void CachePlayerCollider()
    {
        if (player == null || playerCollider != null) return;
        player.TryGetComponent(out playerCollider);
    }

    private void SetPlayerCollisionIgnored(bool ignore)
    {
        if (!disableCollisionDuringAttack) return;
        if (enemyCollider == null)
            enemyCollider = GetComponent<Collider>();
        if (playerCollider == null)
            CachePlayerCollider();
        if (enemyCollider == null || playerCollider == null) return;

        Physics.IgnoreCollision(enemyCollider, playerCollider, ignore);
    }

    private void HandlePlayerShotFired(Vector3 shotPosition, float loudness)
    {
        RegisterShotStimulus(shotPosition, loudness, true);
    }

    public void NotifyShotHitByPlayer(Vector3 shotPosition)
    {
        RegisterShotStimulus(shotPosition, 2f, false);
    }

    public void NotifyPlayerRunning(Vector3 runPosition, float loudness = 1f)
    {
        RegisterShotStimulus(runPosition, loudness, true);
    }

    private void RegisterShotStimulus(Vector3 shotPosition, float loudness, bool requireHearingCheck)
    {
        if (!gameObject.activeInHierarchy || isDead || isPermanentlyDead || caughtPlayer)
            return;

        if (isStunned || isWakingUp)
            return;

        EnsureInitialized();

        if (IsMovementDisabled())
            return;

        float normalizedLoudness = Mathf.Max(0.1f, loudness);
        if (requireHearingCheck)
        {
            float hearDistance = hearingRadius * normalizedLoudness;
            if (Vector3.Distance(transform.position, shotPosition) > hearDistance)
                return;
        }

        isScanning = false;
        StopSearch();
        isRandomPatrolling = false;
        shotInvestigationEndTime = Time.time + Mathf.Max(0.5f, investigateShotDuration);
        playerLastPosition = shotPosition;

        if (currentState == EnemyState.Patrol)
            BeginAlert(shotPosition);
        else if (currentState != EnemyState.Attack)
            SetState(EnemyState.Chase);

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            Vector3 destination = shotPosition;
            if (NavMesh.SamplePosition(shotPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                destination = hit.position;
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(destination);
        }
    }

    private void Update()
    {
        if (caughtPlayer || !gameObject.activeInHierarchy || isPermanentlyDead) return;

        if (isDead || currentState == EnemyState.Dead)
            return;

        if (m_Animator != null)
        {
            bool shouldApplyRootMotion = !disableMovement;
            if (rootMotionApplied != shouldApplyRootMotion)
            {
                rootMotionApplied = shouldApplyRootMotion;
                m_Animator.applyRootMotion = rootMotionApplied;
            }
        }

        if (isStunned)
        {
            SetState(EnemyState.Stunned);
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

        UpdateRunNoiseStimulus();

        if (isScanning)
        {
            CheckForPlayer();
            UpdateScan();
        }
        else
        {
            if (isPatrolling)
                UpdatePatrol();

            CheckForPlayer();
        }

        if (currentState == EnemyState.Alert)
            UpdateAlert();

        if (currentState == EnemyState.Chase)
            UpdateErraticChaseSpeed();

        if (isChasing && shotInvestigationEndTime > 0f && Time.time > shotInvestigationEndTime && !isSearching)
            shotInvestigationEndTime = 0f;

        if (IsMovementDisabled())
        {
            StopAgentMovement();
            return;
        }

        // m_Animator.SetFloat("Speed", navMeshAgent.velocity.magnitude);
        // m_Animator.SetBool("isChasing", isChasing);

        if (useWalkingAnimation)
            UpdateMovementAnimation();

        if (currentState == EnemyState.Chase && IsCloseToPlayer())
            AttackPlayer();
    }

    private void UpdateAlert()
    {
        if (Time.time < stateEndTime)
            return;

        SetState(EnemyState.Chase);
        ResumeAgentMovementAndRepath();
    }

    private void UpdateRunNoiseStimulus()
    {
        if (!listenToPlayerRunNoise || player == null)
            return;

        if (Time.time < nextRunNoiseCheckTime)
            return;

        nextRunNoiseCheckTime = Time.time + Mathf.Max(0.05f, runNoiseCheckInterval);

        float speed = 0f;
        if (player.TryGetComponent<Rigidbody>(out var rb))
            speed = rb.linearVelocity.magnitude;
        else if (player.TryGetComponent<CharacterController>(out var cc))
            speed = cc.velocity.magnitude;

        if (speed >= Mathf.Max(0.1f, playerRunSpeedThreshold))
        {
            lastHeardRunPosition = player.position;
            RegisterShotStimulus(lastHeardRunPosition, playerRunLoudness, true);
        }
    }

    private void UpdateErraticChaseSpeed()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
            return;

        float minSpeed = Mathf.Max(speedWalk, Mathf.Min(speedChaseMin, speedChaseMax));
        float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(speedChaseMin, speedChaseMax));
        float t = Mathf.PerlinNoise(GetInstanceID() * 0.037f, Time.time * Mathf.Max(0.1f, chaseErraticChangeRate));
        navMeshAgent.speed = Mathf.Lerp(minSpeed, maxSpeed, t);
    }

    private void SetState(EnemyState newState)
    {
        currentState = newState;
        currentStateDebug = newState;

        switch (newState)
        {
            case EnemyState.Patrol:
                isPatrolling = true;
                isChasing = false;
                break;
            case EnemyState.Alert:
                isPatrolling = false;
                isChasing = false;
                break;
            case EnemyState.Chase:
                isPatrolling = false;
                isChasing = true;
                break;
            case EnemyState.Attack:
                isPatrolling = false;
                isChasing = true;
                break;
            case EnemyState.Stunned:
                isPatrolling = false;
                isChasing = false;
                break;
            case EnemyState.Dead:
                isPatrolling = false;
                isChasing = false;
                break;
        }

        if (m_Animator != null)
            m_Animator.SetBool("isChasing", useChaseAnimatorFlag && (newState == EnemyState.Chase || newState == EnemyState.Attack));
    }

    private void BeginAlert(Vector3 focusPoint)
    {
        if (isDead || isStunned || isWakingUp)
            return;

        playerLastPosition = focusPoint;
        stateEndTime = Time.time + Mathf.Max(0.1f, alertDuration);
        SetState(EnemyState.Alert);
        StopAgentMovement();

        Vector3 lookDir = focusPoint - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

        PlayStateWithFallback(screamStateName, baseAnimLayer);
        if (screamClip != null && audioSource != null)
            audioSource.PlayOneShot(screamClip);
    }

        // ← НОВЫЕ МЕТОДЫ ДЛЯ АКТИВАЦИИ ОХОТЫ ИЗВНЕ

    /// <summary>
    /// Телепортирует врага в указанную позицию (используется при взятии дискеты)
    /// </summary>
    public void TeleportToPosition(Vector3 newPosition)
    {
        EnsureInitialized();

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            if (!navMeshAgent.Warp(newPosition))
            {
                if (NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    navMeshAgent.Warp(hit.position);
                else
                    transform.position = newPosition;
            }
        }
        else
        {
            transform.position = newPosition;
        }
    }
}