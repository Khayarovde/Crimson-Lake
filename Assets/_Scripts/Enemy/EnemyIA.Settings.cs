using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public partial class AdvancedEnemyAI 
{
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private float speedWalk = 2f;
    [SerializeField, Tooltip("Если включено — враг не ходит (патруль/поиск/преследование отключены)")]
    private bool disableMovement = false;

    [Header("Patrol")]
    [SerializeField, Tooltip("Список точек патруля. Если пусто, используется случайный патруль")]
    private Transform[] waypoints;
    [SerializeField] private bool loopPatrol = true;
    [SerializeField] private float waypointPauseTime = 0.6f;
    [SerializeField] private float randomPatrolRadius = 10f;
    [SerializeField] private float randomPatrolPointTolerance = 0.8f;
    [SerializeField] private float randomPatrolWait = 0.6f;

    [Header("Detection")]
    [SerializeField] private float viewRadius = 15f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField, Tooltip("Ближняя зона обнаружения (360 градусов при наличии прямой видимости)")]
    private float closeAwarenessRadius = 3.5f;
    [SerializeField, Tooltip("Широкая периферийная зона обнаружения")]
    private float peripheralViewRadius = 9f;
    [SerializeField, Range(1f, 360f), Tooltip("Угол периферийного зрения")]
    private float peripheralViewAngle = 170f;
    [SerializeField, Tooltip("Маска для проверки прямой видимости")]
    private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;
    [SerializeField, Tooltip("Маска поиска целей в зоне зрения")]
    private LayerMask playerDetectionMask = ~0;
    [SerializeField] private Vector3 sightOriginOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector3 sightTargetOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Tooltip("Дистанция остановки перед игроком, чтобы не толкать")]
    private float stopBeforePlayerDistance = 1.8f;

    [Header("Alert / Scream")]
    [SerializeField] private float alertDuration = 1.1f;
    [SerializeField] private string screamStateName = "Scream";
    [SerializeField] private AudioClip screamClip;

    [Header("Chase")]
    [SerializeField] private float speedChaseMin = 3.8f;
    [SerializeField] private float speedChaseMax = 5.6f;
    [SerializeField] private float chaseErraticChangeRate = 2.2f;

    [Header("Hearing")]
    [SerializeField, Tooltip("Базовый радиус слуха врага")]
    private float hearingRadius = 18f;
    [SerializeField, Tooltip("Как долго враг проверяет последнюю позицию выстрела")]
    private float investigateShotDuration = 5f;
    [SerializeField] private bool listenToPlayerRunNoise = true;
    [SerializeField] private float playerRunSpeedThreshold = 3.2f;
    [SerializeField] private float playerRunLoudness = 1f;
    [SerializeField] private float runNoiseCheckInterval = 0.2f;

    [Header("Scan On Spawn")]
    [SerializeField] private bool scanOnSpawn = true;
    [SerializeField] private float scanDuration = 2.5f;
    [SerializeField] private float scanTurnSpeed = 120f;

    [Header("Search")]
    [SerializeField] private float searchRadius = 6f;
    [SerializeField] private int searchPointsCount = 3;
    [SerializeField] private float searchPointTolerance = 0.6f;
    [SerializeField] private float searchPointWait = 0.6f;
    [SerializeField] private float repathInterval = 0.3f;

    [Header("Catch Settings")]
    [SerializeField] private AudioClip catchSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 3.5f;
    [SerializeField, Tooltip("Задержка от старта удара до нанесения урона (реальное время)")]
    private float attackWindupTime = 0.4f;
    [SerializeField, Range(0f, 1f), Tooltip("Нормализованное время удара (доля анимации)")]
    private float attackHitNormalizedTime = 0.7f;
    [SerializeField, Tooltip("Длительность анимации удара (сек)")]
    private float attackAnimationDuration = 1.2f;
    [SerializeField, Tooltip("Сколько времени враг стоит на месте во время удара (реальное время)")]
    private float attackLockTime = 0.8f;
    [SerializeField, Tooltip("Максимальный угол (в градусах) между forward врага и направлением на игрока для атаки/урона")]
    private float maxAttackAngle = 60f;
    [SerializeField, Tooltip("Автоповорот к игроку перед атакой (только по оси Y)")]
    private bool facePlayerOnAttack = true;
    [SerializeField, Tooltip("Радиус хитбокса удара (OverlapSphere)")]
    private float attackHitRadius = 0.8f;
    [SerializeField, Tooltip("Смещение хитбокса вперёд вдоль forward")]
    private float attackHitForwardOffset = 0.5f;
    [SerializeField, Tooltip("Отключать коллизии с игроком во время атаки")]
    private bool disableCollisionDuringAttack = true;

    [Header("Post Attack Step")]
    [SerializeField, Tooltip("После атаки отойти в сторону")]
    private bool postAttackSideStepEnabled = true;
    [SerializeField, Tooltip("Дистанция бокового шага")]
    private float postAttackSideStepDistance = 1.5f;
    [SerializeField, Tooltip("Длительность бокового шага (сек)")]
    private float postAttackSideStepDuration = 0.6f;

    [Header("Attack Variants")]
    [SerializeField, Range(0f, 5f)] private float attack1Weight = 1f;
    [SerializeField, Range(0f, 5f)] private float attack2Weight = 1f;
    [SerializeField, Range(0f, 5f)] private float attack3Weight = 1f;

    [Header("Animation")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string walkingStateName = "walking";
    [SerializeField, Tooltip("Передавать в Animator bool 'isChasing' (для бега). По умолчанию выключено.")]
    private bool useChaseAnimatorFlag = false;
    [SerializeField, Tooltip("Порог скорости для включения walking")]
    private float movementAnimStartSpeed = 0.2f;
    [SerializeField, Tooltip("Порог скорости для возврата в idle")]
    private float movementAnimStopSpeed = 0.08f;
    [SerializeField, Tooltip("Минимальный интервал между переключениями idle/walking")]
    private float movementAnimSwitchCooldown = 0.12f;
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

    [Header("Attack Events")]
    [SerializeField] private UnityEvent onAttackStarted;
    [SerializeField] private UnityEvent onAttackHit;
    [SerializeField] private UnityEvent onAttackFinished;

    [Header("Hitbox")]
    [SerializeField, Tooltip("Радиус капсулы коллайдера врага (для более лёгкого попадания)")]
    private float enemyColliderRadius = 0.7f;
    [SerializeField, Tooltip("Высота капсулы коллайдера врага")]
    private float enemyColliderHeight = 2.2f;
    [SerializeField, Tooltip("Центр капсулы коллайдера врага")]
    private Vector3 enemyColliderCenter = new Vector3(0f, 1.1f, 0f);



    [Header("Revive")]
    [SerializeField] private bool reviveEnabled = true;
    [SerializeField] private bool destroyOnBurn = false;
    [SerializeField] private float reviveDelayMin = 60f;
    [SerializeField] private float reviveDelayMax = 120f;
    [SerializeField, Range(0f, 1f)] private float reviveHealthPercentMin = 0.5f;
    [SerializeField, Range(0f, 1f)] private float reviveHealthPercentMax = 0.5f;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool isPermanentlyDead = false;

    [Header("Debug")]
    [SerializeField] private EnemyState currentStateDebug = EnemyState.Patrol;
}
