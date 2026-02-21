﻿using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public partial class AdvancedEnemyAI : MonoBehaviour
{
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

    void Start()
    {
        if (!gameObject.activeInHierarchy) return;

        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null) baseNavSpeed = navMeshAgent.speed;
        m_Animator = GetComponent<Animator>();
        if (m_Animator != null) baseAnimatorSpeed = m_Animator.speed;
        currentHealth = maxHealth;

        ApplySceneSpeeds();

        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerCollider = player.GetComponent<Collider>();

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

        enemyCollider = capsule != null ? capsule : GetComponent<Collider>();

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = true;
        }

        gameObject.layer = LayerMask.NameToLayer("Enemy");

        if (navMeshAgent != null)
        {
            navMeshAgent.stoppingDistance = Mathf.Max(navMeshAgent.stoppingDistance, stopBeforePlayerDistance);
            navMeshAgent.autoBraking = true;
            navMeshAgent.acceleration = Mathf.Max(0.1f, agentAcceleration);
            navMeshAgent.angularSpeed = Mathf.Max(1f, agentAngularSpeed);
        }

        if (scanOnSpawn)
            BeginScan();
        else
            BeginPatrol();
    }

    private void CachePlayerCollider()
    {
        if (player == null || playerCollider != null) return;
        playerCollider = player.GetComponent<Collider>();
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

    private void ApplySceneSpeeds()
    {
        chaseSpeed = patrolSpeed;
        speedWalk = patrolSpeed;
        speedRun = patrolSpeed;
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

        // m_Animator.SetFloat("Speed", navMeshAgent.velocity.magnitude);
        // m_Animator.SetBool("isChasing", isChasing);

        if (useWalkingAnimation)
            UpdateMovementAnimation();

        if (IsCloseToPlayer())
            AttackPlayer();
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
}