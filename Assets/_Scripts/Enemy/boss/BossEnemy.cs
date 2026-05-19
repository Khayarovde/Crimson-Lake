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
/// Получение урона:
///   — Урон копится, но основное HP уменьшается только после серии:
///     GetDown -> Tryaska(1/2) -> (HP-).
///   — Если HP остаётся, босс делает wakeUp_stun и возвращается к walking.
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

    // ─── Стан/ломаемая полоска ───────────────────────────────────────────────

    [Header("Stun / Break Sequence")]
    [Tooltip("Суммарный урон, после которого запускается GetDown -> Tryaska.")]
    public float stunDamageThreshold = 60f;
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

    [Header("Close Attack (Ruka Attack2)")]
    [Tooltip("Если игрок слишком близко, проигрывается Attack2 на слое Ruka.")]
    public float closeAttackDistance = 1.6f;
    [Tooltip("Урон ближней атаки.")]
    public int closeAttackDamage = 15;
    [Tooltip("Кулдаун между близкими атаками.")]
    public float closeAttackCooldown = 1.1f;

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
    [Tooltip("Расстояние от босса, на котором спавнится леска.")]
    public float leskaSpawnOffset = 1.5f;
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

    private Coroutine slowRoutine;

    private float pendingDamage;
    private float accumulatedStunDamage;

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
        pendingDamage = 0f;
        accumulatedStunDamage = 0f;

        ApplyNavTuning();
        SetAgentSpeed(chaseSpeed);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            ResolveAnimatorLayers();
        }
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

        // Во время тарана, стана или спавна лесок — управление передано корутине
        if (isDoingRam || isDoingCloseAttack || isSpawningLeska || isStunSequence)
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

        TryApplyDirectDamage(transform.position, closeAttackDistance, closeAttackDamage);

        yield return new WaitForSeconds(0.15f);

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

        isDoingRam = false;
    }

    private IEnumerator DoubleRamRoutine()
    {
        isDoingRam = true;
        nextRamTime = Time.time + ramCooldown;

        yield return RamOnceRoutine(1f);

        if (doubleRamGap > 0f)
            yield return new WaitForSeconds(doubleRamGap);

        yield return RamOnceRoutine(Mathf.Clamp(doubleRamSecondAimScale, 0.2f, 1f));

        isDoingRam = false;
    }

    private IEnumerator RamOnceRoutine(float aimScale)
    {
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
        Vector3 ramDir = player.position - transform.position;
        ramDir.y = 0f;
        if (ramDir.sqrMagnitude < 0.0001f)
            ramDir = transform.forward;
        ramDir.Normalize();

        float dashSpeed = Mathf.Clamp(Random.Range(ramSpeedMin, ramSpeedMax), 0.1f, 50f);
        if (dashSpeed <= 0f)
            dashSpeed = ramSpeed;

        float ramEnd = Time.time + ramDuration;
        bool ramHitDealt = false;
        while (Time.time < ramEnd)
        {
            if (!ramHitDealt && TryApplyDirectDamage(transform.position, ramHitRadius, ramDamage))
                ramHitDealt = true;

            navAgent.Move(ramDir * dashSpeed * Time.deltaTime);
            yield return null;
        }

        // — Возврат к преследованию —
        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayBaseAnim(baseWalkAnim);
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

        SpawnLeska(transform.position + transform.forward * leskaSpawnOffset);
        SpawnLeska(transform.position - transform.forward * leskaSpawnOffset);

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

        pendingDamage += incomingDamage;

        if (stunDamageThreshold > 0f)
        {
            accumulatedStunDamage += incomingDamage;
            if (!isStunSequence && accumulatedStunDamage >= stunDamageThreshold)
            {
                accumulatedStunDamage = 0f;
                StartCoroutine(StunBreakRoutine());
            }
        }
        else
        {
            ApplyPendingDamage();
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

    private IEnumerator StunBreakRoutine()
    {
        isStunSequence = true;
        StopAgentHard();

        PlayBaseAnim(baseGetDownAnim);
        if (getDownDuration > 0f)
            yield return new WaitForSeconds(getDownDuration);

        string tryaskaAnim = (Random.value < 0.5f) ? baseTryaska1Anim : baseTryaska2Anim;
        PlayBaseAnim(tryaskaAnim);
        if (tryaskaDuration > 0f)
            yield return new WaitForSeconds(tryaskaDuration);

        if (ApplyPendingDamage())
            yield break;

        PlayBaseAnim(baseWakeUpAnim);
        if (wakeUpDuration > 0f)
            yield return new WaitForSeconds(wakeUpDuration);

        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayBaseAnim(baseWalkAnim);
        isStunSequence = false;
    }

    private bool ApplyPendingDamage()
    {
        if (pendingDamage <= 0f)
            return false;

        health -= pendingDamage;
        pendingDamage = 0f;
        health = Mathf.Max(0f, health);

        TryEnterPhase2();

        if (health <= 0f)
        {
            Die();
            return true;
        }

        return false;
    }

    private void Die()
    {
        isDead = true;
        isStunSequence = false;
        StopAllCoroutines();
        StopAgentHard();
        PlayBaseAnim(baseDeathAnim);
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
        float distSqr = (player.position - sourcePosition).sqrMagnitude;
        if (distSqr > hitRadius * hitRadius)
            return false;

        playerHealth.ApplyDamage(damage);
        return true;
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
        Gizmos.DrawWireSphere(transform.position + transform.forward * leskaSpawnOffset, 0.25f);
        Gizmos.DrawWireSphere(transform.position - transform.forward * leskaSpawnOffset, 0.25f);

        // Порог перехода в фазу 2 (текст в Scene view)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
