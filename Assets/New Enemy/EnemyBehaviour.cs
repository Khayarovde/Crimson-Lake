using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyBehaviour : MonoBehaviour
{
    public enum EnemyState { Idle, Patrol, Chase, Attack, Stun, Dead };
    public EnemyState state;
    public Animator anim;
    private NavMeshAgent agent;
    public Transform targetTransform;
    public FinalDoor finalDoor;
    private Rigidbody rb;
    [Header("Path")]
    public Transform[] patrolPoints;
    private int currentPointIndex = 0;

    [Header("Distances")]
    public float chaseRange = 1f;
    public float attackRange = 1f;
    private float distanceFromTarget = Mathf.Infinity;

    [Header("Aim and Detection")]
    public Light spotLight;
    public float viewDistance = 20f;
    float viewAngle;
    public LayerMask viewMask;
    private Coroutine attackRoutine;

    [Header("Timers")]
    public float idleTime = 1f;
    public float stunTime = 1f;
    private float timer = 0f;
    public float cooldownAttack = 1000f;
    private float lastAttackTime = 0;

    [Header("Stats")]
    public bool isBoss;
    public int damagePerHit = 10;
    public float health = 100f;
    public float maxHealth = 100f;

    [Header("Sound Effects")]
    public AudioSource audioSource;
    public AudioClip sfxChase;
    public AudioClip sfxAttack;
    public AudioClip sfxDeath;

    [Header("Particles")]
    public ParticleSystem deathEffect;

    // Константа для основного слоя анимации
    private const int baseAnimLayer = 0;

    void Start()
    {
        anim = GetComponent<Animator>();
        viewAngle = spotLight.spotAngle;
        agent = GetComponent<NavMeshAgent>();
        targetTransform = GameObject.FindGameObjectWithTag("Player").transform;
        rb = GetComponent<Rigidbody>();
        anim.SetBool("IsAttacking", false); // Инициализация флага атаки
        SetIdle();
    }

    void Update()
    {
        distanceFromTarget = GetDistanceFromTarget();

        // Логика смены направления взгляда на игрока
        if (distanceFromTarget < attackRange)
        {
            transform.LookAt(targetTransform);
        }

        switch (state)
        {
            case EnemyState.Idle:
                IdleUpdate();
                break;
            case EnemyState.Patrol:
                PatrolUpdate();
                break;
            case EnemyState.Chase:
                ChaseUpdate();
                break;
            case EnemyState.Attack:
                AttackUpdate();
                break;
            case EnemyState.Stun:
                StunUpdate();
                break;
            case EnemyState.Dead:
                DeadUpdate();
                break;
            default:
                break;
        }
    }

    #region State Methods
    void IdleUpdate()
    {
        if (timer >= idleTime)
        {
            SetPatrol();
        }
        else
        {
            timer += Time.deltaTime;
        }
    }

    void PatrolUpdate()
    {
        agent.speed = 5f;
        if (CanSeePlayer())
        {
            spotLight.color = Color.red;
            SetChase();
        }
        else
        {
            spotLight.color = Color.green;
        }

        if (distanceFromTarget < chaseRange)
        {
            SetChase();
            return;
        }

        if (agent.remainingDistance <= agent.stoppingDistance)
        {
            currentPointIndex++;
            if (currentPointIndex >= patrolPoints.Length) currentPointIndex = 0;
            agent.SetDestination(patrolPoints[currentPointIndex].position);
        }
    }

    void ChaseUpdate()
    {
        agent.speed = 0.5f;
        agent.isStopped = false;
        agent.SetDestination(targetTransform.position);

        if (distanceFromTarget <= attackRange)
        {
            agent.isStopped = true; // Остановка физического тела на расстоянии атаки
            SetAttack();
        }

        if (distanceFromTarget > chaseRange)
        {
            SetPatrol();
        }
    }

    // Атака
    IEnumerator AttackRoutine()
    {
        while (true)
        {
            // Начинаем анимацию атаки
            PlayAttackAnimation(1);

            // Ожидаем завершения анимации или заданного времени
            yield return new WaitForSeconds(cooldownAttack);

            // Наносим урон игроку
            targetTransform.GetComponent<PlayerHealth>()?.TakeEnemyHit();
            Debug.Log("Enemy Hitting!");
        }
    }

    void AttackUpdate()
    {
        // Если игрок в зоне атаки, инициируем цикл атаки
        if (distanceFromTarget <= attackRange)
        {
            if (attackRoutine == null)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
        }
        else
        {
            // Если игрок ушёл из зоны атаки, останавливаем цикл
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
        }
    }
    
    void DeadUpdate()
    {
        // Завершаем уничтожение объекта спустя некоторое время
        Destroy(gameObject, 0.5f);
    }
    #endregion

    #region Utility Methods
    void SetIdle()
    {
        timer = 0f;
        anim.SetBool("IsAttacking", false); // Отключаем атаку
        state = EnemyState.Idle;
    }

    void SetPatrol()
    {
        agent.isStopped = false;
        
        // Проверяем границу массива перед изменением текущего индекса
        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
        
        // Устанавливаем новую цель движения
        agent.SetDestination(patrolPoints[currentPointIndex].position);

        anim.SetBool("IsAttacking", false); // Отключаем атаку
        state = EnemyState.Patrol;
    }

    void SetChase()
    {
        PlaySFX(sfxChase); // Проигрываем звук погонь
        anim.SetBool("IsAttacking", false); // Отключаем атаку
        state = EnemyState.Chase;
    }

    void SetAttack()
    {
        anim.SetBool("IsAttacking", true); // Активируем атаку
        state = EnemyState.Attack;
    }

    void SetStun()
    {
        anim.SetBool("IsAttacking", false); // Отключаем атаку
        state = EnemyState.Stun;
    }

    void SetDead()
    {
        deathEffect.Play(); // Воспроизводим эффект смерти
        PlaySFX(sfxDeath); // Проигрываем звук смерти
        anim.SetBool("IsAttacking", false); // Отключаем атаку
        state = EnemyState.Dead;
        if (isBoss)
        {
            finalDoor.OpenFinalDoor();
        }
    }

    float GetDistanceFromTarget()
    {
        return Vector3.Distance(targetTransform.position, transform.position);
    }

    bool CanSeePlayer()
    {
        if (Vector3.Distance(transform.position, targetTransform.position) < viewDistance)
        {
            Vector3 dirToPlayer = (targetTransform.position - transform.position).normalized;
            float angleBetweenEnemyAndPlayer = Vector3.Angle(transform.forward, dirToPlayer);
            if (angleBetweenEnemyAndPlayer < viewAngle / 2f)
            {
                if (!Physics.Linecast(transform.position, targetTransform.position, viewMask))
                {
                    return true;
                }
            }
        }
        return false;
    }

    void StunUpdate()
    {
        // Пример реализации поведения в состоянии оглушения
        if (timer >= stunTime)
        {
            SetPatrol(); // Возвращаемся в состояние патрулирования после завершения периода оглушения
        }
        else
        {
            timer += Time.deltaTime;
        }
    }

    void PlaySFX(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region AIMethods
    private void PlayAttackAnimation(int attackIndex)
    {
        if (anim == null) return;

        string stateName = GetAttackStateName(attackIndex);
        if (HasState(anim, baseAnimLayer, stateName))
            PlayState(stateName, baseAnimLayer);
    }

    private string GetAttackStateName(int attackIndex)
    {
        switch (attackIndex)
        {
            case 1: return "Attack2";
            case 2: return "Attack3";
            default: return "Attack";
        }
    }

    // Проверка существования состояния в Animator'е
    private bool HasState(Animator animator, int layerIndex, string stateName)
    {
        if (animator == null) return false;

        // Получаем текущее состояние на заданном слое
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(layerIndex);

        // Проверяем совпадение имен состояний
        return currentState.IsName(stateName);
    }

    // Воспроизведение состояния на заданном слое
    private void PlayState(string stateName, int layerIndex)
    {
        if (anim == null) return;
        anim.Play(stateName, layerIndex);
    }
    #endregion

    #region Collision Handling
    void OnCollisionEnter(Collision collision)
    {
        PlayerHealth playerHealth = collision.collider.GetComponent<PlayerHealth>();
        if (playerHealth != null && state == EnemyState.Attack)
        {
            playerHealth.TakeEnemyHit(); // Наносим урон только при контакте и в режиме атаки
        }
    }
    #endregion

    #region Visualization
    void OnDrawGizmosSelected()
    {
        Color colorYellow = Color.yellow;
        colorYellow.a = 0.15f;
        Gizmos.color = colorYellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Color colorRed = Color.red;
        colorRed.a = 0.15f;
        Gizmos.color = colorRed;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * viewDistance);
    }
    #endregion
}