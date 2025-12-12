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

    [SerializeField] private LayerMask playerMask;

    [Header("Catch Settings")]
    [SerializeField] private AudioClip catchSound;
    [SerializeField] private AudioSource audioSource;

    private int currentWaypointIndex = 0;
    private bool isPatrolling = true;
    private bool isChasing = false;
    private bool caughtPlayer = false;

    private Transform player;
    private GameObject loseScreenCanvas;
    private Vector3 playerLastPosition = Vector3.zero;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        m_Animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        loseScreenCanvas = GameObject.Find("LoseScreenCanvas");
        if (loseScreenCanvas != null)
            loseScreenCanvas.SetActive(false);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        // ← НОВОЕ: Добавляем Collider для raycast от игрока
        if (GetComponent<Collider>() == null)
        {
            var collider = gameObject.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 1, 0);  // Центр на уровне тела
            collider.radius = 0.5f;  // Радиус
            collider.height = 2f;    // Высота
        }

        // ← НОВОЕ: Добавляем Rigidbody для толчка (изначально kinematic)
        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = true;  // Включаем гравитацию для реализма во время толчка
        }

        // Устанавливаем layer "Enemy" (создай layer в Unity: Edit > Project Settings > Tags and Layers)
        gameObject.layer = LayerMask.NameToLayer("Enemy");

        GoToNextWaypoint();
    }

    void Update()
    {
        if (caughtPlayer) return;

        if (isPatrolling && navMeshAgent.enabled && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && !navMeshAgent.pathPending)
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

            if (navMeshAgent.enabled && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && !navMeshAgent.pathPending)
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
        if (caughtPlayer) return;
        caughtPlayer = true;

        // Включение анимации захвата
        m_Animator.SetBool("IsCaughtPlayer", caughtPlayer);
        navMeshAgent.isStopped = true;
        // Ждём завершения анимации захвата (таймер 1.5 секунды)
        StartCoroutine(WaitForAnimationComplete());
    }

    IEnumerator WaitForAnimationComplete()
    {
        yield return new WaitForSeconds(1.5f);
        
        // Останавливаем врага и время
        // navMeshAgent.isStopped = true;
        Time.timeScale = 0f;

        // Запускаем корутину, которая сначала проиграет звук, а ПОТОМ покажет экран
        StartCoroutine(PlayCatchSoundAndShowLoseScreen());
    }

    IEnumerator PlayCatchSoundAndShowLoseScreen()
    {
        // Проигрываем звук (если есть)
        if (catchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(catchSound);

            // Ждём, пока звук доиграет до конца
            yield return new WaitForSecondsRealtime(catchSound.length);
        }
        else
        {
            // Если звука нет — небольшая задержка, чтобы был "эффект"
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // Только теперь показываем экран поражения
        if (loseScreenCanvas != null)
        {
            loseScreenCanvas.SetActive(true);
        }
    }

    // Эту функцию вешаешь на кнопку "Restart" на LoseScreenCanvas
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Опционально — кнопка в меню
    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu"); // или как у тебя называется сцена меню
    }

    // Гизмосы
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 left = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward * viewRadius;
        Vector3 right = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward * viewRadius;

        Gizmos.DrawRay(transform.position, left);
        Gizmos.DrawRay(transform.position, right);
    }
}