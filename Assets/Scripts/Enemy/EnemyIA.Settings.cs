using UnityEngine;
using UnityEngine.AI;

public partial class AdvancedEnemyAI 
{
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private float speedWalk = 2f;
    [SerializeField] private float speedRun = 2f;

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private bool useRandomPatrolWhenNoWaypoints = true;
    [SerializeField] private float randomPatrolRadius = 10f;
    [SerializeField] private float randomPatrolPointTolerance = 0.8f;
    [SerializeField] private float randomPatrolWait = 0.6f;

    [Header("Scene Speeds")]
    [SerializeField] private float patrolSpeed = 1.35f;
    [SerializeField] private float searchSpeed = 1.45f;
    [SerializeField] private float chaseSpeed = 1.65f;

    [Header("Agent Feel")]
    [SerializeField, Tooltip("Ускорение NavMeshAgent (меньше = тяжелее разгон)")]
    private float agentAcceleration = 4.2f;
    [SerializeField, Tooltip("Поворот NavMeshAgent в град/сек (меньше = тяжелее разворот)")]
    private float agentAngularSpeed = 115f;

    [Header("Detection")]
    [SerializeField] private float viewRadius = 15f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField, Tooltip("Маска для проверки прямой видимости")]
    private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;
    [SerializeField] private Vector3 sightOriginOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector3 sightTargetOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Tooltip("Дистанция остановки перед игроком, чтобы не толкать")]
    private float stopBeforePlayerDistance = 1.8f;

    [Header("Aggression")]
    [SerializeField, Tooltip("Множитель скорости, когда враг очень близко к игроку")]
    private float chaseCloseSpeedMultiplier = 0.28f;
    [SerializeField, Tooltip("Плавность снижения скорости при приближении (больше = медленнее ближе)")]
    private float chaseSpeedFalloffPower = 2.2f;

    [Header("Approach")]
    [SerializeField, Tooltip("Скорость неспешного сближения до атаки")]
    private float approachSpeed = 1.55f;
    [SerializeField, Tooltip("Дистанция, на которой враг начинает ускоряться (0 = не ускоряться)")]
    private float approachDistance = 4.5f;

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
    [SerializeField, Tooltip("Множитель скорости анимации на время удара/захвата")]
    private float attackAnimationSpeed = 1f;
    [SerializeField, Tooltip("Максимальный угол (в градусах) между forward врага и направлением на игрока для атаки/урона")]
    private float maxAttackAngle = 60f;
    [SerializeField, Tooltip("Автоповорот к игроку перед атакой (только по оси Y)")]
    private bool facePlayerOnAttack = true;
    [SerializeField, Tooltip("Радиус хитбокса удара (OverlapSphere)")]
    private float attackHitRadius = 0.8f;
    [SerializeField, Tooltip("Смещение хитбокса вперёд вдоль forward")]
    private float attackHitForwardOffset = 0.5f;
    [SerializeField, Tooltip("Множитель скорости NavMeshAgent во время атаки")]
    private float attackMoveSpeedMultiplier = 0f;
    [SerializeField, Tooltip("Скорость плавного изменения скорости при атаке")]
    private float attackSpeedLerp = 6f;
    [SerializeField, Tooltip("Отключать коллизии с игроком во время атаки")]
    private bool disableCollisionDuringAttack = true;

    [Header("Post Attack Step")]
    [SerializeField, Tooltip("После атаки отойти в сторону")]
    private bool postAttackSideStepEnabled = true;
    [SerializeField, Tooltip("Дистанция бокового шага")]
    private float postAttackSideStepDistance = 1.5f;
    [SerializeField, Tooltip("Длительность бокового шага (сек)")]
    private float postAttackSideStepDuration = 0.6f;
    [SerializeField, Tooltip("Скорость бокового шага")]
    private float postAttackSideStepSpeed = 2f;

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



    [Header("Revive")]
    [SerializeField] private bool reviveEnabled = true;
    [SerializeField] private float reviveDelayMin = 8f;
    [SerializeField] private float reviveDelayMax = 12f;
    [SerializeField, Range(0f, 1f)] private float reviveHealthPercentMin = 0.3f;
    [SerializeField, Range(0f, 1f)] private float reviveHealthPercentMax = 0.5f;
    [SerializeField] private float maxHealth = 100f;
}
