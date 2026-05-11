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
    [Tooltip("Длительность самого рывка.")]
    public float ramDuration = 0.6f;
    [Tooltip("Урон от тарана.")]
    public int ramDamage = 25;
    [Tooltip("Кулдаун между таранами.")]
    public float ramCooldown = 4f;
    [Tooltip("Радиус хитбокса при ударе тарана.")]
    public float ramHitRadius = 1.2f;

    [Header("Attack Hitbox (Animation Event)")]
    public bool useAttackHitEvent = true;
    public int attackEventDamage = 15;
    public float attackEventRadius = 1.1f;
    public float attackEventCooldown = 0.08f;

    [Header("Attack Hitboxes")]
    public Transform headHitPoint;
    public Transform leftHandHitPoint;
    public Transform rightHandHitPoint;

    [Header("Close Attack (Ruka Attack2)")]
    [Tooltip("Если игрок слишком близко, проигрывается Attack2 на слое Ruka.")]
    public float closeAttackDistance = 1.6f;
    [Tooltip("Кулдаун между близкими атаками.")]
    public float closeAttackCooldown = 1.1f;

    [Header("Hit Detection")]
    [SerializeField, Range(1, 32)] private int maxHitColliders = 8;

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
    public LayerMask playerLayer;

    // ─── Внутреннее состояние ─────────────────────────────────────────────────

    private NavMeshAgent navAgent;
    private Transform player;
    private PlayerHealth playerHealth;

    private BossPhase currentPhase = BossPhase.Phase1;
    private bool isDead;
    private bool isSlow;
    private bool isStunSequence;

    private float nextRamTime;
    private float nextLeskaTime;
    private float nextRepathTime;
    private float nextCloseAttackTime;

    private bool isDoingRam;
    private bool isSpawningLeska;

    private float pendingDamage;
    private float accumulatedStunDamage;

    private string currentBaseAnimState;
    private string currentRukaAnimState;

    private Collider[] attackHitResults;

    private float nextHeadHitTime;
    private float nextLeftHitTime;
    private float nextRightHitTime;

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
        pendingDamage = 0f;
        accumulatedStunDamage = 0f;

        ApplyNavTuning();
        SetAgentSpeed(chaseSpeed);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            ResolveAnimatorLayers();
        }

        PrepareHitBuffers();
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

    public void AttackHitboxOn()
    {
        AttackHitboxOnHead();
    }

    public void AttackHitboxOnHead()
    {
        if (!useAttackHitEvent)
            return;

        TryDealHit(headHitPoint, ramHitRadius, ramDamage, ref nextHeadHitTime);
    }

    public void AttackHitboxOnLeftHand()
    {
        if (!useAttackHitEvent)
            return;

        TryDealHit(leftHandHitPoint, attackEventRadius, attackEventDamage, ref nextLeftHitTime);
    }

    public void AttackHitboxOnRightHand()
    {
        if (!useAttackHitEvent)
            return;

        TryDealHit(rightHandHitPoint, attackEventRadius, attackEventDamage, ref nextRightHitTime);
    }

    public void AttackHitboxOff()
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (isDead || player == null || navAgent == null || !navAgent.isOnNavMesh)
            return;

        CheckPhaseTransition();

        // Во время тарана, стана или спавна лесок — управление передано корутине
        if (isDoingRam || isSpawningLeska || isStunSequence)
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
    /// Проверяет порог HP и переключает фазу.
    /// </summary>
    private void CheckPhaseTransition()
    {
        if (currentPhase == BossPhase.Phase1 && health <= maxHealth * phase2HealthThreshold)
        {
            currentPhase = BossPhase.Phase2;
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

        // Попытка тарана
        float ramTriggerDistSqr = ramTriggerDistance * ramTriggerDistance;
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

        PlayRukaAnim(rukaCloseAnim);
        nextCloseAttackTime = Time.time + Mathf.Max(0f, closeAttackCooldown);
        return true;
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

        // Запуск атаки сразу при начале тарана
        PlayRukaAnim(rukaRamAnim);

        // — Прицеливание —
        SetAgentSpeed(aimingSpeed);
        navAgent.isStopped = false;
        navAgent.SetDestination(player.position);
        PlayBaseAnim(baseWalkAnim);

        if (audioSource != null && ramRoarClip != null)
            audioSource.PlayOneShot(ramRoarClip);

        float aimEnd = Time.time + ramAimDuration;
        float nextAimRepath = 0f;
        while (Time.time < aimEnd)
        {
            FacePlayer(720f);
            if (Time.time >= nextAimRepath)
            {
                nextAimRepath = Time.time + Mathf.Max(0.05f, pathRecalcInterval);
                navAgent.SetDestination(player.position);
            }
            yield return null;
        }

        // — Рывок —
        StopAgentHard();
        Vector3 ramDir = player.position - transform.position;
        ramDir.y = 0f;
        if (ramDir.sqrMagnitude < 0.0001f)
            ramDir = transform.forward;
        ramDir.Normalize();

        float ramEnd = Time.time + ramDuration;
        while (Time.time < ramEnd)
        {
            navAgent.Move(ramDir * ramSpeed * Time.deltaTime);
            yield return null;
        }

        // — Возврат к преследованию —
        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayBaseAnim(baseWalkAnim);

        isDoingRam = false;
    }

    private void TryDealHit(Transform hitPoint, float hitRadius, int damage, ref float nextHitTime)
    {
        if (damage <= 0)
            return;

        if (hitPoint == null)
            return;

        if (Time.time < nextHitTime)
            return;

        nextHitTime = Time.time + Mathf.Max(0f, attackEventCooldown);

        if (attackHitResults == null || attackHitResults.Length == 0)
            PrepareHitBuffers();

        float radius = Mathf.Max(0.1f, hitRadius);
        int hitCount = Physics.OverlapSphereNonAlloc(
            hitPoint.position,
            radius,
            attackHitResults,
            playerLayer,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = attackHitResults[i];
            if (c == null)
                continue;

            PlayerHealth ph = c.transform.root.GetComponent<PlayerHealth>();
            if (ph != null && !ph.IsDead)
            {
                ph.ApplyDamage(damage);
                break;
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

        StopCoroutine(nameof(SlowRoutine)); // На случай уже активного замедления
        StartCoroutine(SlowRoutine());
    }

    private IEnumerator SlowRoutine()
    {
        isSlow = true;
        SetAgentSpeed(chaseSpeed * slowSpeedMultiplier);

        yield return new WaitForSeconds(slowDuration);

        isSlow = false;
        if (!isDoingRam)
            SetAgentSpeed(chaseSpeed);
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

        CheckPhaseTransition();

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

    private void PrepareHitBuffers()
    {
        int size = Mathf.Max(1, maxHitColliders);

        if (attackHitResults == null || attackHitResults.Length != size)
            attackHitResults = new Collider[size];
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

    private void PlayBaseAnim(string stateName)
    {
        PlayAnimOnLayer(stateName, baseLayerIndex, ref currentBaseAnimState);
    }

    private void PlayRukaAnim(string stateName)
    {
        PlayAnimOnLayer(stateName, rukaLayerIndex, ref currentRukaAnimState);
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
