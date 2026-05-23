using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Босс с двумя фазами поведения и "ломаемой" полосой.
///
/// Фаза 1 (BossPhase.Phase1):
///   — Постоянно преследует игрока через NavMesh.
///   — Периодически делает ТАРАН (Attack на слое Ruka).
///   — Во время прицеливания перед тараном тормозит.
///
/// Фаза 2 (BossPhase.Phase2, активируется при HP <= phase2HealthThreshold):
///   — Все механики фазы 1 остаются.
///   — Добавляется способность СПАВНИТЬ ЛЕСКИ: перед собой и за собой.
///   — При получении урона: если игрок агрессивен, босс временно замедляется.
///
/// Получение урона (броня + окно уязвимости):
///   — В броне урон не проходит в HP, а копится в счётчик пробития.
///   — При пробитии: GetDown -> Tryaska(1/2) (окно урона) -> wakeUp_stun.
///   — HP уменьшается только во время Tryaska.
///   — После wakeUp броня восстанавливается через задержку.
///   — Если HP <= 0, переходит в death_end.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class BossEnemy : MonoBehaviour
{
    // ─── Фаза ────────────────────────────────────────────────────────────────

    public enum BossPhase { Phase1, Phase2 }

    [Header("Phase")]
    [Tooltip("Порог HP (0..1 = процент от максимума), при котором активируется фаза 2.")]
    [Range(0f, 0.99f)] public float phase2HealthThreshold = 0.5f;

    // ─── Здоровье ─────────────────────────────────────────────────────────────

    [Header("Health")]
    public float maxHealth = 300f;
    [HideInInspector] public float health;


    // ─── Броня / окно уязвимости ────────────────────────────────────────────

    [Header("Armor / Vulnerability")]
    [Tooltip("Суммарный урон по броне, после которого она ломается.")]
    public float armorBreakThreshold = 90f;
    [Tooltip("Задержка восстановления брони после wakeUp (сек).")]
    public float armorRegenDelay = 8f;
    [Tooltip("Длительность анимации GetDown (сек).")]
    public float getDownDuration = 0.7f;
    [Tooltip("Длительность анимации Tryaska1/Tryaska2 (сек).")]
    public float tryaskaDuration = 5.0f;
    [Tooltip("Длительность анимации wakeUp_stun (сек).")]
    public float wakeUpDuration = 0.8f;

    // ─── Движение ─────────────────────────────────────────────────────────────

    [Header("Movement")]
    public float chaseSpeed = 3.5f;
    [Tooltip("Скорость во время прицеливания перед тараном.")]
    public float aimingSpeed = 1.2f;
    [Tooltip("Скорость самого тарана.")]
    public float ramSpeed = 9f;

    // ─── Таран ────────────────────────────────────────────────────────────────

    [Header("Ram Attack")]
    [Tooltip("Расстояние до игрока, при котором начинается прицеливание.")]
    public float ramTriggerDistance = 5f;
    [Tooltip("Длительность фазы прицеливания перед тараном.")]
    public float ramAimDuration = 0.8f;
    [Tooltip("Минимальная длительность прицеливания (рандомизация).")]
    public float ramAimDurationMin = 0.5f;
    [Tooltip("Максимальная длительность прицеливания (рандомизация).")]
    public float ramAimDurationMax = 0.95f;
    [Tooltip("Длительность самого рывка.")]
    public float ramDuration = 0.6f;
    [Tooltip("Урон от тарана.")]
    public int ramDamage = 25;
    [Tooltip("Кулдаун между таранами.")]
    public float ramCooldown = 4f;
    [Tooltip("Радиус хитбокса при ударе тарана.")]
    public float ramHitRadius = 1.2f;
    [Tooltip("Минимальная скорость рывка (рандомизация).")]
    public float ramSpeedMin = 8.5f;
    [Tooltip("Максимальная скорость рывка (рандомизация).")]
    public float ramSpeedMax = 10.5f;
    [Tooltip("Пауза между первым и вторым рывком двойного тарана.")]
    public float doubleRamGap = 0.25f;
    [Tooltip("Множитель времени прицеливания для второго рывка двойного тарана.")]
    public float doubleRamSecondAimScale = 0.6f;

    [Header("Adaptive Ram (Anti-Dodge)")]
    public bool enableAdaptiveRam = true;

    [Header("Ram Target Lag")]
    public bool useLaggedRamTarget = true;
    [Tooltip("Минимальная задержка позиции игрока для тарана (сек).")]
    public float ramTargetLagMin = 0.3f;
    [Tooltip("Максимальная задержка позиции игрока для тарана (сек).")]
    public float ramTargetLagMax = 0.4f;

    [Header("Close Attack (Ruka Attack2)")]
    [Tooltip("Если игрок слишком близко, проигрывается Attack2 на слое Ruka.")]
    public float closeAttackDistance = 1.6f;
    [Tooltip("Во сколько раз дальше отрабатывает приоритет Attack2, даже если игрок ещё не в прямом радиусе удара.")]
    [Range(1f, 3f)] public float closeAttackPreferRangeMultiplier = 1.8f;
    [Tooltip("Шанс выбрать Attack2 на границе зоны предпочтения (чем ближе, тем выше шанс).")]
    [Range(0f, 1f)] public float closeAttackPreferChanceAtEdge = 0.25f;
    [Tooltip("Шанс выбрать Attack2 почти вплотную к боссу, если Attack2 уже доступен по дистанции.")]
    [Range(0f, 1f)] public float closeAttackPreferChanceNear = 0.95f;
    [Tooltip("Урон ближней атаки.")]
    public int closeAttackDamage = 15;
    [Tooltip("Кулдаун между близкими атаками.")]
    public float closeAttackCooldown = 1.1f;

    [Header("Phase 1 - Reverse Ram")]
    [Tooltip("После первого тарана делает второй рывок в обратном направлении.")]
    public bool enableReverseRam = true;
    [Tooltip("Время разворота перед обратным рывком (сек).")]
    public float reverseRamTurnDuration = 0.2f;
    [Tooltip("Множитель длительности обратного рывка.")]
    public float reverseRamDurationScale = 0.75f;
    [Tooltip("Множитель скорости обратного рывка.")]
    public float reverseRamSpeedScale = 0.95f;

    [Header("Hit Detection")]
    [Tooltip("Смещение точки попадания ближней атаки вперёд от босса (м).")]
    public float closeHitForwardOffset = 0.8f;
    [Tooltip("Вертикальное смещение точки попадания ближней атаки (м).")]
    public float closeHitUpOffset = 0.8f;
    [Tooltip("Задержка перед нанесением урона ближней атакой (сек).")]
    public float closeHitDelay = 0.1f;
    [Tooltip("Длительность окна попадания ближней атаки (сек).")]
    public float closeHitWindow = 0.25f;
    [Tooltip("Смещение точки попадания тарана вперёд от босса (м).")]
    public float ramHitForwardOffset = 0.9f;
    [Tooltip("Вертикальное смещение точки попадания тарана (м).")]
    public float ramHitUpOffset = 0.8f;
    [Tooltip("Вертикальное смещение точки цели (игрока) для расчёта попадания (м).")]
    public float playerHitUpOffset = 0.9f;
    [Tooltip("Коэффициент примагничивания точки попадания Attack2 к игроку (0 = расчётная точка, 1 = позиция игрока).")]
    [Range(0f, 1f)] public float closeAttackMagnetization = 0.35f;

    [Header("Chase Tuning")]
    [Tooltip("Минимальный интервал пересчёта пути при обычном преследовании (сек).")]
    [Range(0.01f, 0.2f)] public float chaseRepathInterval = 0.03f;
    [Tooltip("Минимальный сдвиг позиции игрока, после которого босс обновляет путь (м).")]
    [Range(0.01f, 1f)] public float chaseRepathDistanceThreshold = 0.1f;

    [Header("Phase 2 - Shark Spin")]
    [Tooltip("Кулдаун между вращениями (сек).")]
    public float sharkCooldown = 6f;
    [Tooltip("Длительность вращения (сек).")]
    public float sharkSpinDuration = 0.5f;
    [Tooltip("Радиус урона вращения.")]
    public float sharkRadius = 2.2f;
    [Tooltip("Урон вращения.")]
    public int sharkDamage = 18;
    [Tooltip("Макс. дистанция до игрока для запуска вращения.")]
    public float sharkTriggerDistance = 3.0f;
    [Tooltip("Требуемый угол за спиной (градусы). 180 = строго сзади.")]
    public float sharkBehindAngle = 120f;
    [Tooltip("Маска слоёв для OverlapSphere вращения.")]
    public LayerMask sharkHitMask = ~0;

    [Header("Phase 2 - Feint Ram")]
    [Tooltip("Минимальное время до прерывания тарана (сек).")]
    public float feintMinTime = 0.12f;
    [Tooltip("Максимальное время до прерывания тарана (сек).")]
    public float feintMaxTime = 0.28f;
    [Tooltip("Дистанция шага в сторону при финте (м).")]
    public float feintSidestepDistance = 1.1f;
    [Tooltip("Длительность шага в сторону при финте (сек).")]
    public float feintSidestepDuration = 0.18f;

    // ─── Замедление при уроне (фаза 2) ────────────────────────────────────────

    [Header("Hit Slowdown (Phase 2)")]
    [Tooltip("Насколько снижается скорость при замедлении (множитель, например 0.4 = -60%).")]
    [Range(0f, 1f)] public float slowSpeedMultiplier = 0.4f;
    [Tooltip("Длительность замедления в секундах.")]
    public float slowDuration = 0.5f;

    // ─── Лески (фаза 2) ────────────────────────────────────────────────────────

    [Header("Leska Traps (Phase 2)")]
    [Tooltip("Префаб объекта-лески (BossLeskaObject).")]
    public BossLeskaObject leskaPrefab;
    [Tooltip("Урон от касания лески.")]
    public float leskaDamage = 20f;
    [Tooltip("Сколько секунд леска лежит на арене.")]
    public float leskaLifetime = 3.5f;
    [Tooltip("Локальная точка спавна лески перед боссом (X=вбок, Y=вверх, Z=вперёд).")]
    public Vector3 leskaSpawnOffsetFront = new Vector3(0f, 0f, 1.5f);
    [Tooltip("Локальная точка спавна лески за боссом (X=вбок, Y=вверх, Z=назад).")]
    public Vector3 leskaSpawnOffsetBack = new Vector3(0f, 0f, -1.5f);
    [Tooltip("Кулдаун между спавном лесок.")]
    public float leskaCooldown = 5f;
    [Tooltip("Длительность анимации спавна лески (босс стоит на месте).")]
    public float leskaSpawnAnimDuration = 0.9f;

    // ─── NavMesh ──────────────────────────────────────────────────────────────


    // ─── Анимация ─────────────────────────────────────────────────────────────

    [Header("Animations - Base Layer")]
    [SerializeField] private string baseLayerName = "Base Layer";
    [SerializeField] private string baseWalkAnim = "walking";
    [SerializeField] private string baseGetDownAnim = "GetDown";
    [SerializeField] private string baseTryaska1Anim = "Tryaska1";
    [SerializeField] private string baseTryaska2Anim = "Tryaska2";
    [SerializeField] private string baseWakeUpAnim = "wakeUp_stun";
    [SerializeField] private string baseDeathAnim = "death_end";
    [Tooltip("Опционально. Если пусто, во время спавна лески анимация не меняется.")]
    [SerializeField] private string baseLeskaSpawnAnim = "";

    [Header("Animations - Ruka Layer")]
    [SerializeField] private string rukaLayerName = "Ruka";
    [SerializeField] private string rukaRamAnim = "Attack";
    [SerializeField] private string rukaCloseAnim = "Attack2";
    [SerializeField] private string rukaHitAnim = "hit";

    [Header("Animation Settings")]
    [SerializeField] private float animCrossFade = 0.12f;
    [SerializeField] private int baseLayerIndex = 0;
    [SerializeField] private int rukaLayerIndex = 1;

    // ─── Glazktuk / Zigzag Rush (low HP special) ─────────────────────────────
    [Header("Glazktuk - Zigzag Rush")]
    [Tooltip("Включить режим цепных зигзагообразных рывков при низком HP (фаза 1).")]
    public bool enableGlazktuk = true;
    [Tooltip("Порог HP (0..1) для активации Glazktuk (примерно 0.3-0.35).")]
    [Range(0f, 0.99f)] public float glazTriggerHealthPercent = 0.35f;
    [Tooltip("Минимум рывков в серии")]
    public int glazMinRams = 4;
    [Tooltip("Максимум рывков в серии")]
    public int glazMaxRams = 6;
    [Tooltip("Угол поворота между рывками (мин градусы)")]
    public float glazAngleMin = 45f;
    [Tooltip("Угол поворота между рывками (макс градусы)")]
    public float glazAngleMax = 90f;
    [Tooltip("Множитель увеличения скорости для каждого следующего рывка (например 0.12 = +12% каждый раз)")]
    public float glazSpeedIncreasePerDash = 0.12f;
    [Tooltip("Короткое прицеливание перед серией (сек). Обычно доли секунды")]
    public float glazInitialAim = 0.12f;
    [Tooltip("Длительность каждого рывка в серии (сек)")]
    public float glazDashDuration = 0.45f;
    [Tooltip("Пауза после серии (сек) — окно для контратаки игрока.")]
    public float glazPauseAfterSeries = 1.0f;
    [Tooltip("На середине рывка резкая коррекция курса на этот угол к игроку (мин)")]
    public float glazMidTurnMin = 20f;
    [Tooltip("На середине рывка резкая коррекция курса на этот угол к игроку (макс)")]
    public float glazMidTurnMax = 35f;
    // ─── Ссылки ───────────────────────────────────────────────────────────────

    [Header("References")]
    public Animator animator;
    public ParticleSystem hitEffect;
    public AudioSource audioSource;
    public AudioClip ramRoarClip;
    public AudioClip leskaSpawnClip;
    public AudioClip armorBreakClip;
    public AudioClip damageClip;
    public AudioClip deathClip;
    [SerializeField] private GameObject sceneTransitionTriggerObject;

    [Header("Gizmos")]
    public bool showGizmos = true;
    public bool showGizmoLabels = false;

    // ─── Внутреннее состояние ─────────────────────────────────────────────────

    private NavMeshAgent navAgent;
    private Transform player;
    private PlayerHealth playerHealth;

    private BossPhase currentPhase = BossPhase.Phase1;
    private bool phase2Entered;
    private bool isDead;
    private bool isSlow;
    private bool isStunSequence;
    private bool isDoingCloseAttack;

    private float nextRamTime;
    private float nextLeskaTime;
    private float nextRepathTime;
    private float nextCloseAttackTime;

    private bool isDoingRam;
    private bool isSpawningLeska;
    private bool isDoingShark;
    private bool abortRamChain;
    private bool glazPhaseConflictLogged;

    // Glazktuk state
    private bool isDoingGlazktuk;
    private int ramAttemptCounter = 0;

    private Coroutine slowRoutine;
    private Coroutine rageRoutine;

    private readonly Collider[] sharkHitsBuffer = new Collider[8];

    private float armorDamage;
    private bool isArmorBroken;
    private bool isVulnerable;
    private bool isEnraged;
    private bool armorBreakPending;

    [Header("Hit Rage")]
    [Tooltip("Множитель скорости при каждом попадании игрока.")]
    [Range(1f, 3f)] public float rageSpeedMultiplier = 1.35f;
    [Tooltip("Множитель ускорения при каждом попадании игрока.")]
    [Range(1f, 3f)] public float rageAccelerationMultiplier = 1.2f;
    [Tooltip("Длительность ярости после последнего попадания (сек).")]
    public float rageDuration = 2.5f;
    [Tooltip("Максимальный суммарный множитель скорости от ярости.")]
    [Range(1f, 5f)] public float rageMaxSpeedMultiplier = 2.5f;
    [Tooltip("Максимальный суммарный множитель ускорения от ярости.")]
    [Range(1f, 5f)] public float rageMaxAccelerationMultiplier = 2f;

    private float nextSharkTime;
    private Vector3 lastRamDir;
    private Vector3 lastChaseDestination;
    private bool hasChaseDestination;

    private int dodgeStreak;

    private const int playerPosHistorySize = 32;
    private Vector3[] playerPosHistory;
    private float[] playerPosTimeHistory;
    private int playerPosHistoryCount;
    private int playerPosHistoryIndex;

    private string currentBaseAnimState;
    private string currentRukaAnimState;

#region PlayerMemory
    private enum DodgeDirection
    {
        None,
        Left,
        Right
    }

    private enum MemoryAttackType
    {
        Ram,
        Glazktuk
    }

    private struct DodgeEvent
    {
        public DodgeDirection direction;
        public float distanceAtDodge;
        public MemoryAttackType attackType;
    }

    private struct AttackReactionEvent
    {
        public MemoryAttackType attackType;
        public bool reactedBeforeDash;
        public float reactionTime;
    }

    [Header("Player Memory")]
    [Tooltip("Максимум событий уклонения/реакции, которые храним в памяти босса.")]
    [Range(8, 64)] public int memoryCapacity = 24;
    [Tooltip("Порог перемещения игрока (м), считающийся реакцией на прицеливание/рывок.")]
    [Range(0.05f, 2f)] public float reactionMoveThreshold = 0.6f;
    [Tooltip("Сколько подряд уклонений в одну сторону нужно для уверенного тарана.")]
    [Range(3, 8)] public int confidentRamStreakThreshold = 3;
    [Tooltip("Боковое смещение цели для уверенного тарана (м).")]
    [Range(0.2f, 3f)] public float confidentRamSideOffset = 1.1f;
    [Tooltip("Доп. длительность прицеливания для ловушки на реакцию (сек).")]
    [Range(0.05f, 1.5f)] public float reactionTrapExtraAim = 0.45f;
    [Tooltip("Шанс применить ловушку на реакцию, если игрок рано уклоняется.")]
    [Range(0f, 1f)] public float reactionTrapChance = 0.65f;
    [Tooltip("Насколько далеко от игрока по стороне доминирующего уклонения спавнить PredictedLeska (м).")]
    [Range(0.3f, 4f)] public float predictedLeskaSideOffset = 1.6f;
    [Tooltip("Шанс подменить одну из обычных лесок на PredictedLeska.")]
    [Range(0f, 1f)] public float predictedLeskaChance = 0.8f;
    private DodgeEvent[] dodgeEvents;
    private int dodgeEventsCount;
    private int dodgeEventsCursor;

    private AttackReactionEvent[] reactionEvents;
    private int reactionEventsCount;
    private int reactionEventsCursor;

    private DodgeDirection dominantDodgeDirection = DodgeDirection.None;
    private float averageReactionTime = 0.25f;
    private bool playerDodgesEarly;
    private bool playerIsAggressive;
    private float preferredCombatDistance = 3f;

    private int sameSideDodgeStreak;
    private DodgeDirection lastDodgeDirection = DodgeDirection.None;
    private int vulnerableHitCounter;


    private DodgeDirection pendingRamDodgeDirection = DodgeDirection.None;
    private float pendingRamDodgeDistance;
    private MemoryAttackType pendingRamAttackType = MemoryAttackType.Ram;
#endregion

    // ─────────────────────────────────────────────────────────────────────────
    // Инициализация
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Поиск игрока по тегу
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
            playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            player.TryGetComponent(out playerHealth);
        }

        health = maxHealth;
        phase2Entered = false;
        armorDamage = 0f;
        isArmorBroken = false;
        isVulnerable = false;

        dodgeStreak = 0;

        playerPosHistory = new Vector3[playerPosHistorySize];
        playerPosTimeHistory = new float[playerPosHistorySize];
        playerPosHistoryCount = 0;
        playerPosHistoryIndex = 0;
        hasChaseDestination = false;

        int capacity = Mathf.Max(8, memoryCapacity);
        dodgeEvents = new DodgeEvent[capacity];
        reactionEvents = new AttackReactionEvent[capacity];
        dodgeEventsCount = 0;
        dodgeEventsCursor = 0;
        reactionEventsCount = 0;
        reactionEventsCursor = 0;
        sameSideDodgeStreak = 0;
        lastDodgeDirection = DodgeDirection.None;
        vulnerableHitCounter = 0;

        SetAgentSpeed(chaseSpeed);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            ResolveAnimatorLayers();
        }

        if (navAgent != null)
        {
            navAgent.autoBraking = false;
            navAgent.autoRepath = true;
            navAgent.acceleration = 999f;
            navAgent.angularSpeed = 720f;
            navAgent.stoppingDistance = 0.1f;
        }

        if (sceneTransitionTriggerObject != null)
            sceneTransitionTriggerObject.SetActive(false);
    }

    private void OnValidate()
    {
        phase2HealthThreshold = Mathf.Clamp(phase2HealthThreshold, 0f, 0.99f);
        glazTriggerHealthPercent = Mathf.Clamp(glazTriggerHealthPercent, 0f, 0.99f);

        if (!enableGlazktuk)
            return;

        if (phase2HealthThreshold >= glazTriggerHealthPercent)
        {
            Debug.LogWarning(
                $"[BossEnemy] Конфликт порогов: phase2HealthThreshold ({phase2HealthThreshold:0.00}) >= glazTriggerHealthPercent ({glazTriggerHealthPercent:0.00}). " +
                "Glazktuk будет запускаться только с fallback-логикой и может вести себя не так, как ожидается.",
                this);
        }
    }

    

    

    // ─────────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (isDead || player == null || navAgent == null || !navAgent.isOnNavMesh)
            return;

        if (armorBreakPending && !isStunSequence && !isDoingRam && !isDoingGlazktuk)
        {
            StartCoroutine(ArmorBreakRoutine());
            return;
        }

        RecordPlayerHistory();

        // Во время тарана, стана или спавна лесок — управление передано корутине
        if (isDoingRam || isDoingCloseAttack || isSpawningLeska || isStunSequence || isDoingShark)
            return;

        // Фаза 2: периодический спавн лесок
        if (currentPhase == BossPhase.Phase2)
            Phase2Update();

        Phase1Update();
       
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Логика фаз
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Проверяет порог HP и переключает фазу один раз.
    /// </summary>
    private void TryEnterPhase2()
    {
        if (phase2Entered)
            return;

        if (health <= maxHealth * phase2HealthThreshold)
        {
            currentPhase = BossPhase.Phase2;
            phase2Entered = true;
            Debug.Log($"[BossEnemy] Переход в фазу 2 (HP={health:0.#})");
        }
    }

    /// <summary>
    /// Фаза 1: преследование + таран.
    /// </summary>
    private void Phase1Update()
    {
        Vector3 toPlayer = player.position - transform.position;
        float distSqr = toPlayer.sqrMagnitude;

        // Ближняя атака (Attack2 на слое Ruka)
        bool didCloseAttack = TryCloseAttack(distSqr);
        if (didCloseAttack)
            return;

        // Попытка тарана
        float ramTriggerDistSqr = ramTriggerDistance * ramTriggerDistance;
        // Glazktuk (цепной зигзаг) при низком HP
        bool lowHealthForGlaz = health <= maxHealth * glazTriggerHealthPercent;
        bool glazThresholdConflict = phase2HealthThreshold > glazTriggerHealthPercent;
        bool glazPhaseAllowed = currentPhase == BossPhase.Phase1 || glazThresholdConflict;
        bool canGlazktuk = enableGlazktuk && lowHealthForGlaz && glazPhaseAllowed;

        if (enableGlazktuk && glazThresholdConflict && currentPhase == BossPhase.Phase2 && !glazPhaseConflictLogged)
        {
            glazPhaseConflictLogged = true;
            LogBossEvent("Glazktuk fallback", "phase2HealthThreshold выше glazTriggerHealthPercent; Glazktuk разрешен в Phase2, иначе не сработает");
        }

        if (!didCloseAttack && canGlazktuk && Time.time >= nextRamTime && distSqr <= ramTriggerDistSqr)
        {
            StartCoroutine(GlazktukRoutine());
            return;
        }

        if (!didCloseAttack && Time.time >= nextRamTime && distSqr <= ramTriggerDistSqr)
        {
            ramAttemptCounter++;
            StartCoroutine(RamRoutine());
            return;
        }

        // Обычное преследование
        ChasePlayer();
    }

    /// <summary>
    /// Фаза 2: добавляет периодический спавн лесок.
    /// </summary>
    private void Phase2Update()
    {
        if (Time.time >= nextSharkTime && ShouldDoSharkSpin())
        {
            StartCoroutine(SharkSpinRoutine());
            return;
        }

        if (Time.time >= nextLeskaTime && leskaPrefab != null)
        {
            StartCoroutine(LeskaSpawnRoutine());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Преследование
    // ─────────────────────────────────────────────────────────────────────────

    private void ChasePlayer()
    {
        Vector3 destination = player.position;
        if (ShouldRefreshPath(destination))
        {
            float chaseThreshold = Mathf.Max(0.01f, chaseRepathDistanceThreshold);
            float chaseThresholdSqr = chaseThreshold * chaseThreshold;
            bool shouldRepathNow = !hasChaseDestination
                                  || Time.time >= nextRepathTime
                                  || (destination - lastChaseDestination).sqrMagnitude >= chaseThresholdSqr
                                  || !navAgent.hasPath;

            if (shouldRepathNow)
            {
                nextRepathTime = Time.time + Mathf.Max(0.01f, chaseRepathInterval);
                navAgent.isStopped = false;
                navAgent.SetDestination(destination);
                lastChaseDestination = destination;
                hasChaseDestination = true;
            }
        }

        PlayBaseAnim(baseWalkAnim);
    }

    private bool TryCloseAttack(float distSqr)
    {
        if (isDoingRam || isDoingGlazktuk || isSpawningLeska || isStunSequence || isDoingShark)
            return false;

        if (closeAttackDistance <= 0f)
            return false;

        if (Time.time < nextCloseAttackTime)
            return false;

        float closeDistSqr = closeAttackDistance * closeAttackDistance;
        if (distSqr <= closeDistSqr)
        {
            StartCoroutine(CloseAttackRoutine());
            nextCloseAttackTime = Time.time + Mathf.Max(0f, closeAttackCooldown);
            return true;
        }

        float preferRange = closeAttackDistance * Mathf.Max(1f, closeAttackPreferRangeMultiplier);
        float preferRangeSqr = preferRange * preferRange;
        if (distSqr > preferRangeSqr)
            return false;

        float proximityT = Mathf.InverseLerp(preferRangeSqr, closeDistSqr, distSqr);
        float preferChance = Mathf.Lerp(closeAttackPreferChanceAtEdge, closeAttackPreferChanceNear, proximityT);
        if (Random.value > preferChance)
            return false;

        StartCoroutine(CloseAttackRoutine());
        nextCloseAttackTime = Time.time + Mathf.Max(0f, closeAttackCooldown);
        return true;
    }

    /// <summary>
    /// Ближняя атака без коллайдеров: стоп, разворот, прямой урон, возврат к преследованию.
    /// </summary>
    private IEnumerator CloseAttackRoutine()
    {
        isDoingCloseAttack = true;
        LogBossEvent("Начало CloseAttack", "Босс начал ближнюю атаку Attack2");

        StopAgentHard();
        FacePlayer(1440f);
        PlayRukaAnim(rukaCloseAnim);

        if (closeHitDelay > 0f)
            yield return new WaitForSeconds(closeHitDelay);

        float hitEnd = Time.time + Mathf.Max(0.01f, closeHitWindow);
        bool closeHitDealt = false;
        while (Time.time < hitEnd)
        {
            FacePlayer(540f);

            if (!closeHitDealt)
            {
                Vector3 calculatedHitOrigin = GetHitOrigin(closeHitForwardOffset, closeHitUpOffset);
                Vector3 playerBasedOrigin = player.position + Vector3.up * Mathf.Max(0f, playerHitUpOffset);
                Vector3 hitOrigin = Vector3.Lerp(calculatedHitOrigin, playerBasedOrigin, Mathf.Clamp01(closeAttackMagnetization));
                if (TryApplyDirectDamage(hitOrigin, closeAttackDistance, closeAttackDamage))
                {
                    closeHitDealt = true;
                    LogBossEvent("Попадание CloseAttack", "Attack2 попал по игроку");
                }
            }
            yield return null;
        }

        if (!closeHitDealt)
            LogBossEvent("Промах CloseAttack", "Attack2 не попал по игроку");

        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayBaseAnim(baseWalkAnim);

        isDoingCloseAttack = false;
        LogBossEvent("Конец CloseAttack", "Босс завершил ближнюю атаку Attack2");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Таран
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Корутина тарана:
    ///   1. Фаза прицеливания — босс тормозит, разворачивается к игроку.
    ///   2. Рывок — резкий разгон.
    ///   3. Проверка попадания.
    ///   4. Кулдаун.
    /// </summary>
    private IEnumerator RamRoutine()
    {
        isDoingRam = true;
        nextRamTime = Time.time + ramCooldown;

        yield return RamOnceRoutine(1f);

        if (abortRamChain)
        {
            abortRamChain = false;
            isDoingRam = false;
            yield break;
        }

        if (currentPhase == BossPhase.Phase1 && enableReverseRam)
            yield return ReverseRamOnceRoutine();

        if (ShouldChainCloseAfterRam())
        {
            if (currentPhase == BossPhase.Phase2)
                yield return Phase2RamBounceAndCloseAttackRoutine();
            else
                yield return StartCoroutine(CloseAttackRoutine());
        }

        isDoingRam = false;
    }

    private IEnumerator RamOnceRoutine(float aimScale)
    {
        bool hasEnoughMemory = dodgeEventsCount >= 3;
        bool isAdaptiveRush = enableAdaptiveRam && (hasEnoughMemory ? sameSideDodgeStreak >= Mathf.Max(3, confidentRamStreakThreshold) : dodgeStreak >= 2);
        bool useConfidentRam = currentPhase == BossPhase.Phase1 && sameSideDodgeStreak >= Mathf.Max(3, confidentRamStreakThreshold) && dominantDodgeDirection != DodgeDirection.None;
        bool useReactionTrap = currentPhase == BossPhase.Phase1 && playerDodgesEarly && Random.value < reactionTrapChance;

        // — Прицеливание —
        SetAgentSpeed(aimingSpeed);
        navAgent.isStopped = false;
        navAgent.SetDestination(player.position);
        PlayBaseAnim(baseWalkAnim);

        if (audioSource != null && ramRoarClip != null)
            audioSource.PlayOneShot(ramRoarClip);

        float aimDuration = Mathf.Clamp(Random.Range(ramAimDurationMin, ramAimDurationMax), 0.05f, 2f);
        if (aimDuration <= 0f)
            aimDuration = ramAimDuration;

        aimDuration *= Mathf.Clamp(aimScale, 0.2f, 2f);
        if (isAdaptiveRush)
            aimDuration = Mathf.Max(0.05f, ramAimDurationMin);
        if (useReactionTrap)
            aimDuration += Mathf.Max(0.05f, reactionTrapExtraAim);

        LogBossEvent("Начало прицеливания тарана", $"Длительность={aimDuration:0.00}с");

        float aimEnd = Time.time + aimDuration;
        float nextAimRepath = 0f;
        float aimStartTime = Time.time;
        Vector3 aimStartPlayerPos = player.position;
        bool reactedDuringAim = false;
        float firstReactionTime = -1f;
        while (Time.time < aimEnd)
        {
            if (!reactedDuringAim)
            {
                Vector3 delta = player.position - aimStartPlayerPos;
                delta.y = 0f;
                if (delta.sqrMagnitude >= reactionMoveThreshold * reactionMoveThreshold)
                {
                    reactedDuringAim = true;
                    firstReactionTime = Time.time - aimStartTime;
                }
            }

            FacePlayer(720f);

            if (Time.time >= nextAimRepath)
            {
                nextAimRepath = Time.time + 0.15f;
                Vector3 destination = player.position;
                if (ShouldRefreshPath(destination))
                {
                    navAgent.SetDestination(destination);
                    lastChaseDestination = destination;
                    hasChaseDestination = true;
                }
            }
            yield return null;
        }

        LogBossEvent("Конец прицеливания тарана", $"Фактическая длительность={Time.time - aimStartTime:0.00}с");

        // — Рывок —
        StopAgentHard();
        PlayRukaAnim(rukaRamAnim);
        Vector3 ramTarget = (useLaggedRamTarget && !useReactionTrap) ? GetLaggedPlayerPosition() : player.position;

        if (useConfidentRam)
        {
            Vector3 toTargetFlat = ramTarget - transform.position;
            toTargetFlat.y = 0f;
            Vector3 baseDir = toTargetFlat.sqrMagnitude > 0.0001f ? toTargetFlat.normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, baseDir);
            float sideSign = dominantDodgeDirection == DodgeDirection.Right ? 1f : -1f;
            ramTarget += right * sideSign * confidentRamSideOffset;
        }

        Vector3 ramDir = ramTarget - transform.position;
        ramDir.y = 0f;
        if (ramDir.sqrMagnitude < 0.0001f)
            ramDir = transform.forward;
        ramDir.Normalize();
        lastRamDir = ramDir;
        pendingRamAttackType = MemoryAttackType.Ram;

        float dashSpeed = Mathf.Clamp(Random.Range(ramSpeedMin, ramSpeedMax), 0.1f, 50f);
        if (dashSpeed <= 0f)
            dashSpeed = ramSpeed;

        LogBossEvent("Параметры рывка", $"Скорость={dashSpeed:0.00}, Направление={ramDir}");

        float ramEnd = Time.time + ramDuration;
        bool ramHitDealt = false;
        Vector3 lastHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
        bool doFeint = currentPhase == BossPhase.Phase2 && playerDodgesEarly && reactedDuringAim;
        float feintDelay = Mathf.Clamp(averageReactionTime, feintMinTime, feintMaxTime);
        float feintTime = Time.time + Mathf.Clamp(feintDelay, 0.05f, ramDuration);
        float dashStartTime = Time.time;
        Vector3 dashStartPlayerPos = player.position;
        bool reactedAfterDash = false;
        while (Time.time < ramEnd)
        {
            if (armorBreakPending)
            {
                StopAgentHard();
                yield return new WaitForSeconds(0.08f);
                if (!isDead && !isStunSequence)
                    StartCoroutine(ArmorBreakRoutine());
                yield break;
            }

            if (!reactedDuringAim && !reactedAfterDash)
            {
                Vector3 deltaAfterDash = player.position - dashStartPlayerPos;
                deltaAfterDash.y = 0f;
                if (deltaAfterDash.sqrMagnitude >= reactionMoveThreshold * reactionMoveThreshold)
                {
                    reactedAfterDash = true;
                    firstReactionTime = Time.time - dashStartTime;
                }
            }

            if (doFeint && Time.time >= feintTime)
            {
                abortRamChain = true;
                RecordReactionTime(MemoryAttackType.Ram, reactedDuringAim, Mathf.Max(0f, firstReactionTime));
                yield return FeintRoutine();
                yield break;
            }

            Vector3 currentHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
            if (!ramHitDealt && SegmentHitsPlayer(lastHitOrigin, currentHitOrigin, ramHitRadius))
            {
                if (TryApplyDirectDamage(currentHitOrigin, ramHitRadius, ramDamage))
                {
                    ramHitDealt = true;
                    LogBossEvent("Попадание тарана", "Рывок попал по игроку");
                }
            }

            navAgent.Move(ramDir * dashSpeed * Time.deltaTime);
            lastHitOrigin = currentHitOrigin;
            yield return null;
        }

        if (firstReactionTime < 0f)
            firstReactionTime = ramDuration;
        RecordReactionTime(MemoryAttackType.Ram, reactedDuringAim, Mathf.Max(0f, firstReactionTime));

        CaptureRamDodgeSample(ramDir);

        if (!ramHitDealt)
            LogBossEvent("Промах тарана", "Рывок не попал по игроку");

        RegisterRamOutcome(ramHitDealt);

        // — Возврат к преследованию —
        // Не восстанавливаем агента, если планируется CloseAttack
        if (!ShouldChainCloseAfterRam())
        {
            navAgent.isStopped = false;
            SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
            PlayBaseAnim(baseWalkAnim);
        }
    }

    private IEnumerator ReverseRamOnceRoutine()
    {
        Vector3 reverseDir = -lastRamDir;
        if (reverseDir.sqrMagnitude < 0.0001f)
            reverseDir = -transform.forward;
        reverseDir.y = 0f;
        reverseDir.Normalize();

        if (reverseRamTurnDuration > 0f)
        {
            float turnEnd = Time.time + reverseRamTurnDuration;
            Quaternion targetRot = Quaternion.LookRotation(reverseDir, Vector3.up);
            while (Time.time < turnEnd)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 1440f * Time.deltaTime);
                yield return null;
            }
        }

        StopAgentHard();
        PlayRukaAnim(rukaRamAnim);

        float dashSpeed = Mathf.Max(0.1f, ramSpeed * reverseRamSpeedScale);
        float dashDuration = Mathf.Max(0.05f, ramDuration * reverseRamDurationScale);
        float ramEnd = Time.time + dashDuration;
        bool ramHitDealt = false;
        pendingRamAttackType = MemoryAttackType.Ram;
        Vector3 lastHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
        while (Time.time < ramEnd)
        {
            if (armorBreakPending)
            {
                StopAgentHard();
                yield return new WaitForSeconds(0.08f);
                if (!isDead && !isStunSequence)
                    StartCoroutine(ArmorBreakRoutine());
                yield break;
            }

            Vector3 currentHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
            if (!ramHitDealt && SegmentHitsPlayer(lastHitOrigin, currentHitOrigin, ramHitRadius))
            {
                if (TryApplyDirectDamage(currentHitOrigin, ramHitRadius, ramDamage))
                {
                    ramHitDealt = true;
                    LogBossEvent("Попадание тарана", "Обратный рывок попал по игроку");
                }
            }

            navAgent.Move(reverseDir * dashSpeed * Time.deltaTime);
            lastHitOrigin = currentHitOrigin;
            yield return null;
        }

        CaptureRamDodgeSample(reverseDir);

        if (!ramHitDealt)
            LogBossEvent("Промах тарана", "Обратный рывок не попал по игроку");

        RegisterRamOutcome(ramHitDealt);
    }

    private IEnumerator Phase2RamBounceAndCloseAttackRoutine()
    {
        if (player == null || navAgent == null)
            yield break;

        StopAgentHard();

        Vector3 bounceDir = player.position - transform.position;
        bounceDir.y = 0f;
        if (bounceDir.sqrMagnitude < 0.0001f)
            bounceDir = transform.forward;
        bounceDir.Normalize();

        PlayBaseAnim(baseWalkAnim);

        float bounceDuration = Mathf.Clamp(closeHitDelay + 0.10f, 0.08f, 0.22f);
        float bounceDistance = Mathf.Clamp(closeAttackDistance * 0.45f, 0.35f, 0.85f);
        float bounceSpeed = bounceDistance / Mathf.Max(0.05f, bounceDuration);
        float bounceEnd = Time.time + bounceDuration;

        LogBossEvent("Отскок после тарана", $"Прыжок к игроку перед CloseAttack, дистанция={bounceDistance:0.00}, длительность={bounceDuration:0.00}с");

        while (Time.time < bounceEnd)
        {
            if (armorBreakPending)
            {
                StopAgentHard();
                yield return new WaitForSeconds(0.08f);
                if (!isDead && !isStunSequence)
                    StartCoroutine(ArmorBreakRoutine());
                yield break;
            }

            FacePlayer(1080f);
            navAgent.Move(bounceDir * bounceSpeed * Time.deltaTime);
            yield return null;
        }

        yield return StartCoroutine(CloseAttackRoutine());
    }

    /// <summary>
    /// Glazktuk: серия из нескольких рывков зигзагом (без возврата к преследованию между ними).
    /// Триггерится при низком HP и выполняется в фазе 1.
    /// </summary>
    private IEnumerator GlazktukRoutine()
    {
        if (isDoingGlazktuk || player == null || navAgent == null)
            yield break;

        isDoingGlazktuk = true;
        isDoingRam = true;
        nextRamTime = Time.time + glazPauseAfterSeries;

        try
        {
            // Короткое прицеливание перед серией
            SetAgentSpeed(aimingSpeed);
            navAgent.isStopped = false;
            PlayBaseAnim(baseWalkAnim);
            if (audioSource != null && ramRoarClip != null)
                audioSource.PlayOneShot(ramRoarClip);

            float aimDuration = Mathf.Max(0.01f, glazInitialAim);
            float aimEnd = Time.time + aimDuration;
            while (Time.time < aimEnd)
            {
                FacePlayer(720f);
                yield return null;
            }

            // Отрезаем влияние pathfinding перед серией ручных Move.
            StopAgentHard();
            PlayRukaAnim(rukaRamAnim);

            // Подготовка параметров серии
            int count = Random.Range(Mathf.Max(1, glazMinRams), Mathf.Max(glazMinRams, glazMaxRams) + 1);
            float initialSpeed = Mathf.Clamp(Random.Range(ramSpeedMin, ramSpeedMax), 0.1f, 50f);
            Vector3 lastDir = transform.forward;

            for (int i = 0; i < count; i++)
            {
                // Определяем направление: первый рывок — в сторону игрока, далее — от previous + поворот
                Vector3 ramTarget = useLaggedRamTarget ? GetLaggedPlayerPosition() : player.position;
                Vector3 ramDir = (ramTarget - transform.position);
                ramDir.y = 0f;
                if (ramDir.sqrMagnitude < 0.0001f)
                    ramDir = transform.forward;
                ramDir.Normalize();

                if (i > 0)
                {
                    float ang = Random.Range(glazAngleMin, glazAngleMax);
                    ang *= (Random.value < 0.5f) ? -1f : 1f;
                    ramDir = Quaternion.AngleAxis(ang, Vector3.up) * lastDir;
                    ramDir.y = 0f;
                    ramDir.Normalize();
                }

                lastDir = ramDir;

                // Скорость растёт каждый шаг
                float dashSpeed = initialSpeed * (1f + glazSpeedIncreasePerDash * i);
                LogBossEvent("Glazktuk рывок", $"Итерация {i + 1}/{count}, скорость={dashSpeed:0.00}");
                float dashStart = Time.time;
                float dashEnd = dashStart + Mathf.Max(0.05f, glazDashDuration);
                bool ramHit = false;
                Vector3 lastHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
                bool midTurnDone = false;

                while (Time.time < dashEnd)
                {
                    if (armorBreakPending)
                    {
                        StopAgentHard();
                        yield return new WaitForSeconds(0.08f);
                        if (!isDead && !isStunSequence)
                            StartCoroutine(ArmorBreakRoutine());
                        yield break;
                    }

                    // В середине рывка делаем резкую коррекцию курса в сторону игрока
                    if (!midTurnDone && Time.time >= dashStart + (dashEnd - dashStart) * 0.5f)
                    {
                        midTurnDone = true;
                        Vector3 toPlayer = player.position - transform.position;
                        toPlayer.y = 0f;
                        if (toPlayer.sqrMagnitude > 0.0001f)
                        {
                            float angleToPlayer = Vector3.SignedAngle(ramDir, toPlayer.normalized, Vector3.up);
                            float correction = Mathf.Clamp(angleToPlayer, -Mathf.Max(glazMidTurnMin, 0f), Mathf.Max(glazMidTurnMax, 0f));
                            ramDir = Quaternion.AngleAxis(correction, Vector3.up) * ramDir;
                            ramDir.Normalize();
                        }
                    }

                    Vector3 currentHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
                    if (!ramHit && SegmentHitsPlayer(lastHitOrigin, currentHitOrigin, ramHitRadius))
                    {
                        if (TryApplyDirectDamage(currentHitOrigin, ramHitRadius, ramDamage))
                        {
                            ramHit = true;
                            LogBossEvent("Попадание тарана", $"Glazktuk рывок {i + 1}/{count} попал по игроку");
                        }
                    }

                    navAgent.Move(ramDir * dashSpeed * Time.deltaTime);
                    lastHitOrigin = currentHitOrigin;
                    yield return null;
                }

                pendingRamAttackType = MemoryAttackType.Glazktuk;
                CaptureRamDodgeSample(ramDir);

                if (!ramHit)
                    LogBossEvent("Промах тарана", $"Glazktuk рывок {i + 1}/{count} не попал по игроку");

                RegisterRamOutcome(ramHit);
                // Небольшая пауза между рывками — минимальная, чтобы не возвращаться к преследованию
                yield return null;
            }

            // Завершаем серию: даём окно игроку
            if (glazPauseAfterSeries > 0f)
                yield return new WaitForSeconds(glazPauseAfterSeries);
        }
        finally
        {
            isDoingGlazktuk = false;
            isDoingRam = false;

            if (!isDead && !isStunSequence && navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
                PlayBaseAnim(baseWalkAnim);
            }
        }
    }


    // ─────────────────────────────────────────────────────────────────────────
    // Лески (фаза 2)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Корутина спавна лесок:
    ///   1. Босс останавливается, проигрывает анимацию.
    ///   2. Спавнит леску перед собой и за собой.
    ///   3. Продолжает преследование.
    /// </summary>
    private IEnumerator LeskaSpawnRoutine()
    {
        isSpawningLeska = true;
        nextLeskaTime = Time.time + leskaCooldown;

        StopAgentHard();
        PlayBaseAnim(baseLeskaSpawnAnim);

        if (audioSource != null && leskaSpawnClip != null)
            audioSource.PlayOneShot(leskaSpawnClip);

        yield return new WaitForSeconds(leskaSpawnAnimDuration);

        bool spawnedPredicted = false;
        if (currentPhase == BossPhase.Phase2 && dominantDodgeDirection != DodgeDirection.None && Random.value < predictedLeskaChance)
        {
            SpawnPredictedLeska();
            spawnedPredicted = true;
        }

        SpawnLeska(transform.TransformPoint(leskaSpawnOffsetFront));
        if (!spawnedPredicted)
            SpawnLeska(transform.TransformPoint(leskaSpawnOffsetBack));

        navAgent.isStopped = false;
        PlayBaseAnim(baseWalkAnim);
        isSpawningLeska = false;
    }

    /// <summary>
    /// Создаёт один объект-леску в заданной точке.
    /// </summary>
    private void SpawnLeska(Vector3 worldPos)
    {
        if (leskaPrefab == null) return;

        // Прижать к NavMesh
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            worldPos = hit.position;

        BossLeskaObject leska = Instantiate(leskaPrefab, worldPos, Quaternion.identity);
        leska.Init(leskaDamage, leskaLifetime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Урон и смерть
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Публичный метод получения урона. Вызывается снаружи (оружие игрока, и т.д.).
    /// </summary>
    public void TakeDamage(float incomingDamage)
    {
        if (isDead || incomingDamage <= 0f) return;

        if (hitEffect != null)
            hitEffect.Play();

        // Реакция на урон — hit на слое Ruka (только если не таран/стан)
        if (!isDoingRam && !isStunSequence)
            PlayRukaAnim(rukaHitAnim);

        if (isVulnerable)
        {
            vulnerableHitCounter++;
            playerIsAggressive = true;
            RecalculatePlayerAnalytics();
            ApplyHealthDamage(incomingDamage);
        }
        else
        {
            ApplyArmorDamage(incomingDamage);
        }

        // Замедление при уроне — только в фазе 2
        if (currentPhase == BossPhase.Phase2)
            TryApplySlow();

        TryApplyHitRage();
    }

    /// <summary>
    /// Применяет временное замедление только если игрок ведёт себя агрессивно (фаза 2).
    /// </summary>
    private void TryApplySlow()
    {
        if (!playerIsAggressive)
            return;

        if (slowRoutine != null)
            StopCoroutine(slowRoutine);

        slowRoutine = StartCoroutine(SlowRoutine());
    }

    private void TryApplyHitRage()
    {
        if (rageRoutine != null)
            StopCoroutine(rageRoutine);

        rageRoutine = StartCoroutine(RageRoutine());
    }

    private IEnumerator RageRoutine()
    {
        isEnraged = true;

        float endTime = Time.time + Mathf.Max(0.1f, rageDuration);
        while (Time.time < endTime)
        {
            yield return null;
        }

        isEnraged = false;
        rageRoutine = null;
    }

    private IEnumerator SlowRoutine()
    {
        isSlow = true;
        SetAgentSpeed(chaseSpeed * slowSpeedMultiplier);

        yield return new WaitForSeconds(slowDuration);

        isSlow = false;
        if (!isDoingRam)
            SetAgentSpeed(chaseSpeed);

        slowRoutine = null;
    }

    private IEnumerator ArmorBreakRoutine()
    {
        armorBreakPending = false;
        isDoingRam = false;
        isDoingGlazktuk = false;
        isStunSequence = true;
        StopAgentHard();
        isArmorBroken = true;
        isVulnerable = false;

        if (audioSource != null && armorBreakClip != null)
            audioSource.PlayOneShot(armorBreakClip);

        float originalCrossFade = animCrossFade;
        animCrossFade = Mathf.Max(originalCrossFade, 0.22f);
        LogBossEvent("Начало GetDown", $"t={Time.time:0.000}");
        PlayBaseAnim(baseGetDownAnim);
        animCrossFade = originalCrossFade;
        if (getDownDuration > 0f)
            yield return new WaitForSeconds(getDownDuration);

        string tryaskaAnim = (Random.value < 0.5f) ? baseTryaska1Anim : baseTryaska2Anim;
        LogBossEvent("Начало Tryaska", $"Анимация={tryaskaAnim}, t={Time.time:0.000}");
        PlayBaseAnim(tryaskaAnim);
        isVulnerable = true;
        if (tryaskaDuration > 0f)
            yield return new WaitForSeconds(tryaskaDuration);

        isVulnerable = false;
        if (isDead)
            yield break;

        LogBossEvent("Начало wakeUp", $"t={Time.time:0.000}");
        PlayBaseAnim(baseWakeUpAnim);
        if (wakeUpDuration > 0f)
            yield return new WaitForSeconds(wakeUpDuration);

        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayBaseAnim(baseWalkAnim);
        isStunSequence = false;

        if (armorRegenDelay > 0f)
            yield return new WaitForSeconds(armorRegenDelay);

        armorDamage = 0f;
        isArmorBroken = false;
    }

    private void ApplyArmorDamage(float incomingDamage)
    {
        if (armorBreakThreshold <= 0f)
        {
            ApplyHealthDamage(incomingDamage);
            return;
        }

        if (isArmorBroken || isStunSequence)
            return;

        armorDamage += incomingDamage;
        if (armorDamage >= armorBreakThreshold)
        {
            armorDamage = 0f;
            armorBreakPending = true;
            LogBossEvent("Выставлен armorBreakPending", $"Прервана атака: {GetCurrentAttackName()}");
        }
    }

    private void ApplyHealthDamage(float incomingDamage)
    {
        if (incomingDamage <= 0f)
            return;

        health -= incomingDamage;
        health = Mathf.Max(0f, health);

        if (audioSource != null && damageClip != null)
            audioSource.PlayOneShot(damageClip);

        TryEnterPhase2();

        if (health <= 0f)
            Die();
    }

    private void Die()
    {
        isDead = true;
        isStunSequence = false;

        dodgeStreak = 0;

        StopAllCoroutines();
        StopAgentHard();
        PlayBaseAnim(baseDeathAnim);

        if (audioSource != null && deathClip != null)
            audioSource.PlayOneShot(deathClip);

        if (sceneTransitionTriggerObject != null)
        {
            sceneTransitionTriggerObject.SetActive(true);

            SceneTransitionTrigger transitionTrigger = sceneTransitionTriggerObject.GetComponent<SceneTransitionTrigger>();
            if (transitionTrigger != null)
                transitionTrigger.SetBossDefeated(true);
        }

        LogBossEvent("Смерть босса", "Босс умер");
        // Здесь можно добавить: выдать лут, отыграть музыку победы, и т.д.
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Вспомогательные методы
    // ─────────────────────────────────────────────────────────────────────────

    private void SetAgentSpeed(float speed)
    {
        if (navAgent != null)
            navAgent.speed = Mathf.Max(0f, speed);
    }

    private void ResolveAnimatorLayers()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(baseLayerName))
        {
            int baseIndex = animator.GetLayerIndex(baseLayerName);
            if (baseIndex >= 0)
                baseLayerIndex = baseIndex;
        }

        if (!string.IsNullOrEmpty(rukaLayerName))
        {
            int rukaIndex = animator.GetLayerIndex(rukaLayerName);
            if (rukaIndex >= 0)
                rukaLayerIndex = rukaIndex;
        }

        if (rukaLayerIndex >= 0 && rukaLayerIndex < animator.layerCount)
            animator.SetLayerWeight(rukaLayerIndex, 1f);
    }

    private void StopAgentHard()
    {
        if (navAgent == null || !navAgent.isOnNavMesh) return;
        navAgent.isStopped = true;
        if (navAgent.hasPath) navAgent.ResetPath();
        navAgent.velocity = Vector3.zero;
        navAgent.nextPosition = transform.position;
    }

    private void FacePlayer(float degreesPerSecond)
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.deltaTime);
    }

    private bool TryApplyDirectDamage(Vector3 sourcePosition, float radius, int damage)
    {
        if (damage <= 0 || player == null || playerHealth == null || playerHealth.IsDead)
            return false;

        float hitRadius = Mathf.Max(0.1f, radius);
        Vector3 targetPos = player.position + Vector3.up * Mathf.Max(0f, playerHitUpOffset);
        float distSqr = (targetPos - sourcePosition).sqrMagnitude;
        if (distSqr > hitRadius * hitRadius)
            return false;

        playerHealth.ApplyDamage(damage);
        return true;
    }

    private Vector3 GetHitOrigin(float forwardOffset, float upOffset)
    {
        return transform.position + transform.forward * forwardOffset + Vector3.up * upOffset;
    }

    private bool SegmentHitsPlayer(Vector3 start, Vector3 end, float radius)
    {
        if (player == null)
            return false;

        Vector3 targetPos = player.position + Vector3.up * Mathf.Max(0f, playerHitUpOffset);
        Vector3 ab = end - start;
        float abSqr = ab.sqrMagnitude;
        float t = 0f;
        if (abSqr > 0.0001f)
            t = Mathf.Clamp01(Vector3.Dot(targetPos - start, ab) / abSqr);

        Vector3 closest = start + ab * t;
        float distSqr = (targetPos - closest).sqrMagnitude;
        return distSqr <= radius * radius;
    }

    private bool ShouldDoSharkSpin()
    {
        if (player == null)
            return false;

        Vector3 toPlayer = player.position - transform.position;
        float distSqr = toPlayer.sqrMagnitude;
        if (distSqr > sharkTriggerDistance * sharkTriggerDistance)
            return false;

        Vector3 flatToPlayer = toPlayer;
        flatToPlayer.y = 0f;
        if (flatToPlayer.sqrMagnitude < 0.0001f)
            return false;

        float angle = Vector3.Angle(transform.forward, flatToPlayer.normalized);
        return angle >= sharkBehindAngle;
    }

    private IEnumerator SharkSpinRoutine()
    {
        isDoingShark = true;
        nextSharkTime = Time.time + sharkCooldown;

        StopAgentHard();

        float endTime = Time.time + Mathf.Max(0.05f, sharkSpinDuration);
        while (Time.time < endTime)
        {
            transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
            yield return null;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, sharkRadius, sharkHitsBuffer, sharkHitMask);
        for (int i = 0; i < hitCount; i++)
        {
            if (sharkHitsBuffer[i] != null && sharkHitsBuffer[i].TryGetComponent(out PlayerHealth hitHealth))
            {
                if (!hitHealth.IsDead)
                    hitHealth.ApplyDamage(sharkDamage);
                break;
            }
        }

        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayBaseAnim(baseWalkAnim);
        isDoingShark = false;
    }

    private IEnumerator FeintRoutine()
    {
        StopAgentHard();

        float side = (Random.value < 0.5f) ? -1f : 1f;
        Vector3 sidestepDir = transform.right * side;
        float endTime = Time.time + Mathf.Max(0.05f, feintSidestepDuration);
        while (Time.time < endTime)
        {
            navAgent.Move(sidestepDir * (feintSidestepDistance / Mathf.Max(0.05f, feintSidestepDuration)) * Time.deltaTime);
            yield return null;
        }

        yield return StartCoroutine(CloseAttackRoutine());
    }

    private void CaptureRamDodgeSample(Vector3 ramDir)
    {
        if (player == null)
        {
            pendingRamDodgeDirection = DodgeDirection.None;
            pendingRamDodgeDistance = 0f;
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        pendingRamDodgeDistance = toPlayer.magnitude;

        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            pendingRamDodgeDirection = DodgeDirection.None;
            return;
        }

        Vector3 right = Vector3.Cross(Vector3.up, ramDir.normalized);
        float side = Vector3.Dot(toPlayer.normalized, right);
        if (side > 0.15f)
            pendingRamDodgeDirection = DodgeDirection.Right;
        else if (side < -0.15f)
            pendingRamDodgeDirection = DodgeDirection.Left;
        else
            pendingRamDodgeDirection = DodgeDirection.None;
    }

    private void RecordDodgeDirection(DodgeDirection direction, float distance, MemoryAttackType attackType)
    {
        if (dodgeEvents == null || dodgeEvents.Length == 0)
            return;

        dodgeEvents[dodgeEventsCursor] = new DodgeEvent
        {
            direction = direction,
            distanceAtDodge = Mathf.Max(0f, distance),
            attackType = attackType
        };

        dodgeEventsCursor = (dodgeEventsCursor + 1) % dodgeEvents.Length;
        dodgeEventsCount = Mathf.Min(dodgeEventsCount + 1, dodgeEvents.Length);

        if (direction != DodgeDirection.None)
        {
            if (direction == lastDodgeDirection)
                sameSideDodgeStreak++;
            else
                sameSideDodgeStreak = 1;

            lastDodgeDirection = direction;
        }

        RecalculatePlayerAnalytics();
    }

    private void RecordReactionTime(MemoryAttackType attackType, bool reactedBeforeDash, float reactionTime)
    {
        if (reactionEvents == null || reactionEvents.Length == 0)
            return;

        reactionEvents[reactionEventsCursor] = new AttackReactionEvent
        {
            attackType = attackType,
            reactedBeforeDash = reactedBeforeDash,
            reactionTime = Mathf.Max(0f, reactionTime)
        };

        reactionEventsCursor = (reactionEventsCursor + 1) % reactionEvents.Length;
        reactionEventsCount = Mathf.Min(reactionEventsCount + 1, reactionEvents.Length);

        RecalculatePlayerAnalytics();
    }

    private void RecalculatePlayerAnalytics()
    {
        int left = 0;
        int right = 0;
        float distSum = 0f;
        int distCount = 0;
        for (int i = 0; i < dodgeEventsCount; i++)
        {
            DodgeEvent e = dodgeEvents[i];
            if (e.direction == DodgeDirection.Left) left++;
            if (e.direction == DodgeDirection.Right) right++;
            if (e.distanceAtDodge > 0f)
            {
                distSum += e.distanceAtDodge;
                distCount++;
            }
        }

        if (right > left)
            dominantDodgeDirection = DodgeDirection.Right;
        else if (left > right)
            dominantDodgeDirection = DodgeDirection.Left;
        else
            dominantDodgeDirection = DodgeDirection.None;

        float reactSum = 0f;
        int reactCount = 0;
        int earlyCount = 0;
        for (int i = 0; i < reactionEventsCount; i++)
        {
            AttackReactionEvent e = reactionEvents[i];
            reactSum += e.reactionTime;
            reactCount++;
            if (e.reactedBeforeDash)
                earlyCount++;
        }

        if (reactCount > 0)
        {
            averageReactionTime = reactSum / reactCount;
            playerDodgesEarly = earlyCount >= Mathf.CeilToInt(reactCount * 0.6f);
        }

        if (distCount > 0)
            preferredCombatDistance = distSum / distCount;

        playerIsAggressive = vulnerableHitCounter > 0;
    }

    private void SpawnPredictedLeska()
    {
        if (player == null)
            return;

        float sideSign = dominantDodgeDirection == DodgeDirection.Right ? 1f : -1f;
        Vector3 predictedPos = player.position + transform.right * sideSign * predictedLeskaSideOffset;
        SpawnLeska(predictedPos);
    }

    private void PlayBaseAnim(string stateName)
    {
        if (!string.IsNullOrEmpty(stateName) && stateName != baseWalkAnim)
            SetRukaLayerWeight(0f);

        PlayAnimOnLayer(stateName, baseLayerIndex, ref currentBaseAnimState);
    }

    private void PlayRukaAnim(string stateName)
    {
        SetRukaLayerWeight(1f);
        PlayAnimOnLayer(stateName, rukaLayerIndex, ref currentRukaAnimState);
    }

    private void SetRukaLayerWeight(float weight)
    {
        if (animator == null)
            return;

        if (rukaLayerIndex < 0 || rukaLayerIndex >= animator.layerCount)
            return;

        animator.SetLayerWeight(rukaLayerIndex, Mathf.Clamp01(weight));
    }

    private void PlayAnimOnLayer(string stateName, int layer, ref string currentState)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        if (layer < 0 || layer >= animator.layerCount)
            return;

        if (stateName == currentState)
            return;

        if (!animator.HasState(layer, Animator.StringToHash(stateName)))
            return;

        animator.CrossFadeInFixedTime(stateName, animCrossFade, layer);
        currentState = stateName;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Gizmos (отладка в редакторе)
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        // Зона тарана
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, ramTriggerDistance);

        // Хитбокс тарана
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, ramHitRadius);

        // Ближняя атака
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, closeAttackDistance);

        // Точки спавна лесок
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.TransformPoint(leskaSpawnOffsetFront), 0.25f);
        Gizmos.DrawWireSphere(transform.TransformPoint(leskaSpawnOffsetBack), 0.25f);

        // Точки хитбокса
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireSphere(GetHitOrigin(closeHitForwardOffset, closeHitUpOffset), closeAttackDistance);
        Gizmos.DrawWireSphere(GetHitOrigin(ramHitForwardOffset, ramHitUpOffset), ramHitRadius);

        // Шарк
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, sharkRadius);

        // Порог перехода в фазу 2 (текст в Scene view)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

    #if UNITY_EDITOR
        if (showGizmoLabels)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, "Boss Gizmos");
            UnityEditor.Handles.Label(GetHitOrigin(closeHitForwardOffset, closeHitUpOffset), "Close Hit");
            UnityEditor.Handles.Label(GetHitOrigin(ramHitForwardOffset, ramHitUpOffset), "Ram Hit");
            UnityEditor.Handles.Label(transform.TransformPoint(leskaSpawnOffsetFront), "Leska Front");
            UnityEditor.Handles.Label(transform.TransformPoint(leskaSpawnOffsetBack), "Leska Back");
        }
    #endif
    }

    private void RecordPlayerHistory()
    {
        if (player == null)
            return;

        playerPosHistory[playerPosHistoryIndex] = player.position;
        playerPosTimeHistory[playerPosHistoryIndex] = Time.time;
        playerPosHistoryIndex = (playerPosHistoryIndex + 1) % playerPosHistorySize;
        if (playerPosHistoryCount < playerPosHistorySize)
            playerPosHistoryCount++;
    }

    private bool ShouldRefreshPath(Vector3 destination)
    {
        if (navAgent == null)
            return false;

        if (!hasChaseDestination)
            return true;

        if ((destination - lastChaseDestination).sqrMagnitude >= 0.5f)
            return true;

        if (!navAgent.hasPath)
            return true;

        return navAgent.pathStatus != NavMeshPathStatus.PathComplete;
    }

    private Vector3 GetLaggedPlayerPosition()
    {
        if (playerPosHistoryCount == 0 || player == null)
            return player != null ? player.position : transform.position;

        float lag = Mathf.Clamp(Random.Range(ramTargetLagMin, ramTargetLagMax), 0.05f, 2f);
        float targetTime = Time.time - lag;

        Vector3 olderPos = player.position;
        float olderTime = -1f;
        Vector3 newerPos = player.position;
        float newerTime = float.PositiveInfinity;

        for (int i = 0; i < playerPosHistoryCount; i++)
        {
            int idx = (playerPosHistoryIndex - 1 - i + playerPosHistorySize) % playerPosHistorySize;
            float t = playerPosTimeHistory[idx];
            Vector3 p = playerPosHistory[idx];

            if (t <= targetTime && t > olderTime)
            {
                olderTime = t;
                olderPos = p;
            }

            if (t >= targetTime && t < newerTime)
            {
                newerTime = t;
                newerPos = p;
            }
        }

        if (olderTime < 0f)
            return newerTime < float.PositiveInfinity ? newerPos : player.position;

        if (newerTime == float.PositiveInfinity || Mathf.Approximately(olderTime, newerTime))
            return olderPos;

        float lerpT = Mathf.InverseLerp(olderTime, newerTime, targetTime);
        return Vector3.Lerp(olderPos, newerPos, lerpT);
    }

    private void RegisterRamOutcome(bool hitDealt)
    {
        RecordDodgeDirection(pendingRamDodgeDirection, pendingRamDodgeDistance, pendingRamAttackType);

        bool hasEnoughMemory = dodgeEventsCount >= 3;

        if (hitDealt)
        {
            if (!hasEnoughMemory)
                dodgeStreak = 0;
            return;
        }

        if (!hasEnoughMemory)
            dodgeStreak++;
    }

    private bool ShouldChainCloseAfterRam()
    {
        if (player == null)
            return false;

        float preferDistanceLimit = closeAttackDistance * 1.25f;
        if (preferredCombatDistance > preferDistanceLimit)
            return false;

        if (Time.time < nextCloseAttackTime)
            return false;

        float chainDistance = closeAttackDistance * 1.0f;
        float distSqr = (player.position - transform.position).sqrMagnitude;
        if (distSqr > chainDistance * chainDistance)
            return false;

        nextCloseAttackTime = Time.time + Mathf.Max(0f, closeAttackCooldown);
        return true;
    }

    private void LogBossEvent(string eventName, string details)
    {
        Debug.Log($"[BossEnemy] {eventName}: {details}");
    }

    private string GetCurrentAttackName()
    {
        if (isDoingGlazktuk)
            return "Glazktuk";
        if (isDoingRam)
            return pendingRamAttackType == MemoryAttackType.Glazktuk ? "Glazktuk" : "Таран";
        if (isDoingCloseAttack)
            return "CloseAttack";
        if (isDoingShark)
            return "SharkSpin";
        if (isSpawningLeska)
            return "LeskaSpawn";

        return "Неизвестно";
    }
}
