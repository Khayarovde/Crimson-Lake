using UnityEngine;
using UnityEngine.AI;

public class AdvancedEnemyAI : MonoBehaviour
{
    // Старое поведение сохраняется
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Animator m_Animator;
    [SerializeField] private float speedWalk = 6f;
    [SerializeField] private float speedRun = 9f;

    // Новое поведение патрулирования
    [SerializeField] private Transform[] waypoints;
    private int currentWaypointIndex = 0;

    // Параметры поля зрения
    [SerializeField] private float viewRadius = 15f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private LayerMask playerMask; // Только маска игрока остаётся активной

    // Текущие состояния
    private bool isPatrolling = true;
    private bool isChasing = false;
    private bool caughtPlayer = false;

    // Остальные поля сохранятся
    private Transform player;
    private Vector3 playerLastPosition = Vector3.zero;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        m_Animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        // Начнём патрулирование
        GoToNextWaypoint();
    }

    void Update()
    {
        // Основная логика патрулирования и взаимодействия с игроком
        if (isPatrolling)
        {
            if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                GoToNextWaypoint();
            }
        }

        // Проверка игрока в поле зрения
        CheckForPlayer();

        // Установим параметры анимации
        m_Animator.SetFloat("Speed", navMeshAgent.velocity.magnitude);
        m_Animator.SetBool("isChasing", isChasing);
    }

    void CheckForPlayer()
    {
        if (player == null)
            return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (distToPlayer <= viewRadius && InSightCone(player.position))
        {
            StartChasing();
        }
    }

    bool InSightCone(Vector3 targetPos)
    {
        Vector3 dirToTarget = (targetPos - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToTarget);
        return angle <= viewAngle / 2;
    }

    void StartChasing()
    {
        isPatrolling = false;
        isChasing = true;
        navMeshAgent.speed = speedRun;
        navMeshAgent.SetDestination(player.position);
    }

    void StopChasing()
    {
        isPatrolling = true;
        isChasing = false;
        navMeshAgent.speed = speedWalk;
        GoToNextWaypoint();
    }

    void GoToNextWaypoint()
    {
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        navMeshAgent.SetDestination(waypoints[currentWaypointIndex].position);
    }

    // Показ поля зрения в окне Scene View
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Quaternion leftRotation = Quaternion.Euler(0, -viewAngle / 2, 0);
        Quaternion rightRotation = Quaternion.Euler(0, viewAngle / 2, 0);

        Vector3 leftDir = leftRotation * transform.forward;
        Vector3 rightDir = rightRotation * transform.forward;

        Gizmos.DrawRay(transform.position, leftDir * viewRadius);
        Gizmos.DrawRay(transform.position, rightDir * viewRadius);
    }
}