﻿using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections;

public class AdvancedEnemyAI : MonoBehaviour
{
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private float speedWalk = 6f;
    [SerializeField] private float speedRun = 9f;

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;

    [Header("Detection")]
    [SerializeField] private float viewRadius = 15f;
    [SerializeField] private float viewAngle = 90f;

    [Header("Catch Settings")]
    [SerializeField] private AudioClip catchSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField, Tooltip("Задержка от старта удара до нанесения урона (реальное время)")]
    private float attackWindupTime = 0.25f;
    [SerializeField, Tooltip("Сколько времени враг стоит на месте во время удара (реальное время)")]
    private float attackLockTime = 0.6f;
    [SerializeField, Tooltip("Множитель скорости анимации на время удара/захвата")]
    private float attackAnimationSpeed = 1.6f;
    [SerializeField, Tooltip("Максимальный угол (в градусах) между forward врага и направлением на игрока для атаки/урона")] 
    private float maxAttackAngle = 60f;
    [SerializeField, Tooltip("Автоповорот к игроку перед атакой (только по оси Y)")] 
    private bool facePlayerOnAttack = true;
    [SerializeField, Tooltip("Имя Trigger параметра атаки в Animator (если есть)")]
    private string attackTrigger = "Attack";
    [SerializeField, Tooltip("Имя анимационного стейта атаки (для принудительного CrossFade)")]
    private string attackStateName = "Attack";
    [SerializeField, Tooltip("Слой аниматора, где лежит атака")]
    private int attackStateLayer = 0;
    [SerializeField, Tooltip("Радиус хитбокса удара (OverlapSphere)")]
    private float attackHitRadius = 1.2f;
    [SerializeField, Tooltip("Смещение хитбокса вперёд вдоль forward")]
    private float attackHitForwardOffset = 0.6f;

    [Header("Hitbox")]
    [SerializeField, Tooltip("Радиус капсулы коллайдера врага (для более лёгкого попадания)")]
    private float enemyColliderRadius = 0.7f;
    [SerializeField, Tooltip("Высота капсулы коллайдера врага")]
    private float enemyColliderHeight = 2.2f;
    [SerializeField, Tooltip("Центр капсулы коллайдера врага")]
    private Vector3 enemyColliderCenter = new Vector3(0f, 1.1f, 0f);

    // Задержка перед появлением Canvas'а после проигрывания звука
    [SerializeField] private float loseScreenDelayAfterSound = 2f;

    // ← НОВОЕ: Прямое назначение Canvas'а в инспекторе
    [Header("UI")]
    [SerializeField] private GameObject loseScreenCanvas;

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

    // Чтобы экран поражения показался только один раз (при нескольких врагах)
    private static bool hasShownLoseScreen = false;

    void Start()
    {
        if (!gameObject.activeInHierarchy) return;

        navMeshAgent = GetComponent<NavMeshAgent>();
        m_Animator = GetComponent<Animator>();
        if (m_Animator != null) baseAnimatorSpeed = m_Animator.speed;

        ResolveLoseCanvasIfMissing();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();

            // Если компонент не повесили вручную — добавим сами, иначе враг не сможет "наносить удары".
            if (playerHealth == null)
                playerHealth = player.gameObject.AddComponent<PlayerHealth>();

            if (loseScreenCanvas != null)
                playerHealth.SetLoseCanvas(loseScreenCanvas);
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

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = true;
        }

        gameObject.layer = LayerMask.NameToLayer("Enemy");

        // Убеждаемся, что Canvas изначально скрыт (на всякий случай)
        if (loseScreenCanvas != null) loseScreenCanvas.SetActive(false);

        GoToNextWaypoint();
    }

    void Update()
    {
        if (caughtPlayer || !gameObject.activeInHierarchy || isStunned) return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            caughtPlayer = true;
            if (navMeshAgent != null) navMeshAgent.isStopped = true;
            return;
        }

        if (isPatrolling && navMeshAgent.enabled && 
            navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && 
            !navMeshAgent.pathPending)
            GoToNextWaypoint();

        CheckForPlayer();

        m_Animator.SetFloat("Speed", navMeshAgent.velocity.magnitude);
        m_Animator.SetBool("isChasing", isChasing);

        if (IsCloseToPlayer())
            AttackPlayer();
    }

    void CheckForPlayer()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool canSee = dist <= viewRadius && InSightCone(player.position);

        if (canSee)
        {
            playerLastPosition = player.position;
            if (!isChasing)
            {
                isPatrolling = false;
                isChasing = true;
                navMeshAgent.speed = speedRun;
            }
            if (navMeshAgent.enabled)
                navMeshAgent.SetDestination(player.position);
        }
        else if (isChasing)
        {
            if (navMeshAgent.enabled && !navMeshAgent.hasPath)
                navMeshAgent.SetDestination(playerLastPosition);

            if (navMeshAgent.enabled && 
                navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && 
                !navMeshAgent.pathPending)
                StopChasing();
        }
    }

    bool InSightCone(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        return Vector3.Angle(transform.forward, dir) <= viewAngle / 2f;
    }

    void StopChasing()
    {
        isChasing = false;
        isPatrolling = true;
        navMeshAgent.speed = speedWalk;
        if (navMeshAgent.enabled)
            GoToNextWaypoint();
    }

    void GoToNextWaypoint()
    {
        if (waypoints.Length == 0) return;
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        if (navMeshAgent.enabled)
            navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
    }

    bool IsCloseToPlayer()
    {
        if (player == null) return false;
        float range = attackRange;
        if (navMeshAgent != null)
            range = Mathf.Max(range, navMeshAgent.stoppingDistance + 0.1f);
        return Vector3.Distance(transform.position, player.position) <= range;
    }

    void AttackPlayer()
    {
        if (isAttacking) return;
        if (isStunned) return;
        if (playerHealth == null || playerHealth.IsDead) return;
        if (Time.time < nextAttackTime) return;
        if (!IsFacingPlayer()) return; // Атакуем только когда смотрим на игрока

        nextAttackTime = Time.time + attackCooldown;
        StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        isAttacking = true;

        if (isStunned)
        {
            isAttacking = false;
            yield break;
        }

        if (navMeshAgent != null) navMeshAgent.isStopped = true;

        // Повернуться к игроку перед ударом (только по оси Y)
        if (facePlayerOnAttack)
            FacePlayerOnY();

        if (m_Animator != null)
        {
            PlayAttackAnimation();
        }

        if (attackWindupTime > 0f)
            yield return new WaitForSecondsRealtime(attackWindupTime);

        if (!caughtPlayer && !isStunned)
            TryDealDamage();

        if (playerHealth != null && playerHealth.IsDead)
        {
            caughtPlayer = true;
            hasShownLoseScreen = true;
            if (m_Animator != null) m_Animator.SetBool("IsCaughtPlayer", true);
            if (catchSound != null && audioSource != null) audioSource.PlayOneShot(catchSound);
        }

        float remainingLock = Mathf.Max(0f, attackLockTime - attackWindupTime);
        if (remainingLock > 0f)
            yield return new WaitForSecondsRealtime(remainingLock);

        if (m_Animator != null)
            m_Animator.speed = baseAnimatorSpeed;
        if (!caughtPlayer && navMeshAgent != null) navMeshAgent.isStopped = false;

        isAttacking = false;
    }

    private void PlayAttackAnimation()
    {
        if (m_Animator == null) return;

        m_Animator.speed = baseAnimatorSpeed * Mathf.Max(0.1f, attackAnimationSpeed);

        if (HasTrigger(m_Animator, attackTrigger))
            m_Animator.SetTrigger(attackTrigger);

        if (!string.IsNullOrEmpty(attackStateName) && HasState(m_Animator, attackStateLayer, attackStateName))
        {
            m_Animator.Play(attackStateName, attackStateLayer, 0f);
            m_Animator.CrossFadeInFixedTime(attackStateName, 0.5f, attackStateLayer);
        }
    }

    private void TryDealDamage()
    {
        var ph = GetPlayerHealth();
        if (ph == null || ph.IsDead) return;

        // Урон только если враг смотрит на игрока
        if (!IsFacingPlayer()) return;

        Vector3 center = transform.position + transform.forward * attackHitForwardOffset;
        Collider[] hits = Physics.OverlapSphere(center, attackHitRadius, LayerMask.GetMask("Default", "Player"));
        foreach (var h in hits)
        {
            if (h == null) continue;
            var target = h.GetComponent<PlayerHealth>();
            if (target == null) continue;
            target.TakeEnemyHit(this);
            break;
        }
    }

    private bool IsFacingPlayer()
    {
        if (player == null) return false;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return false;
        toPlayer.Normalize();
        float dot = Vector3.Dot(transform.forward, toPlayer);
        float cosLimit = Mathf.Cos(maxAttackAngle * Mathf.Deg2Rad);
        return dot >= cosLimit;
    }

    private void FacePlayerOnY()
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private PlayerHealth GetPlayerHealth()
    {
        if (playerHealth != null) return playerHealth;
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
        return playerHealth;
    }

    private static bool HasTrigger(Animator animator, string param)
    {
        if (animator == null || string.IsNullOrWhiteSpace(param)) return false;
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == param)
                return true;
        return false;
    }

    private static bool HasState(Animator animator, int layer, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;
        return animator.HasState(layer, Animator.StringToHash(stateName));
    }

    private void ResolveLoseCanvasIfMissing()
    {
        if (loseScreenCanvas != null) return;

        var byName = GameObject.Find("LoseScreen") ?? GameObject.Find("LoseScreenCanvas") ?? GameObject.Find("Lose Canvas");
        if (byName != null)
        {
            loseScreenCanvas = byName;
            return;
        }

        #if UNITY_2023_1_OR_NEWER
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        #else
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
        #endif
        foreach (var c in canvases)
        {
            if (c == null) continue;
            string n = c.gameObject.name.ToLowerInvariant();
            if (n.Contains("lose") || n.Contains("gameover") || n.Contains("defeat") || n.Contains("dead"))
            {
                loseScreenCanvas = c.gameObject;
                return;
            }
        }
    }

    // Кнопки на экране поражения
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        hasShownLoseScreen = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        hasShownLoseScreen = false;
        SceneManager.LoadScene("Menu");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward * viewRadius;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward * viewRadius;

        Gizmos.DrawRay(transform.position, left);
        Gizmos.DrawRay(transform.position, right);
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

    public void ApplyStun(float duration)
    {
        isStunned = true;
        navMeshAgent.isStopped = true;
        m_Animator.SetBool("IsStunned", true); // Включаем анимацию стана

        // Запускаем анимацию стана через триггер
        m_Animator.Rebind();
        // Активируем Stun Layer
        m_Animator.SetLayerWeight(2, 1f); // Максимальный вес Stun Layer
        // Уменьшаем вес Base Layer до минимума (почти 0)
        m_Animator.SetLayerWeight(1, 0f); // Оставляем минимальный вес для сохранения целостности модели
        StartCoroutine(RevertFromStun(duration));
    }

    IEnumerator RevertFromStun(float duration)
    {
        yield return new WaitForSeconds(duration); // Ждём конец стана
        // Запускаем анимацию стана через триггер
        m_Animator.Rebind();
        // Уменьшаем вес Base Layer до минимума (почти 0)
        m_Animator.SetLayerWeight(1, 0f); // Оставляем минимальный вес для сохранения целостности модели
        // Активируем Stun Layer
        m_Animator.SetLayerWeight(2, 0f); // Максимальный вес Stun Layer
        m_Animator.SetLayerWeight(3, 1f);

        yield return new WaitForSeconds(2f); 
        isStunned = false;
        isAttacking = false;
        navMeshAgent.isStopped = false; // Возвращаем подвижность
        m_Animator.SetLayerWeight(3, 0f);
        m_Animator.SetLayerWeight(1, 1f); // Возвращаем Base Layer
        m_Animator.SetBool("IsStunned", false); // Выключаем состояние стана
    }
    /// <summary>
    /// Принудительно запускает преследование игрока (активация охоты после взятия дискеты)
    /// </summary>
    public void StartChasingAfterDiskette()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("Не найден игрок с тегом 'Player' для запуска преследования!");
                return;
            }
        }

        // Сразу переходим в режим погони
        isPatrolling = false;
        isChasing = true;
        navMeshAgent.speed = speedRun;

        // Устанавливаем цель — игрок
        if (navMeshAgent.enabled)
            navMeshAgent.SetDestination(player.position);

        // Обновляем аниматор
        m_Animator.SetBool("isChasing", true);

        Debug.Log("Враг активирован и начал преследование игрока после взятия дискеты!");
    }
}