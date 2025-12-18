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

    // Задержка перед появлением Canvas'а после проигрывания звука
    [SerializeField] private float loseScreenDelayAfterSound = 2f;

    // ← НОВОЕ: Прямое назначение Canvas'а в инспекторе
    [Header("UI")]
    [SerializeField] private GameObject loseScreenCanvas;

    private int currentWaypointIndex = 0;
    private bool isPatrolling = true;
    private bool isChasing = false;
    private bool caughtPlayer = false;

    private Transform player;
    private Vector3 playerLastPosition = Vector3.zero;
    private bool isStunned = false;

    // Чтобы экран поражения показался только один раз (при нескольких врагах)
    private static bool hasShownLoseScreen = false;

    void Start()
    {
        if (!gameObject.activeInHierarchy) return;

        navMeshAgent = GetComponent<NavMeshAgent>();
        m_Animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        // Добавляем Collider и Rigidbody, если их нет
        if (GetComponent<Collider>() == null)
        {
            var collider = gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 1, 0);
            collider.radius = 0.5f;
            collider.height = 2f;
        }

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = true;
        }

        gameObject.layer = LayerMask.NameToLayer("Enemy");

        // Убеждаемся, что Canvas изначально скрыт (на всякий случай)
        if (loseScreenCanvas != null)
            loseScreenCanvas.SetActive(false);

        GoToNextWaypoint();
    }

    void Update()
    {
        if (caughtPlayer || !gameObject.activeInHierarchy || isStunned) return;

        if (isPatrolling && navMeshAgent.enabled && 
            navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && 
            !navMeshAgent.pathPending)
            GoToNextWaypoint();

        CheckForPlayer();

        m_Animator.SetFloat("Speed", navMeshAgent.velocity.magnitude);
        m_Animator.SetBool("isChasing", isChasing);

        if (IsCloseToPlayer())
            CatchPlayer();
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
        return Vector3.Distance(transform.position, player.position) <= 1.5f;
    }

    void CatchPlayer()
    {
        if (caughtPlayer || hasShownLoseScreen) return;

        caughtPlayer = true;
        hasShownLoseScreen = true;

        m_Animator.SetBool("IsCaughtPlayer", true);
        navMeshAgent.isStopped = true;

        StartCoroutine(CatchSequence());
    }

    IEnumerator CatchSequence()
    {
        // 1. Ждём окончания анимации захвата
        yield return new WaitForSeconds(1.5f);

        // 2. Замораживаем игровое время (физика, движение, AI — всё останавливается)
        Time.timeScale = 0f;

        // 3. Проигрываем звук поимки (он будет играть в unscaled time)
        if (catchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(catchSound);
            
            // Ждём окончания звука в РЕАЛЬНОМ времени (игрок услышит его полностью)
            float soundLength = catchSound.length;
            yield return new WaitForSecondsRealtime(soundLength);
        }
        else
        {
            // Небольшая задержка, если звука нет
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // 4. Показываем экран поражения
        if (loseScreenCanvas != null)
        {
            loseScreenCanvas.SetActive(true);

            // Делаем UI интерактивным (на всякий случай)
            CanvasGroup canvasGroup = loseScreenCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = loseScreenCanvas.AddComponent<CanvasGroup>();

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            Time.timeScale = 1f;
        }
        else
        {
            Debug.LogError("LoseScreenCanvas не назначен в инспекторе у врага!");
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