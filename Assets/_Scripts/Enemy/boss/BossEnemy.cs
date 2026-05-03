using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Босс с двумя фазами поведения.
///
/// Фаза 1 (BossPhase.Phase1):
///   — Постоянно преследует игрока через NavMesh.
///   — Периодически делает ТАРАН: разгоняется, бьёт при контакте.
///   — Во время прицеливания перед тараном тормозит.
///
/// Фаза 2 (BossPhase.Phase2, активируется при HP <= phase2HealthThreshold):
///   — Все механики фазы 1 остаются.
///   — Добавляется способность СПАВНИТЬ ЛЕСКИ: перед собой и за собой.
///   — При получении урона: с вероятностью slowOnHitChance — теряет скорость,
///     иначе — игнорирует замедление.
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
    public bool useAttackHitEvent = false;
    public int attackEventDamage = 15;
    public float attackEventRadius = 1.1f;
    public float attackEventCooldown = 0.08f;

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

    [Header("Animations")]
    [SerializeField] private string idleAnim = "Idle";
    [SerializeField] private string walkAnim = "walking";
    [SerializeField] private string ramAimAnim = "Scream";
    [SerializeField] private string ramChargeAnim = "Attack";
    [SerializeField] private string hitAnim = "Hit";
    [SerializeField] private string leskaSpawnAnim = "Scream";
    [SerializeField] private string deathAnim = "death_padaet";
    [SerializeField] private float animCrossFade = 0.12f;
    [SerializeField] private int animLayer = 0;

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

    private float nextRamTime;
    private float nextLeskaTime;
    private float nextRepathTime;
    private float nextAttackEventTime;

    private bool isDoingRam;
    private bool isSpawningLeska;

    private string currentAnimState;

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

        ApplyNavTuning();
        SetAgentSpeed(chaseSpeed);

        if (animator != null)
            animator.applyRootMotion = false;
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
        if (!useAttackHitEvent)
            return;

        if (Time.time < nextAttackEventTime)
            return;

        nextAttackEventTime = Time.time + Mathf.Max(0f, attackEventCooldown);
        DealAttackEventDamage();
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

        // Во время тарана или спавна лесок — управление передано корутине
        if (isDoingRam || isSpawningLeska)
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
        float dist = Vector3.Distance(transform.position, player.position);

        // Попытка тарана
        if (Time.time >= nextRamTime && dist <= ramTriggerDistance)
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

        PlayAnim(walkAnim);
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

        // — Прицеливание —
        SetAgentSpeed(aimingSpeed);
        navAgent.isStopped = false;
        navAgent.SetDestination(player.position);
        PlayAnim(ramAimAnim);

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
        PlayAnim(ramChargeAnim);
        Vector3 ramDir = player.position - transform.position;
        ramDir.y = 0f;
        if (ramDir.sqrMagnitude < 0.0001f)
            ramDir = transform.forward;
        ramDir.Normalize();

        bool hit = false;
        float ramEnd = Time.time + ramDuration;
        while (Time.time < ramEnd)
        {
            navAgent.Move(ramDir * ramSpeed * Time.deltaTime);
            if (!hit)
            {
                hit = TryDealRamDamage();
            }
            yield return null;
        }

        // — Возврат к преследованию —
        navAgent.isStopped = false;
        SetAgentSpeed(isSlow ? chaseSpeed * slowSpeedMultiplier : chaseSpeed);
        PlayAnim(walkAnim);

        isDoingRam = false;
    }

    /// <summary>
    /// Наносит урон от тарана через OverlapSphere.
    /// </summary>
    private bool TryDealRamDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, ramHitRadius, playerLayer, QueryTriggerInteraction.Collide);
        foreach (Collider c in hits)
        {
            PlayerHealth ph = c.transform.root.GetComponent<PlayerHealth>();
            if (ph != null && !ph.IsDead)
            {
                ph.ApplyDamage(ramDamage);
                return true;
            }
        }

        if (playerHealth != null && !playerHealth.IsDead)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= ramHitRadius)
            {
                playerHealth.ApplyDamage(ramDamage);
                return true;
            }
        }

        return false;
    }

    private void DealAttackEventDamage()
    {
        if (attackEventDamage <= 0)
            return;

        float radius = Mathf.Max(0.1f, attackEventRadius);
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, playerLayer, QueryTriggerInteraction.Collide);
        foreach (Collider c in hits)
        {
            PlayerHealth ph = c.transform.root.GetComponent<PlayerHealth>();
            if (ph != null && !ph.IsDead)
            {
                ph.ApplyDamage(attackEventDamage);
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
        PlayAnim(leskaSpawnAnim);

        if (audioSource != null && leskaSpawnClip != null)
            audioSource.PlayOneShot(leskaSpawnClip);

        yield return new WaitForSeconds(leskaSpawnAnimDuration);

        SpawnLeska(transform.position + transform.forward * leskaSpawnOffset);
        SpawnLeska(transform.position - transform.forward * leskaSpawnOffset);

        navAgent.isStopped = false;
        PlayAnim(walkAnim);
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

        health -= incomingDamage;
        health = Mathf.Max(0f, health);

        if (hitEffect != null)
            hitEffect.Play();

        // Реакция на урон — анимация хита (только если не делаем таран)
        if (!isDoingRam)
            PlayAnim(hitAnim);

        // Замедление при уроне — шанс сработать в обеих фазах
        TryApplySlow();

        if (health <= 0f)
        {
            Die();
            return;
        }
    }

    /// <summary>
    /// С вероятностью slowOnHitChance применяет временное замедление.
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

    private void Die()
    {
        isDead = true;
        StopAllCoroutines();
        StopAgentHard();
        PlayAnim(deathAnim);
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

    private void PlayAnim(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return;
        if (stateName == currentAnimState) return;
        if (!animator.HasState(animLayer, Animator.StringToHash(stateName))) return;

        animator.CrossFadeInFixedTime(stateName, animCrossFade, animLayer);
        currentAnimState = stateName;
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

        // Точки спавна лесок
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + transform.forward * leskaSpawnOffset, 0.25f);
        Gizmos.DrawWireSphere(transform.position - transform.forward * leskaSpawnOffset, 0.25f);

        // Порог перехода в фазу 2 (текст в Scene view)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
