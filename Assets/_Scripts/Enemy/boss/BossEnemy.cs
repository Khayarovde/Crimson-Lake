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
///   — При получении урона: с вероятностью slowOnHitChance — теряет скорость.
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

    [Header("Finisher")]
    [SerializeField] private bool canBeFinished;
    [SerializeField] private bool autoFinishableOnLowHealth = true;
    [SerializeField, Range(0f, 1f)] private float finishableHealthPercent = 0.2f;
    [SerializeField] private bool requirePhase2ForFinisher = true;

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
    [Tooltip("Угол финта во время прицеливания (градусы).")]
    public float ramFeintAngle = 12f;
    [Tooltip("Интервал смены финта во время прицеливания (сек).")]
    public float ramFeintSwitchInterval = 0.18f;
    [Tooltip("Дистанция, на которой запускается двойной таран (если игрок слишком далеко).")]
    public float doubleRamTriggerDistance = 9f;
    [Tooltip("Пауза между первым и вторым рывком двойного тарана.")]
    public float doubleRamGap = 0.25f;
    [Tooltip("Множитель времени прицеливания для второго рывка двойного тарана.")]
    public float doubleRamSecondAimScale = 0.6f;

    [Header("Adaptive Ram (Anti-Dodge)")]
    public bool enableAdaptiveRam = true;
    [Tooltip("Минимальный порог подряд удачных уклонений, после которого таран ускоряется.")]
    [Range(1, 10)] public int adaptiveDodgeThresholdMin = 2;
    [Tooltip("Максимальный порог подряд удачных уклонений, после которого таран ускоряется.")]
    [Range(1, 10)] public int adaptiveDodgeThresholdMax = 3;

    [Header("Ram Target Lag")]
    public bool useLaggedRamTarget = true;
    [Tooltip("Минимальная задержка позиции игрока для тарана (сек).")]
    public float ramTargetLagMin = 0.3f;
    [Tooltip("Максимальная задержка позиции игрока для тарана (сек).")]
    public float ramTargetLagMax = 0.4f;

    [Header("Close Attack (Ruka Attack2)")]
    [Tooltip("Если игрок слишком близко, проигрывается Attack2 на слое Ruka.")]
    public float closeAttackDistance = 1.6f;
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
    [Tooltip("Шанс ложного тарана (0..1).")]
    [Range(0f, 1f)] public float feintChance = 0.35f;
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
    [Tooltip("Вероятность замедления при получении урона (0..1).")]
    [Range(0f, 1f)] public float slowOnHitChance = 0.55f;
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

    [Header("NavMesh Tuning")]
    public float navAcceleration = 8f;
    public float navAngularSpeed = 200f;
    public float navStoppingDistance = 0.5f;
    public float pathRecalcInterval = 0.2f;

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

    private Coroutine slowRoutine;

    private readonly Collider[] sharkHitsBuffer = new Collider[8];

    private float armorDamage;
    private bool isArmorBroken;
    private bool isVulnerable;

    private float nextSharkTime;
    private Vector3 lastRamDir;

    private int dodgeStreak;
    private int adaptiveDodgeThreshold;

    private const int playerPosHistorySize = 32;
    private Vector3[] playerPosHistory;
    private float[] playerPosTimeHistory;
    private int playerPosHistoryCount;
    private int playerPosHistoryIndex;

    private string currentBaseAnimState;
    private string currentRukaAnimState;

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
        adaptiveDodgeThreshold = GetAdaptiveDodgeThreshold();

        playerPosHistory = new Vector3[playerPosHistorySize];
        playerPosTimeHistory = new float[playerPosHistorySize];
        playerPosHistoryCount = 0;
        playerPosHistoryIndex = 0;

        ApplyNavTuning();
        SetAgentSpeed(chaseSpeed);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            ResolveAnimatorLayers();
        }

        if (sceneTransitionTriggerObject != null)
            sceneTransitionTriggerObject.SetActive(false);
    }

    public bool CanBeFinished()
    {
        if (isDead)
            return false;

        if (requirePhase2ForFinisher && currentPhase != BossPhase.Phase2)
            return false;

        if (autoFinishableOnLowHealth && health <= maxHealth * finishableHealthPercent)
            return true;

        return canBeFinished;
    }

    public void SetFinishable(bool value)
    {
        canBeFinished = value;
    }

    public void KillDuringStun()
    {
        if (!CanBeFinished())
            return;

        Die();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (isDead || player == null || navAgent == null || !navAgent.isOnNavMesh)
            return;

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
        float doubleRamDistSqr = doubleRamTriggerDistance * doubleRamTriggerDistance;
        bool canDoubleRam = currentPhase == BossPhase.Phase1 && health <= maxHealth * 0.7f;
        if (!didCloseAttack && canDoubleRam && Time.time >= nextRamTime && distSqr >= doubleRamDistSqr)
        {
            StartCoroutine(DoubleRamRoutine());
            return;
        }

        if (!didCloseAttack && Time.time >= nextRamTime && distSqr <= ramTriggerDistSqr)
        {
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
        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + pathRecalcInterval;
            navAgent.isStopped = false;
            navAgent.SetDestination(player.position);
        }

        PlayBaseAnim(baseWalkAnim);
    }

    private bool TryCloseAttack(float distSqr)
    {
        if (closeAttackDistance <= 0f)
            return false;

        if (Time.time < nextCloseAttackTime)
            return false;

        float closeDistSqr = closeAttackDistance * closeAttackDistance;
        if (distSqr > closeDistSqr)
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

        StopAgentHard();
        FacePlayer(1440f);
        PlayRukaAnim(rukaCloseAnim);

        if (closeHitDelay > 0f)
            yield return new WaitForSeconds(closeHitDelay);

        float hitEnd = Time.time + Mathf.Max(0.01f, closeHitWindow);
        bool closeHitDealt = false;
        while (Time.time < hitEnd)
        {
            if (!closeHitDealt)
            {
                Vector3 hitOrigin = GetHitOrigin(closeHitForwardOffset, closeHitUpOffset);
                if (TryApplyDirectDamage(hitOrigin, closeAttackDistance, closeAttackDamage))
                    closeHitDealt = true;
            }
            yield return null;
        }

        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayBaseAnim(baseWalkAnim);

        isDoingCloseAttack = false;
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

        isDoingRam = false;
    }

    private IEnumerator DoubleRamRoutine()
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

        if (doubleRamGap > 0f)
            yield return new WaitForSeconds(doubleRamGap);

        yield return RamOnceRoutine(Mathf.Clamp(doubleRamSecondAimScale, 0.2f, 1f));

        isDoingRam = false;
    }

    private IEnumerator RamOnceRoutine(float aimScale)
    {
        bool isAdaptiveRush = enableAdaptiveRam && dodgeStreak >= adaptiveDodgeThreshold;

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

        float aimEnd = Time.time + aimDuration;
        float nextAimRepath = 0f;
        float feintSign = 1f;
        float nextFeintSwitch = Time.time + ramFeintSwitchInterval;
        while (Time.time < aimEnd)
        {
            if (ramFeintAngle > 0.01f)
            {
                if (Time.time >= nextFeintSwitch)
                {
                    feintSign *= -1f;
                    nextFeintSwitch = Time.time + Mathf.Max(0.05f, ramFeintSwitchInterval);
                }

                Vector3 feintDir = player.position - transform.position;
                feintDir.y = 0f;
                if (feintDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion feintRot = Quaternion.AngleAxis(ramFeintAngle * feintSign, Vector3.up);
                    Vector3 lookDir = feintRot * feintDir.normalized;
                    Quaternion target = Quaternion.LookRotation(lookDir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 720f * Time.deltaTime);
                }
            }
            else
            {
                FacePlayer(720f);
            }

            if (Time.time >= nextAimRepath)
            {
                nextAimRepath = Time.time + Mathf.Max(0.05f, pathRecalcInterval);
                navAgent.SetDestination(player.position);
            }
            yield return null;
        }

        // — Рывок —
        StopAgentHard();
        PlayRukaAnim(rukaRamAnim);
        Vector3 ramTarget = useLaggedRamTarget ? GetLaggedPlayerPosition() : player.position;
        Vector3 ramDir = ramTarget - transform.position;
        ramDir.y = 0f;
        if (ramDir.sqrMagnitude < 0.0001f)
            ramDir = transform.forward;
        ramDir.Normalize();
        lastRamDir = ramDir;

        float dashSpeed = Mathf.Clamp(Random.Range(ramSpeedMin, ramSpeedMax), 0.1f, 50f);
        if (dashSpeed <= 0f)
            dashSpeed = ramSpeed;

        float ramEnd = Time.time + ramDuration;
        bool ramHitDealt = false;
        Vector3 lastHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
        bool doFeint = currentPhase == BossPhase.Phase2 && Random.value < feintChance;
        float feintTime = Time.time + Mathf.Clamp(Random.Range(feintMinTime, feintMaxTime), 0.05f, ramDuration);
        while (Time.time < ramEnd)
        {
            if (doFeint && Time.time >= feintTime)
            {
                abortRamChain = true;
                yield return FeintRoutine();
                yield break;
            }

            Vector3 currentHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
            if (!ramHitDealt && SegmentHitsPlayer(lastHitOrigin, currentHitOrigin, ramHitRadius))
            {
                if (TryApplyDirectDamage(currentHitOrigin, ramHitRadius, ramDamage))
                    ramHitDealt = true;
            }

            navAgent.Move(ramDir * dashSpeed * Time.deltaTime);
            lastHitOrigin = currentHitOrigin;
            yield return null;
        }

        RegisterRamOutcome(ramHitDealt);

        // — Возврат к преследованию —
        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayBaseAnim(baseWalkAnim);
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
        Vector3 lastHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
        while (Time.time < ramEnd)
        {
            Vector3 currentHitOrigin = GetHitOrigin(ramHitForwardOffset, ramHitUpOffset);
            if (!ramHitDealt && SegmentHitsPlayer(lastHitOrigin, currentHitOrigin, ramHitRadius))
            {
                if (TryApplyDirectDamage(currentHitOrigin, ramHitRadius, ramDamage))
                    ramHitDealt = true;
            }

            navAgent.Move(reverseDir * dashSpeed * Time.deltaTime);
            lastHitOrigin = currentHitOrigin;
            yield return null;
        }

        RegisterRamOutcome(ramHitDealt);
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

        SpawnLeska(transform.TransformPoint(leskaSpawnOffsetFront));
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
            ApplyHealthDamage(incomingDamage);
        }
        else
        {
            ApplyArmorDamage(incomingDamage);
        }

        // Замедление при уроне — только в фазе 2
        if (currentPhase == BossPhase.Phase2)
            TryApplySlow();
    }

    /// <summary>
    /// С вероятностью slowOnHitChance применяет временное замедление (фаза 2).
    /// </summary>
    private void TryApplySlow()
    {
        if (Random.value > slowOnHitChance) return; // Шанс не сработал — не тормозим

        if (slowRoutine != null)
            StopCoroutine(slowRoutine);

        slowRoutine = StartCoroutine(SlowRoutine());
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
        isStunSequence = true;
        StopAgentHard();
        isArmorBroken = true;
        isVulnerable = false;

        if (audioSource != null && armorBreakClip != null)
            audioSource.PlayOneShot(armorBreakClip);

        PlayBaseAnim(baseGetDownAnim);
        if (getDownDuration > 0f)
            yield return new WaitForSeconds(getDownDuration);

        string tryaskaAnim = (Random.value < 0.5f) ? baseTryaska1Anim : baseTryaska2Anim;
        PlayBaseAnim(tryaskaAnim);
        isVulnerable = true;
        if (tryaskaDuration > 0f)
            yield return new WaitForSeconds(tryaskaDuration);

        isVulnerable = false;
        if (isDead)
            yield break;

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
            StartCoroutine(ArmorBreakRoutine());
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

        Debug.Log("[BossEnemy] Босс умер.");
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

    private void ApplyNavTuning()
    {
        if (navAgent == null) return;
        navAgent.acceleration = navAcceleration;
        navAgent.angularSpeed = navAngularSpeed;
        navAgent.stoppingDistance = navStoppingDistance;
        navAgent.autoBraking = false;
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
        if (hitDealt)
        {
            dodgeStreak = 0;
            adaptiveDodgeThreshold = GetAdaptiveDodgeThreshold();
            return;
        }

        dodgeStreak++;
    }

    private int GetAdaptiveDodgeThreshold()
    {
        int min = Mathf.Max(1, adaptiveDodgeThresholdMin);
        int max = Mathf.Max(min, adaptiveDodgeThresholdMax);
        return Random.Range(min, max + 1);
    }
}
