using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemytest : MonoBehaviour
{
    private enum EnemyState
    {
        Patrol,
        Detection,
        Alert,
        Chase,
        Attack,
        AttackRecovery,
        Stagger,
        FakeDeath
    }

    public NavMeshAgent navAgent;
    public Transform player;
    public LayerMask groundLayer;
    public LayerMask playerLayer;

    [Header("Health")]
    public float maxHealth = 100f;
    public float health = 100f;
    public bool canBeKilled = true;
    [Tooltip("Если включено, любой урон от оружия убивает врага с одного попадания (игнорируя обычный порог HP).")]
    public bool oneShotKillFromWeapon = false;
    public bool useFakeDeath = true;
    public float permanentDeathDestroyDelay = 2f;
    public bool keepBodyAfterPermanentDeath = true;
    public int knockoutsToPermanentDeath = 2;
    [Tooltip("Если включено, после последнего нокдауна враг падает в Stagger для добивания, а не в fake death.")]
    public bool useFinalKnockoutStun = true;
    [Tooltip("Если включено, после нокдаун-стана враг встает сразу (без фазы fake death ожидания).")]
    public bool instantWakeAfterKnockoutStun = false;
    public float knockoutStunDuration = 0.35f;
    public float knockoutMinVisibleStunDuration = 0.55f;
    public bool debugDamageLogs = true;
    public bool debugStateLogs = true;

    [Header("Patrol")]
    public Transform[] waypoints;
    public bool loopPatrol = true;
    public float walkPointRange = 8f;
    public float patrolSpeed = 0.55f;
    public float waypointStopDistance = 0.8f;
    public float waypointPauseTime = 1.4f;

    [Header("Detection")]
    public float sightRange = 10f;
    [Range(10f, 360f)] public float viewAngle = 90f;
    public float hearingRadiusRun = 8f;
    public float hearingRadiusShot = 16f;
    public float hearingInvestigateSpeed = 0.8f;
    public float investigateWaitTime = 2f;
    public float runNoiseSpeedThreshold = 3.2f;

    [Header("Chase")]
    public float chaseSpeed = 1.15f;
    public float chaseCloseSpeed = 0.8f;
    public float chaseCloseCatchupSpeed = 1.65f;
    public float chaseVisibleCatchupBonus = 0.45f;
    public float chaseFallbackPlayerSpeed = 1.35f;
    [Tooltip("Доп. бонус скорости, когда игрок целится (ПКМ).")]
    public float chaseAimExtraBonus = 0.7f;
    [Tooltip("Максимальная скорость погони, когда игрок целится (ПКМ).")]
    public float chaseAimCatchupSpeed = 2.4f;
    [Tooltip("Минимальная скорость преследования во время ПКМ игрока, чтобы враг всегда немного догонял.")]
    public float chaseAimMinPursuitSpeed = 2.1f;
    [Tooltip("Гарантированная прибавка к скорости относительно игрока во время ПКМ (чуток быстрее).")]
    public float chaseAimGuaranteedLead = 0.18f;
    [Tooltip("Множитель частоты репаса во время ПКМ игрока (меньше = чаще).")]
    public float chaseAimRepathMultiplier = 0.6f;
    public float chaseLostSightCatchupTime = 1.1f;
    public float chaseLostSightCatchupSpeed = 1.85f;
    public float chaseLostSightCatchupDistance = 3.4f;
    public float closeChaseDistance = 2.4f;
    public float chaseSampleRadius = 1.5f;
    public float pathRecalcInterval = 0.22f;
    public float destinationUpdateThreshold = 0.35f;
    public float lastKnownHoldTime = 5f;

    [Header("NavMesh Agent Tuning")]
    public float navAcceleration = 2.2f;
    public float navAngularSpeed = 130f;
    public float navStoppingDistance = 0.65f;
    public bool navAutoBraking = true;

    [Header("Attack")]
    public float attackRange = 1.5f;
    [Tooltip("Запас дистанции для входа в атаку, чтобы враг не застревал на границе range.")]
    public float attackEnterRangePadding = 0.25f;
    public float attackTurnSpeed = 300f;
    public float attackWindup = 0.3f;
    public float attackHitDelayAfterAnimStart = 0.18f;
    public float attackActiveWindow = 0.12f;
    public float attackCooldown = 1.15f;
    public float attackMissRecovery = 0.5f;
    public float attackLungeSpeed = 1.25f;
    public float attackHitLockDuration = 0.28f;
    public float postAttackRecovery = 0.35f;
    public float attackStartCooldown = 1.1f;
    public bool useAnimationEventsForHitbox = true;
    public bool usePreAttackStrafe = true;
    [Range(0f, 1f)] public float preAttackStrafeChance = 0.65f;
    public float preAttackStrafeDistance = 0.9f;
    public float preAttackStrafeDuration = 0.45f;
    public float preAttackStrafeCooldown = 1.25f;
    public float preAttackStrafeSpeed = 1.55f;
    [Tooltip("Множитель шанса сайдстепа, когда игрок в ПКМ. 0 = почти без сайдстепа.")]
    public float preAttackStrafeAimChanceMultiplier = 0.25f;
    [Tooltip("Если игрок уже очень близко, сайдстеп пропускается и враг сразу бьет.")]
    public float preAttackStrafeSkipDistance = 1.2f;
    [Tooltip("Если во время сайдстепа враг снова сблизился, он прерывает сайдстеп и сразу атакует.")]
    public float preAttackStrafeAbortDistance = 1.05f;
    [Tooltip("После промаха на это время отключается сайдстеп, чтобы быстрее повторить прямую атаку.")]
    public float preAttackNoStrafeAfterMissTime = 1.1f;
    public int attackComboHits = 1;
    public float attackComboHitInterval = 0.18f;
    public int damage = 15;
    public Vector3 attackHitboxHalfExtents = new Vector3(0.45f, 0.6f, 0.55f);
    public float attackHitboxForwardOffset = 0.6f;
    [Range(0f, 180f)] public float attackMaxHitAngle = 55f;
    [SerializeField] private float attackMinEventDelay = 0.14f;
    [SerializeField] private EnemyKnifeHitbox knifeHitbox;

    [Header("Stagger")]
    public float staggerDuration = 0.35f;

    [Header("Fake Death")]
    [Tooltip("Через сколько минимум секунд враг встает после fake death.")]
    public float fakeDeathReviveMin = 60f;
    [Tooltip("Через сколько максимум секунд враг встает после fake death.")]
    public float fakeDeathReviveMax = 180f;
    [Range(0f, 1f)] public float reviveHealthPercent = 0.5f;

    [Header("Finisher Reaction")]
    [Tooltip("Насколько враг отлетает от пинка во время добивания.")]
    public float finisherKnockbackDistance = 0.55f;
    [Tooltip("Длительность отлета при добивании.")]
    public float finisherKnockbackDuration = 0.12f;
    [Tooltip("Небольшой подброс по дуге во время отлета.")]
    public float finisherKnockbackArcHeight = 0.08f;

    public Animator animator;
    public ParticleSystem hitEffect;
    public AudioSource audioSource;
    public AudioClip screamClip;

    [Header("Animation")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string walkingStateName = "walking";
    [SerializeField] private string attackStateName = "Attack";
    [SerializeField] private string attackRecoveryStateName = "Idle";
    [SerializeField] private string screamStateName = "Scream";
    [SerializeField] private string fakeDeathStateName = "death";
    [SerializeField] private string wakeUpStateName = "wakeUp_stun";
    [SerializeField] private string staggerStateName = "Stun";
    [SerializeField] private string staggerFallbackStateName = "Hit";
    [SerializeField] private float animTransition = 0.15f;
    [SerializeField] private int baseAnimLayer = 0;

    private Vector3 walkPoint;
    private bool walkPointSet;
    private bool alreadyAttacked;
    private bool isPermanentlyDead;
    private bool isBurned;
    private bool isAlertTriggered;
    private string currentAnimState;
    private float nextRepathTime;
    private float nextWaypointMoveTime;
    private float investigateEndTime = -1f;
    private float lastKnownArrivalTime = -1f;
    private int currentWaypointIndex;
    private Vector3 lastKnownPlayerPosition;
    private EnemyState state = EnemyState.Patrol;
    private Coroutine attackRoutine;
    private Coroutine staggerRoutine;
    private Coroutine fakeDeathRoutine;
    private PlayerHealth playerHealth;
    private Rigidbody playerRigidbody;
    private WeaponHandler playerWeaponHandler;
    private readonly Collider[] attackHitBuffer = new Collider[8];
    private bool attackHitWindowOpen;
    private bool didHitCurrentSwing;
    private float attackAnimStartTime = -10f;
    private Coroutine delayedHitboxOnRoutine;
    private Vector3 lastRequestedDestination;
    private bool hasLastRequestedDestination;
    private float nextAttackAllowedTime;
    private bool isPreAttackStrafing;
    private float preAttackStrafeEndTime;
    private float nextPreAttackStrafeAllowedTime;
    private Vector3 preAttackStrafeTarget;
    private int knockoutCount;
    private Coroutine knockoutRoutine;
    private Vector3 lastPlayerPosition;
    private float computedPlayerSpeed;
    private float lastSeenPlayerTime = -10f;
    private bool isKnockoutStunActive;
    private bool processingWeaponDamage;
    private float noStrafeUntilTime;
    private bool isFinisherExecutionLocked;
    private Coroutine finisherDeathRoutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
                playerObject = GameObject.Find("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player != null)
        {
            player.TryGetComponent(out playerHealth);
            player.TryGetComponent(out playerRigidbody);
            player.TryGetComponent(out playerWeaponHandler);
            lastKnownPlayerPosition = player.position;
            lastPlayerPosition = player.position;
        }

        if (navAgent == null)
            Debug.LogError("Enemytest: NavMeshAgent not found on this GameObject.", this);

        if (player == null)
            Debug.LogError("Enemytest: Player reference is missing. Assign player in Inspector or set Player tag/name correctly.", this);

        if (animator == null)
            Debug.LogWarning("Enemytest: Animator not found. Animation states will be skipped.", this);

        maxHealth = Mathf.Max(1f, maxHealth);
        health = Mathf.Clamp(health, 1f, maxHealth);

        if (navAgent != null)
        {
            ApplyNavAgentTuning();
            SetAgentSpeed(patrolSpeed);
        }

        if (knifeHitbox == null)
            knifeHitbox = GetComponentInChildren<EnemyKnifeHitbox>(true);

        if (knifeHitbox != null)
        {
            knifeHitbox.SetOwner(this);
            knifeHitbox.SetActiveWindow(false);
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        health = Mathf.Clamp(health, 0f, maxHealth);
        permanentDeathDestroyDelay = Mathf.Clamp(permanentDeathDestroyDelay, 0.1f, 10f);
        knockoutsToPermanentDeath = Mathf.Clamp(knockoutsToPermanentDeath, 1, 10);
        knockoutStunDuration = Mathf.Clamp(knockoutStunDuration, 0.05f, 3f);
        knockoutMinVisibleStunDuration = Mathf.Clamp(knockoutMinVisibleStunDuration, 0.1f, 3f);
        patrolSpeed = Mathf.Clamp(patrolSpeed, 0.1f, 3f);
        chaseSpeed = Mathf.Clamp(chaseSpeed, patrolSpeed + 0.2f, 6f);
        chaseCloseSpeed = Mathf.Clamp(chaseCloseSpeed, 0.1f, chaseSpeed);
        chaseCloseCatchupSpeed = Mathf.Clamp(chaseCloseCatchupSpeed, chaseCloseSpeed, 6f);
        chaseVisibleCatchupBonus = Mathf.Clamp(chaseVisibleCatchupBonus, 0f, 2f);
        chaseFallbackPlayerSpeed = Mathf.Clamp(chaseFallbackPlayerSpeed, 0f, 8f);
        chaseAimExtraBonus = Mathf.Clamp(chaseAimExtraBonus, 0f, 3f);
        chaseAimCatchupSpeed = Mathf.Clamp(chaseAimCatchupSpeed, chaseCloseCatchupSpeed, 9f);
        chaseAimMinPursuitSpeed = Mathf.Clamp(chaseAimMinPursuitSpeed, chaseCloseSpeed, 9f);
        chaseAimGuaranteedLead = Mathf.Clamp(chaseAimGuaranteedLead, 0f, 1.5f);
        chaseAimRepathMultiplier = Mathf.Clamp(chaseAimRepathMultiplier, 0.25f, 1.5f);
        chaseLostSightCatchupTime = Mathf.Clamp(chaseLostSightCatchupTime, 0f, 3f);
        chaseLostSightCatchupSpeed = Mathf.Clamp(chaseLostSightCatchupSpeed, chaseCloseSpeed, 8f);
        chaseLostSightCatchupDistance = Mathf.Clamp(chaseLostSightCatchupDistance, 0.8f, 8f);
        closeChaseDistance = Mathf.Clamp(closeChaseDistance, 0.8f, 6f);
        hearingInvestigateSpeed = Mathf.Clamp(hearingInvestigateSpeed, 0.5f, chaseSpeed);
        attackWindup = Mathf.Clamp(attackWindup, 0f, 2f);
        attackEnterRangePadding = Mathf.Clamp(attackEnterRangePadding, 0f, 1.5f);
        attackHitDelayAfterAnimStart = Mathf.Clamp(attackHitDelayAfterAnimStart, 0f, 1.5f);
        attackActiveWindow = Mathf.Clamp(attackActiveWindow, 0.02f, 1f);
        attackCooldown = Mathf.Clamp(attackCooldown, 0.1f, 3f);
        attackMissRecovery = Mathf.Clamp(attackMissRecovery, 0.1f, 2f);
        attackLungeSpeed = Mathf.Clamp(attackLungeSpeed, patrolSpeed, 6f);
        attackHitLockDuration = Mathf.Clamp(attackHitLockDuration, 0.05f, 1.2f);
        postAttackRecovery = Mathf.Clamp(postAttackRecovery, 0.05f, 1.2f);
        attackStartCooldown = Mathf.Clamp(attackStartCooldown, 0.1f, 2.5f);
        preAttackStrafeChance = Mathf.Clamp01(preAttackStrafeChance);
        preAttackStrafeDistance = Mathf.Clamp(preAttackStrafeDistance, 0.2f, 2f);
        preAttackStrafeDuration = Mathf.Clamp(preAttackStrafeDuration, 0.1f, 1.5f);
        preAttackStrafeCooldown = Mathf.Clamp(preAttackStrafeCooldown, 0.1f, 3f);
        preAttackStrafeSpeed = Mathf.Clamp(preAttackStrafeSpeed, chaseCloseSpeed, 8f);
        preAttackStrafeAimChanceMultiplier = Mathf.Clamp(preAttackStrafeAimChanceMultiplier, 0f, 1f);
        preAttackStrafeSkipDistance = Mathf.Clamp(preAttackStrafeSkipDistance, 0.2f, 3f);
        preAttackStrafeAbortDistance = Mathf.Clamp(preAttackStrafeAbortDistance, 0.2f, 3f);
        preAttackNoStrafeAfterMissTime = Mathf.Clamp(preAttackNoStrafeAfterMissTime, 0f, 3f);
        attackComboHits = Mathf.Clamp(attackComboHits, 1, 3);
        attackComboHitInterval = Mathf.Clamp(attackComboHitInterval, 0.05f, 0.8f);
        damage = Mathf.Clamp(damage, 1, 100);
        destinationUpdateThreshold = Mathf.Clamp(destinationUpdateThreshold, 0.05f, 2f);
        attackTurnSpeed = Mathf.Clamp(attackTurnSpeed, 30f, 1080f);
        attackMinEventDelay = Mathf.Clamp(attackMinEventDelay, 0f, 0.5f);
        attackHitboxForwardOffset = Mathf.Clamp(attackHitboxForwardOffset, 0.1f, 2f);
        attackHitboxHalfExtents.x = Mathf.Clamp(attackHitboxHalfExtents.x, 0.05f, 1.5f);
        attackHitboxHalfExtents.y = Mathf.Clamp(attackHitboxHalfExtents.y, 0.05f, 1.5f);
        attackHitboxHalfExtents.z = Mathf.Clamp(attackHitboxHalfExtents.z, 0.05f, 2f);
        attackMaxHitAngle = Mathf.Clamp(attackMaxHitAngle, 10f, 180f);
        finisherKnockbackDistance = Mathf.Clamp(finisherKnockbackDistance, 0f, 2f);
        finisherKnockbackDuration = Mathf.Clamp(finisherKnockbackDuration, 0.02f, 1f);
        finisherKnockbackArcHeight = Mathf.Clamp(finisherKnockbackArcHeight, 0f, 0.5f);
        navAcceleration = Mathf.Clamp(navAcceleration, 0.1f, 20f);
        navAngularSpeed = Mathf.Clamp(navAngularSpeed, 60f, 720f);
        navStoppingDistance = Mathf.Clamp(navStoppingDistance, 0.2f, 2.5f);

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        ApplyNavAgentTuning();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (knifeHitbox == null)
            knifeHitbox = GetComponentInChildren<EnemyKnifeHitbox>(true);

        if (knifeHitbox != null)
        {
            knifeHitbox.SetOwner(this);
            knifeHitbox.SetActiveWindow(false);
        }

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        WeaponHandler.PlayerShotFired += HandlePlayerShotFired;
    }

    private void OnDisable()
    {
        WeaponHandler.PlayerShotFired -= HandlePlayerShotFired;
        AttackHitboxOff();
    }

    private void Update()
    {
        if (isPermanentlyDead || navAgent == null || player == null)
            return;

        if (!navAgent.isOnNavMesh)
            return;

        UpdatePlayerSpeedEstimate();

        UpdatePerception();

        switch (state)
        {
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Detection:
                UpdateDetection();
                break;
            case EnemyState.Alert:
                break;
            case EnemyState.Chase:
                UpdateChase();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
            case EnemyState.AttackRecovery:
                break;
            case EnemyState.Stagger:
            case EnemyState.FakeDeath:
                break;
        }
    }

    private void UpdatePerception()
    {
        if (state == EnemyState.FakeDeath || state == EnemyState.Stagger || state == EnemyState.Alert)
            return;

        bool seesPlayer = CanSeePlayer();
        if (seesPlayer)
        {
            lastKnownPlayerPosition = player.position;
            lastSeenPlayerTime = Time.time;

            if (!isAlertTriggered)
            {
                StartCoroutine(EnterAlert());
                return;
            }

            if (state != EnemyState.Attack)
                SetState(EnemyState.Chase);

            return;
        }

        if (state == EnemyState.Patrol && CanHearPlayerRunning())
            BeginDetection(player.position);
    }

    private bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;
        if (distance > sightRange)
            return false;

        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);

        if (flatToPlayer.sqrMagnitude > 0.0001f && flatForward.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.Angle(flatForward.normalized, flatToPlayer.normalized);
            if (angle > viewAngle * 0.5f)
                return false;
        }

        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 target = player.position + Vector3.up * 1.0f;
        Vector3 dir = target - origin;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, sightRange, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform root = hit.transform.root;
            return root == player || hit.transform == player || hit.transform.IsChildOf(player);
        }

        return false;
    }

    private bool CanHearPlayerRunning()
    {
        if (player == null)
            return false;

        if (playerRigidbody != null)
        {
            float playerSpeed = playerRigidbody.linearVelocity.magnitude;
            if (playerSpeed < runNoiseSpeedThreshold)
                return false;
        }

        return Vector3.Distance(transform.position, player.position) <= hearingRadiusRun;
    }

    private IEnumerator EnterAlert()
    {
        if (state == EnemyState.Alert || state == EnemyState.FakeDeath || state == EnemyState.Stagger)
            yield break;

        SetState(EnemyState.Alert);
        isAlertTriggered = true;
        navAgent.isStopped = true;
        navAgent.ResetPath();

        PlayState(screamStateName, baseAnimLayer);
        if (audioSource != null && screamClip != null)
            audioSource.PlayOneShot(screamClip);

        yield return new WaitForSeconds(Random.Range(1f, 1.5f));

        if (state == EnemyState.Alert)
            SetState(EnemyState.Chase);
    }

    private void SetState(EnemyState next)
    {
        EnemyState previous = state;
        state = next;

        if (previous != next)
            LogStateTransition(previous, next);

        if (navAgent != null)
        {
            bool allowAgentRotation = state != EnemyState.Attack && state != EnemyState.Stagger && state != EnemyState.FakeDeath;
            navAgent.updateRotation = allowAgentRotation;
        }

        if (state == EnemyState.Patrol)
        {
            SetAgentSpeed(patrolSpeed);
            investigateEndTime = -1f;
            lastKnownArrivalTime = -1f;
            isPreAttackStrafing = false;
            navAgent.isStopped = false;
        }
        else if (state == EnemyState.Chase)
        {
            SetAgentSpeed(chaseSpeed);
            navAgent.isStopped = false;
        }
        else if (state == EnemyState.Attack)
        {
            SetAgentSpeed(attackLungeSpeed);
            isPreAttackStrafing = false;
            navAgent.isStopped = false;
        }
        else if (state == EnemyState.AttackRecovery)
        {
            isPreAttackStrafing = false;
            StopAgentMovementHard();
            PlayState(attackRecoveryStateName, baseAnimLayer);
        }
    }

    private void SetAgentSpeed(float speed)
    {
        if (navAgent == null)
            return;

        navAgent.speed = Mathf.Max(0f, speed);
    }

    private void ApplyNavAgentTuning()
    {
        if (navAgent == null)
            return;

        navAgent.acceleration = navAcceleration;
        navAgent.angularSpeed = navAngularSpeed;
        float attackAwareStop = Mathf.Max(0.05f, attackRange - 0.25f);
        navAgent.stoppingDistance = Mathf.Min(navStoppingDistance, attackAwareStop);
        navAgent.autoBraking = navAutoBraking;
    }

    private void UpdatePatrol()
    {
        SetAgentSpeed(patrolSpeed);

        if (waypoints != null && waypoints.Length > 0)
        {
            if (Time.time < nextWaypointMoveTime)
            {
                PlayState(idleStateName, baseAnimLayer);
                return;
            }

            Transform currentPoint = waypoints[currentWaypointIndex];
            if (currentPoint != null)
            {
                navAgent.isStopped = false;
                TrySetDestination(currentPoint.position);
                PlayState(walkingStateName, baseAnimLayer);

                if (!navAgent.pathPending && navAgent.remainingDistance <= waypointStopDistance)
                {
                    nextWaypointMoveTime = Time.time + Mathf.Max(0f, waypointPauseTime);
                    AdvanceWaypoint();
                }
            }

            return;
        }

        if (!walkPointSet)
            SearchWalkPoint();

        if (walkPointSet)
        {
            navAgent.isStopped = false;
            TrySetDestination(walkPoint);
            PlayState(walkingStateName, baseAnimLayer);
        }

        if ((transform.position - walkPoint).magnitude < 1f)
        {
            walkPointSet = false;
            nextWaypointMoveTime = Time.time + Mathf.Max(0f, waypointPauseTime);
        }
    }

    private void SearchWalkPoint()
    {
        float randomZ = Random.Range(-walkPointRange, walkPointRange);
        float randomX = Random.Range(-walkPointRange, walkPointRange);
        Vector3 candidate = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            walkPoint = hit.position;
            walkPointSet = true;
            return;
        }

        if (Physics.Raycast(candidate + Vector3.up, Vector3.down, 4f, groundLayer))
        {
            walkPoint = candidate;
            walkPointSet = true;
        }
    }

    private void AdvanceWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        int next = currentWaypointIndex + 1;
        if (next >= waypoints.Length)
            next = loopPatrol ? 0 : waypoints.Length - 1;

        currentWaypointIndex = next;
    }

    private void BeginDetection(Vector3 investigatePos)
    {
        SetState(EnemyState.Detection);
        SetAgentSpeed(hearingInvestigateSpeed);
        navAgent.isStopped = false;
        TrySetDestination(GetNavMeshPoint(investigatePos, chaseSampleRadius), true);
        PlayState(walkingStateName, baseAnimLayer);
        investigateEndTime = -1f;
    }

    private void UpdateDetection()
    {
        if (CanSeePlayer())
        {
            lastKnownPlayerPosition = player.position;
            if (!isAlertTriggered)
                StartCoroutine(EnterAlert());
            else
                SetState(EnemyState.Chase);
            return;
        }

        navAgent.isStopped = false;
        PlayState(walkingStateName, baseAnimLayer);

        if (!navAgent.pathPending && navAgent.remainingDistance <= waypointStopDistance)
        {
            if (investigateEndTime < 0f)
            {
                investigateEndTime = Time.time + Mathf.Max(0.2f, investigateWaitTime);
                PlayState(idleStateName, baseAnimLayer);
            }
            else if (Time.time >= investigateEndTime)
            {
                SetState(EnemyState.Patrol);
            }
        }
    }

    private void UpdateChase()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        bool seesPlayer = CanSeePlayer();
        bool playerIsAiming = IsPlayerAimingNow();
        float rigidbodySpeed = playerRigidbody != null ? playerRigidbody.linearVelocity.magnitude : 0f;
        float trackedPlayerSpeed = Mathf.Max(rigidbodySpeed, computedPlayerSpeed);

        bool useCloseSpeed = distToPlayer <= closeChaseDistance;
        float selectedSpeed = useCloseSpeed ? chaseCloseSpeed : chaseSpeed;
        if (seesPlayer)
        {
            float baseFallback = playerIsAiming ? Mathf.Max(chaseFallbackPlayerSpeed, 1.3f) : chaseFallbackPlayerSpeed;
            float baselineVisibleSpeed = Mathf.Max(baseFallback, trackedPlayerSpeed);
            float bonus = chaseVisibleCatchupBonus + (playerIsAiming ? chaseAimExtraBonus : 0f);
            float desiredCatchup = baselineVisibleSpeed + bonus;
            float regularCap = useCloseSpeed ? chaseCloseCatchupSpeed : Mathf.Max(chaseCloseCatchupSpeed - 0.25f, chaseSpeed);
            float catchupCap = playerIsAiming ? Mathf.Max(regularCap, chaseAimCatchupSpeed) : regularCap;
            selectedSpeed = Mathf.Max(selectedSpeed, Mathf.Min(catchupCap, desiredCatchup));

            if (playerIsAiming)
                selectedSpeed = Mathf.Max(selectedSpeed, chaseAimMinPursuitSpeed);

            if (playerIsAiming)
                selectedSpeed = Mathf.Max(selectedSpeed, trackedPlayerSpeed + chaseAimGuaranteedLead);
        }
        else
        {
            bool recentLostSight = (Time.time - lastSeenPlayerTime) <= chaseLostSightCatchupTime;
            if (recentLostSight && distToPlayer <= chaseLostSightCatchupDistance)
                selectedSpeed = Mathf.Max(selectedSpeed, chaseLostSightCatchupSpeed);
        }

        SetAgentSpeed(selectedSpeed);
        navAgent.isStopped = false;

        if (isPreAttackStrafing)
        {
            UpdatePreAttackStrafe();
            return;
        }

        Vector3 chaseTarget = seesPlayer ? player.position : lastKnownPlayerPosition;

        if (seesPlayer)
        {
            lastKnownPlayerPosition = player.position;
            lastKnownArrivalTime = -1f;
        }

        if (Time.time >= nextRepathTime)
        {
            float repathInterval = pathRecalcInterval;
            if (playerIsAiming && seesPlayer)
                repathInterval *= chaseAimRepathMultiplier;

            nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);
            TrySetDestination(GetNavMeshPoint(chaseTarget, chaseSampleRadius), playerIsAiming && seesPlayer);
        }

        PlayState(walkingStateName, baseAnimLayer);

        float attackEnterDistance = attackRange + Mathf.Max(0f, attackEnterRangePadding);
        if (Vector3.Distance(transform.position, player.position) <= attackEnterDistance)
        {
            if (TryStartPreAttackStrafe())
                return;

            SetState(EnemyState.Attack);
            return;
        }

        if (!seesPlayer && !navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f)
        {
            if (lastKnownArrivalTime < 0f)
            {
                lastKnownArrivalTime = Time.time;
                PlayState(idleStateName, baseAnimLayer);
            }
            else if (Time.time - lastKnownArrivalTime >= lastKnownHoldTime)
            {
                SetState(EnemyState.Patrol);
            }
        }
    }

    private void UpdateAttack()
    {
        if (state != EnemyState.Attack)
            return;

        if (Time.time < nextAttackAllowedTime)
        {
            SetState(EnemyState.Chase);
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > attackRange + 0.8f)
        {
            SetState(EnemyState.Chase);
            return;
        }

        navAgent.isStopped = false;
        TrySetDestination(GetNavMeshPoint(player.position, chaseSampleRadius), true);
        FacePlayerOnY(attackTurnSpeed);

        if (!alreadyAttacked)
        {
            if (attackRoutine != null)
                StopCoroutine(attackRoutine);

            attackRoutine = StartCoroutine(AttackRoutine());
        }
    }

    private bool TryStartPreAttackStrafe()
    {
        if (!usePreAttackStrafe)
            return false;
        if (Time.time < noStrafeUntilTime)
            return false;
        if (Vector3.Distance(transform.position, player.position) <= preAttackStrafeSkipDistance)
            return false;
        if (Time.time < nextPreAttackStrafeAllowedTime)
            return false;

        float strafeChance = preAttackStrafeChance;
        if (IsPlayerAimingNow())
            strafeChance *= preAttackStrafeAimChanceMultiplier;

        if (Random.value > strafeChance)
            return false;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return false;

        Vector3 dirToPlayer = toPlayer.normalized;
        Vector3 sideDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized;
        if (Random.value < 0.5f)
            sideDir = -sideDir;

        Vector3 desired = transform.position + sideDir * preAttackStrafeDistance + dirToPlayer * 0.25f;
        preAttackStrafeTarget = GetNavMeshPoint(desired, chaseSampleRadius);
        preAttackStrafeEndTime = Time.time + preAttackStrafeDuration;
        nextPreAttackStrafeAllowedTime = Time.time + preAttackStrafeCooldown;
        isPreAttackStrafing = true;

        SetAgentSpeed(preAttackStrafeSpeed);
        TrySetDestination(preAttackStrafeTarget, true);
        FacePlayerOnY(attackTurnSpeed);
        return true;
    }

    private void UpdatePreAttackStrafe()
    {
        if (Vector3.Distance(transform.position, player.position) <= preAttackStrafeAbortDistance)
        {
            isPreAttackStrafing = false;
            SetState(EnemyState.Attack);
            return;
        }

        SetAgentSpeed(preAttackStrafeSpeed);
        navAgent.isStopped = false;
        TrySetDestination(preAttackStrafeTarget, true);
        FacePlayerOnY(attackTurnSpeed);

        bool timeout = Time.time >= preAttackStrafeEndTime;
        bool reached = !navAgent.pathPending && navAgent.remainingDistance <= Mathf.Max(0.15f, navStoppingDistance);
        if (!timeout && !reached)
            return;

        isPreAttackStrafing = false;
        SetState(EnemyState.Attack);
    }

    private IEnumerator AttackRoutine()
    {
        alreadyAttacked = true;
        didHitCurrentSwing = false;

        float windupEnd = Time.time + Mathf.Max(0f, attackWindup);
        while (Time.time < windupEnd)
        {
            if (state != EnemyState.Attack)
                yield break;

            TrySetDestination(GetNavMeshPoint(player.position, chaseSampleRadius), true);
            FacePlayerOnY(attackTurnSpeed);
            yield return null;
        }

        attackAnimStartTime = Time.time;
        PlayState(attackStateName, baseAnimLayer);

        // Lock position during the actual strike so enemy does not slide like a turret.
        StopAgentMovementHard();

        if (knifeHitbox == null)
        {
            int hits = Mathf.Max(1, attackComboHits);
            for (int i = 0; i < hits; i++)
            {
                yield return new WaitForSeconds(attackHitDelayAfterAnimStart);
                yield return TryDealDamageInActiveWindow();
                if (i < hits - 1)
                    yield return new WaitForSeconds(Mathf.Max(0.01f, attackComboHitInterval));
            }
        }
        else
        {
            if (!useAnimationEventsForHitbox)
                AttackHitboxOn();

            float hitWindowEnd = Time.time + Mathf.Max(0.02f, attackActiveWindow);
            while (Time.time < hitWindowEnd)
            {
                FacePlayerOnY(attackTurnSpeed);
                yield return null;
            }

            if (!useAnimationEventsForHitbox)
                AttackHitboxOff();
        }

        yield return new WaitForSeconds(attackHitLockDuration);
        StopAgentMovementHard();

        yield return new WaitForSeconds(Mathf.Max(0.1f, attackCooldown));
        AttackHitboxOff();
        alreadyAttacked = false;
        nextAttackAllowedTime = Time.time + attackStartCooldown;

        if (state == EnemyState.Attack)
        {
            if (!didHitCurrentSwing)
            {
                SetState(EnemyState.AttackRecovery);
                noStrafeUntilTime = Time.time + preAttackNoStrafeAfterMissTime;
                yield return new WaitForSeconds(attackMissRecovery);
            }
            else
            {
                SetState(EnemyState.AttackRecovery);
                noStrafeUntilTime = 0f;
                yield return new WaitForSeconds(postAttackRecovery);
            }

            if (state == EnemyState.AttackRecovery || state == EnemyState.Attack)
                SetState(EnemyState.Chase);
        }

        attackRoutine = null;
    }

    private IEnumerator TryDealDamageInActiveWindow()
    {
        if (player == null || playerHealth == null)
            player?.TryGetComponent(out playerHealth);

        float endTime = Time.time + Mathf.Max(0.02f, attackActiveWindow);
        bool didHit = false;
        while (Time.time <= endTime && !didHit)
        {
            didHit = TryHitPlayerWithAttackHitbox();
            yield return null;
        }
    }

    private bool TryHitPlayerWithAttackHitbox()
    {
        if (player == null || playerHealth == null || playerHealth.IsDead)
            return false;

        Vector3 toPlayer = player.position - transform.position;
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;
        if (flatForward.sqrMagnitude > 0.001f && flatToPlayer.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(flatForward, flatToPlayer);
            if (angle > attackMaxHitAngle)
                return false;
        }

        Vector3 center = transform.position + transform.forward * attackHitboxForwardOffset + Vector3.up * attackHitboxHalfExtents.y;
        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            attackHitboxHalfExtents,
            attackHitBuffer,
            transform.rotation,
            playerLayer,
            QueryTriggerInteraction.Collide);

        // Fallback: if playerLayer is misconfigured in Inspector, try all layers.
        if (hitCount == 0)
        {
            hitCount = Physics.OverlapBoxNonAlloc(
                center,
                attackHitboxHalfExtents,
                attackHitBuffer,
                transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = attackHitBuffer[i];
            if (hit == null)
                continue;

            Transform hitRoot = hit.transform.root;
            if (hitRoot == player)
            {
                playerHealth.ApplyDamage(damage);
                didHitCurrentSwing = true;
                return true;
            }
        }

        return false;
    }

    public void AttackHitboxOn()
    {
        if (state != EnemyState.Attack)
            return;

        float elapsed = Time.time - attackAnimStartTime;
        float remainingDelay = Mathf.Max(0f, attackMinEventDelay - elapsed);
        if (remainingDelay > 0f)
        {
            if (delayedHitboxOnRoutine != null)
                StopCoroutine(delayedHitboxOnRoutine);
            delayedHitboxOnRoutine = StartCoroutine(DelayedHitboxOn(remainingDelay));
            return;
        }

        EnableHitboxWindowNow();
    }

    public void AttackHitboxOff()
    {
        if (delayedHitboxOnRoutine != null)
        {
            StopCoroutine(delayedHitboxOnRoutine);
            delayedHitboxOnRoutine = null;
        }

        attackHitWindowOpen = false;
        if (knifeHitbox != null)
            knifeHitbox.SetActiveWindow(false);
    }

    private IEnumerator DelayedHitboxOn(float delay)
    {
        yield return new WaitForSeconds(delay);
        delayedHitboxOnRoutine = null;

        if (state != EnemyState.Attack)
            yield break;

        EnableHitboxWindowNow();
    }

    private void EnableHitboxWindowNow()
    {
        attackHitWindowOpen = true;
        didHitCurrentSwing = false;
        if (knifeHitbox != null)
            knifeHitbox.SetActiveWindow(true);
    }

    public void OnKnifeHitboxTriggered(Collider other)
    {
        if (!attackHitWindowOpen || didHitCurrentSwing)
            return;
        if (state != EnemyState.Attack)
            return;
        if (other == null)
            return;

        Transform hitRoot = other.transform.root;
        if (player == null || hitRoot != player)
            return;

        if (playerHealth == null)
            player.TryGetComponent(out playerHealth);

        if (playerHealth == null || playerHealth.IsDead)
            return;

        playerHealth.ApplyDamage(damage);
        didHitCurrentSwing = true;
    }

    public void TakeDamage(float incomingDamage)
    {
        processingWeaponDamage = false;
        TakeDamageInternal(incomingDamage);
    }

    public void TakeWeaponDamage(float incomingDamage)
    {
        processingWeaponDamage = true;
        TakeDamageInternal(incomingDamage);
        processingWeaponDamage = false;
    }

    private void TakeDamageInternal(float incomingDamage)
    {
        if (isFinisherExecutionLocked)
        {
            LogDamage($"blocked: finisher lock active, incoming={incomingDamage:0.##}");
            return;
        }

        if (isPermanentlyDead || state == EnemyState.FakeDeath)
        {
            LogDamage($"blocked: state={state}, isPermanentlyDead={isPermanentlyDead}, incoming={incomingDamage:0.##}");
            return;
        }

        if (state == EnemyState.Stagger && isKnockoutStunActive && health <= 0f)
        {
            LogDamage($"blocked: already in finisher stun, incoming={incomingDamage:0.##}");
            return;
        }

        if (!canBeKilled)
        {
            LogDamage($"blocked: canBeKilled=false, incoming={incomingDamage:0.##}");
            return;
        }

        if (incomingDamage <= 0f)
        {
            LogDamage($"blocked: non-positive incoming damage={incomingDamage:0.##}");
            return;
        }

        if (processingWeaponDamage && oneShotKillFromWeapon)
            incomingDamage = Mathf.Max(incomingDamage, health);

        float healthBefore = health;
        health -= incomingDamage;
        health = Mathf.Max(0f, health);
        LogDamage($"passed: -{incomingDamage:0.##}, hp {healthBefore:0.##} -> {health:0.##}");

        if (hitEffect != null)
            hitEffect.Play();

        if (health <= 0f)
        {
            knockoutCount++;
            if (useFakeDeath && knockoutCount < knockoutsToPermanentDeath)
            {
                LogDamage($"result: knockout {knockoutCount}/{knockoutsToPermanentDeath} -> fake death + revive");
                EnterFakeDeath();
            }
            else if (useFinalKnockoutStun)
            {
                LogDamage($"result: knockout {knockoutCount}/{knockoutsToPermanentDeath} -> final finisher stun");
                EnterFinalKnockoutStun();
            }
            else
            {
                LogDamage($"result: knockout {knockoutCount}/{knockoutsToPermanentDeath} -> permanent death");
                EnterPermanentDeath();
            }
            return;
        }

        isKnockoutStunActive = false;

        if (staggerRoutine != null)
            StopCoroutine(staggerRoutine);

        staggerRoutine = StartCoroutine(StaggerRoutine());
    }

    private void LogDamage(string message)
    {
        if (!debugDamageLogs)
            return;

        Debug.Log($"Enemytest[{name}] damage: {message}", this);
    }

    private void LogStateTransition(EnemyState from, EnemyState to)
    {
        if (!debugStateLogs)
            return;

        Debug.Log($"Enemytest[{name}] state: {from} -> {to}", this);
    }

    private void UpdatePlayerSpeedEstimate()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float frameSpeed = Vector3.Distance(player.position, lastPlayerPosition) / deltaTime;
        computedPlayerSpeed = Mathf.Lerp(computedPlayerSpeed, frameSpeed, 0.45f);
        lastPlayerPosition = player.position;
    }

    private bool IsPlayerAimingNow()
    {
        if (playerWeaponHandler == null && player != null)
            player.TryGetComponent(out playerWeaponHandler);

        return playerWeaponHandler != null && playerWeaponHandler.IsAiming;
    }

    private IEnumerator StaggerRoutine()
    {
        EnemyState returnState = state == EnemyState.Attack ? EnemyState.Chase : state;
        state = EnemyState.Stagger;
        isKnockoutStunActive = false;
        navAgent.isStopped = true;
        PlayStaggerState();

        yield return new WaitForSeconds(Mathf.Max(0.05f, staggerDuration));

        if (state == EnemyState.Stagger)
        {
            if (CanSeePlayer() || returnState == EnemyState.Chase || returnState == EnemyState.Attack)
                SetState(EnemyState.Chase);
            else
                SetState(EnemyState.Patrol);
        }

        staggerRoutine = null;
    }

    private void EnterFakeDeath()
    {
        StartKnockoutFlow(false);
    }

    private void StartKnockoutFlow(bool permanentAfterKnockout)
    {
        if (knockoutRoutine != null)
        {
            StopCoroutine(knockoutRoutine);
            knockoutRoutine = null;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (fakeDeathRoutine != null)
        {
            StopCoroutine(fakeDeathRoutine);
            fakeDeathRoutine = null;
        }

        if (staggerRoutine != null)
        {
            StopCoroutine(staggerRoutine);
            staggerRoutine = null;
        }

        AttackHitboxOff();
        knockoutRoutine = StartCoroutine(KnockoutFlowRoutine(permanentAfterKnockout));
    }

    private IEnumerator KnockoutFlowRoutine(bool permanentAfterKnockout)
    {
        SetState(EnemyState.Stagger);
        isKnockoutStunActive = true;
        StopAgentMovementHard();
        PlayStaggerState();

        float stunWait = Mathf.Max(0.05f, knockoutStunDuration, knockoutMinVisibleStunDuration);
        yield return new WaitForSeconds(stunWait);

        if (!permanentAfterKnockout && instantWakeAfterKnockoutStun)
        {
            isKnockoutStunActive = false;
            health = Mathf.Max(1f, maxHealth * Mathf.Clamp01(reviveHealthPercent));
            PlayState(wakeUpStateName, baseAnimLayer);
            yield return new WaitForSeconds(0.05f);
            SetState(EnemyState.Patrol);
            knockoutRoutine = null;
            yield break;
        }

        isKnockoutStunActive = false;
        SetState(EnemyState.FakeDeath);
        health = 0f;
        navAgent.isStopped = true;
        navAgent.ResetPath();
        PlayState(fakeDeathStateName, baseAnimLayer);

        if (permanentAfterKnockout)
        {
            isPermanentlyDead = true;
            if (!keepBodyAfterPermanentDeath)
                Destroy(gameObject, permanentDeathDestroyDelay);

            knockoutRoutine = null;
            yield break;
        }

        fakeDeathRoutine = StartCoroutine(FakeDeathRoutine());
        knockoutRoutine = null;
    }

    private void EnterPermanentDeath()
    {
        StartKnockoutFlow(true);
    }

    private void EnterFinalKnockoutStun()
    {
        if (knockoutRoutine != null)
        {
            StopCoroutine(knockoutRoutine);
            knockoutRoutine = null;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (fakeDeathRoutine != null)
        {
            StopCoroutine(fakeDeathRoutine);
            fakeDeathRoutine = null;
        }

        if (staggerRoutine != null)
        {
            StopCoroutine(staggerRoutine);
            staggerRoutine = null;
        }

        health = 0f;
        SetState(EnemyState.Stagger);
        isKnockoutStunActive = true;
        AttackHitboxOff();
        StopAgentMovementHard();
        PlayStaggerState();
    }

    private IEnumerator FakeDeathRoutine()
    {
        float reviveDelay = Random.Range(Mathf.Min(fakeDeathReviveMin, fakeDeathReviveMax), Mathf.Max(fakeDeathReviveMin, fakeDeathReviveMax));
        yield return new WaitForSeconds(Mathf.Max(1f, reviveDelay));

        if (isBurned || isPermanentlyDead)
            yield break;

        health = Mathf.Max(1f, maxHealth * Mathf.Clamp01(reviveHealthPercent));
        PlayState(wakeUpStateName, baseAnimLayer);
        yield return new WaitForSeconds(1.1f);

        SetState(EnemyState.Patrol);
        fakeDeathRoutine = null;
    }

    public void Burn()
    {
        if (isPermanentlyDead || isFinisherExecutionLocked)
            return;

        isKnockoutStunActive = false;
        isBurned = true;
        isPermanentlyDead = true;

        if (fakeDeathRoutine != null)
        {
            StopCoroutine(fakeDeathRoutine);
            fakeDeathRoutine = null;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        Destroy(gameObject, 0.2f);
    }

    public bool CanBeFinished()
    {
        return !isPermanentlyDead && state == EnemyState.Stagger && isKnockoutStunActive;
    }

    public bool IsInDeathStateOrDead()
    {
        return isPermanentlyDead || state == EnemyState.FakeDeath;
    }

    public void KillDuringStun()
    {
        if (!CanBeFinished() || isFinisherExecutionLocked)
            return;

        if (knockoutRoutine != null)
        {
            StopCoroutine(knockoutRoutine);
            knockoutRoutine = null;
        }

        if (staggerRoutine != null)
        {
            StopCoroutine(staggerRoutine);
            staggerRoutine = null;
        }

        if (fakeDeathRoutine != null)
        {
            StopCoroutine(fakeDeathRoutine);
            fakeDeathRoutine = null;
        }

        if (finisherDeathRoutine != null)
        {
            StopCoroutine(finisherDeathRoutine);
            finisherDeathRoutine = null;
        }

        AttackHitboxOff();
        isKnockoutStunActive = false;
        isPermanentlyDead = true;
        health = 0f;
        isFinisherExecutionLocked = true;
        finisherDeathRoutine = StartCoroutine(FinisherDeathRoutine());
    }

    private IEnumerator FinisherDeathRoutine()
    {
        StopAgentMovementHard();

        Vector3 startPos = transform.position;
        Vector3 away = Vector3.zero;
        if (player != null)
        {
            away = startPos - player.position;
            away.y = 0f;
        }

        if (away.sqrMagnitude < 0.0001f)
            away = -transform.forward;

        Vector3 desired = startPos + away.normalized * finisherKnockbackDistance;
        Vector3 targetPos = desired;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 0.8f, NavMesh.AllAreas))
            targetPos = hit.position;

        float duration = Mathf.Max(0.02f, finisherKnockbackDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * finisherKnockbackArcHeight;
            transform.position = pos;

            if (navAgent != null)
                navAgent.nextPosition = transform.position;

            yield return null;
        }

        transform.position = targetPos;
        if (navAgent != null)
            navAgent.nextPosition = transform.position;

        SetState(EnemyState.FakeDeath);
        PlayState(fakeDeathStateName, baseAnimLayer);

        if (!keepBodyAfterPermanentDeath)
            Destroy(gameObject, permanentDeathDestroyDelay);

        finisherDeathRoutine = null;
    }

    private void FacePlayerOnY()
    {
        if (player == null)
            return;

        Vector3 target = player.position;
        target.y = transform.position.y;
        transform.LookAt(target);
    }

    private void FacePlayerOnY(float degreesPerSecond)
    {
        if (player == null)
            return;

        Vector3 toTarget = player.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        float step = Mathf.Max(0f, degreesPerSecond) * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, step);
    }

    private Vector3 GetNavMeshPoint(Vector3 source, float radius)
    {
        if (NavMesh.SamplePosition(source, out NavMeshHit hit, Mathf.Max(0.3f, radius), NavMesh.AllAreas))
            return hit.position;

        return source;
    }

    private bool TrySetDestination(Vector3 target, bool force = false)
    {
        if (navAgent == null || !navAgent.isOnNavMesh)
            return false;

        if (!force && hasLastRequestedDestination)
        {
            float delta = Vector3.Distance(lastRequestedDestination, target);
            if (delta < destinationUpdateThreshold)
                return false;
        }

        navAgent.SetDestination(target);
        lastRequestedDestination = target;
        hasLastRequestedDestination = true;
        return true;
    }

    private void StopAgentMovementHard()
    {
        if (navAgent == null || !navAgent.isOnNavMesh)
            return;

        navAgent.isStopped = true;
        if (navAgent.hasPath)
            navAgent.ResetPath();

        navAgent.velocity = Vector3.zero;
        navAgent.nextPosition = transform.position;
    }

    private void HandlePlayerShotFired(Vector3 shotPosition, float loudness)
    {
        if (state == EnemyState.FakeDeath || state == EnemyState.Stagger)
            return;

        float radius = hearingRadiusShot * Mathf.Max(0.2f, loudness);
        if (Vector3.Distance(transform.position, shotPosition) > radius)
            return;

        lastKnownPlayerPosition = shotPosition;

        if (!isAlertTriggered)
        {
            BeginDetection(shotPosition);
            return;
        }

        if (state != EnemyState.Attack)
            SetState(EnemyState.Chase);
    }

    public void NotifyNoise(Vector3 noiseWorldPos, float loudness = 1f)
    {
        if (state == EnemyState.FakeDeath || state == EnemyState.Stagger)
            return;

        float radius = hearingRadiusRun * Mathf.Max(0.2f, loudness);
        if (Vector3.Distance(transform.position, noiseWorldPos) <= radius)
            BeginDetection(noiseWorldPos);
    }

    private void PlayState(string stateName, int layer)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;
        if (stateName == currentAnimState)
            return;
        if (!HasState(animator, layer, stateName))
            return;

        animator.CrossFadeInFixedTime(stateName, animTransition, layer);
        currentAnimState = stateName;
    }

    private static bool HasState(Animator targetAnimator, int layer, string stateName)
    {
        if (targetAnimator == null || string.IsNullOrEmpty(stateName))
            return false;
        return targetAnimator.HasState(layer, Animator.StringToHash(stateName));
    }

    private void PlayStaggerState()
    {
        if (HasState(animator, baseAnimLayer, staggerStateName))
        {
            PlayState(staggerStateName, baseAnimLayer);
            return;
        }

        if (HasState(animator, baseAnimLayer, staggerFallbackStateName))
        {
            PlayState(staggerFallbackStateName, baseAnimLayer);
            return;
        }

        PlayState(idleStateName, baseAnimLayer);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearingRadiusRun);

        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward * sightRange;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward * sightRange;
        Gizmos.DrawRay(transform.position, left);
        Gizmos.DrawRay(transform.position, right);

        if (waypoints != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                    continue;

                Gizmos.DrawSphere(waypoints[i].position, 0.15f);

                int next = i + 1;
                if (next < waypoints.Length && waypoints[next] != null)
                    Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
            }
        }

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Vector3 hitboxCenter = transform.position + transform.forward * attackHitboxForwardOffset + Vector3.up * attackHitboxHalfExtents.y;
        Gizmos.matrix = Matrix4x4.TRS(hitboxCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, attackHitboxHalfExtents * 2f);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
