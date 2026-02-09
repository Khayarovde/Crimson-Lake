using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TankController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator; // Animator component
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private float animationTransition = 0.1f;
    [SerializeField] private string moveUpAnimation = "Walk_Up";
    [SerializeField] private string moveDownAnimation = "Walk_Down";
    [SerializeField] private string moveRightAnimation = "Walk_Right";
    [SerializeField] private string moveLeftAnimation = "Walk_Left";
    [SerializeField] private string aimUpAnimation = "Aim_Walk_Up";
    [SerializeField] private string aimDownAnimation = "Aim_Walk_Down";
    [SerializeField] private string aimIdleAnimation = "Aim_Idle";
    [SerializeField] private string baseIdleAnimation = "Idle";
    [SerializeField] private string idleAnimation0 = "Idle_0";
    [SerializeField] private string idleAnimation1 = "Idle_1";
    [SerializeField] private string idleAnimation2 = "Idle_2";
    [SerializeField] private string idleAnimation3 = "Idle_3";
    [SerializeField] private string idleAnimation4 = "Idle_4";
    [SerializeField, Range(0, 4)] private int currentIdle = 0;
    [SerializeField] private float idleSwitchInterval = 3f;
    [SerializeField] private bool cycleIdles = true;
    [SerializeField] private bool randomizeIdles = true;
    [SerializeField] private float idleStartDelay = 10f;
    [SerializeField] private float idleSwitchDelayAfterComplete = 2f;
    [SerializeField] private string hitAnimation = "Hit";
    [SerializeField] private string gameoverAnimation = "gameover_player";
    public float moveSpeed = 3f; // Forward-backward speed
    public float rotateSpeed = 10f; // Rotation speed
    private Rigidbody rb;
    private float inputHorizontal;
    private float inputVertical;
    private string currentState;
    private float nextIdleSwitchTime;
    private bool isAiming;
    private bool wasAiming;
    private float lastMoveTime;
    private bool idleActive;
    private bool idleSwitchScheduled;
    private bool wasMoving;

    private const string DefaultAnimatorControllerPath = "Assets/Blink/Art/Characters/Stylized/Demo_Characters/StylizedHumanAnimator.controller";

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null)
            animator = GetComponent<Animator>();

        lastMoveTime = Time.time;
        ApplyAnimatorController();
    }

    void Update()
    {
        isAiming = Input.GetMouseButton(1);
        ProcessMovement();
        RotateByMouse();
        wasAiming = isAiming;
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    void ProcessMovement()
    {
        // Берём только ось Vertical (W/S)
        inputHorizontal = Input.GetAxisRaw("Horizontal");
        inputVertical = Input.GetAxisRaw("Vertical");

        bool isMoving = Mathf.Abs(inputHorizontal) > 0.1f || Mathf.Abs(inputVertical) > 0.1f;

        if (isMoving)
        {
            lastMoveTime = Time.time;
            idleActive = false;
            idleSwitchScheduled = false;
        }
        else if (wasMoving)
        {
            lastMoveTime = Time.time;
            idleActive = false;
            idleSwitchScheduled = false;
            ChangeAnimation(baseIdleAnimation);
        }
        else if (!isAiming && wasAiming)
        {
            lastMoveTime = Time.time;
            idleActive = false;
            idleSwitchScheduled = false;
            ChangeAnimation(baseIdleAnimation);
        }

        wasMoving = isMoving;

        CheckAnimation(new Vector2(inputHorizontal, inputVertical), isAiming);
    }

    void ApplyMovement()
    {
        // Ось Vertical определяет направление движения (вперёд/назад)
        float vertical = inputVertical;

        // Строго двигаемся по направлению вперёд или назад
        Vector3 movement = transform.forward * vertical;

        // Двигаем тело персонажа
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    void RotateByMouse()
    {
        // Получаем позицию мыши на экране
        Vector3 screenPosition = Input.mousePosition;

        // Пересчитываем позицию мыши в локальные координаты
        Vector3 localMousePosition = new Vector3(screenPosition.x - Screen.width / 2f, 0, screenPosition.y - Screen.height / 2f);

        // Получаем направление из центрального положения в сторону мыши
        Vector3 direction = localMousePosition.normalized;

        // Формируем целевое вращение
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

        // Поворачиваем игрока плавно
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
    }

    private void CheckAnimation(Vector2 movement, bool aiming)
    {
        if (animator == null) return;

        if (movement.y > 0.1f)
            ChangeAnimation(aiming ? aimUpAnimation : moveUpAnimation);
        else if (movement.y < -0.1f)
            ChangeAnimation(aiming ? aimDownAnimation : moveDownAnimation);
        else if (movement.x > 0.1f)
            ChangeAnimation(moveRightAnimation);
        else if (movement.x < -0.1f)
            ChangeAnimation(moveLeftAnimation);
        else if (aiming)
            ChangeAnimation(aimIdleAnimation);
        else if (Time.time - lastMoveTime >= Mathf.Max(0f, idleStartDelay))
            CheckIdle();
    }

    private void CheckIdle()
    {
        if (!idleActive)
        {
            idleActive = true;
            SelectRandomIdle();
            nextIdleSwitchTime = Time.time + Mathf.Max(0.1f, idleSwitchInterval);
            idleSwitchScheduled = false;
        }
        else if (cycleIdles && CanAdvanceIdle() && !idleSwitchScheduled)
        {
            nextIdleSwitchTime = Time.time + Mathf.Max(0.1f, idleSwitchDelayAfterComplete);
            idleSwitchScheduled = true;
        }
        else if (cycleIdles && idleSwitchScheduled && Time.time >= nextIdleSwitchTime)
        {
            AdvanceIdle();
            idleSwitchScheduled = false;
        }

        switch (currentIdle)
        {
            case 0:
                ChangeAnimation(idleAnimation0);
                break;
            case 1:
                ChangeAnimation(idleAnimation1);
                break;
            case 2:
                ChangeAnimation(idleAnimation2);
                break;
            case 3:
                ChangeAnimation(idleAnimation3);
                break;
            case 4:
                ChangeAnimation(idleAnimation4);
                break;
        }
    }

    private void AdvanceIdle()
    {
        nextIdleSwitchTime = Time.time + Mathf.Max(0.1f, idleSwitchInterval);
        if (randomizeIdles)
        {
            SelectRandomIdle();
            return;
        }

        currentIdle = (currentIdle + 1) % 5;
    }

    private void SelectRandomIdle()
    {
        int next = Random.Range(0, 5);
        if (next == currentIdle)
            next = (next + 1) % 5;
        currentIdle = next;
    }

    private bool CanAdvanceIdle()
    {
        if (animator == null) return false;
        if (animator.IsInTransition(0)) return false;

        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (!IsIdleState(currentState)) return false;

        return info.normalizedTime >= 1f;
    }

    private bool IsIdleState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return false;
        return stateName == idleAnimation0 ||
               stateName == idleAnimation1 ||
               stateName == idleAnimation2 ||
               stateName == idleAnimation3 ||
               stateName == idleAnimation4;
    }

    private void ChangeAnimation(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return;
        if (stateName == currentState) return;
        if (!HasState(stateName)) return;

        animator.CrossFadeInFixedTime(stateName, animationTransition, 0);
        currentState = stateName;
    }

    private bool HasState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;
        return animator.HasState(0, Animator.StringToHash(stateName));
    }

    private void ApplyAnimatorController()
    {
        if (animator == null) return;
        if (animatorController == null) return;
        if (animator.runtimeAnimatorController == animatorController) return;

        animator.runtimeAnimatorController = animatorController;
    }

    public void PlayHit()
    {
        if (animator == null) return;
        if (!HasState(hitAnimation)) return;
        ChangeAnimation(hitAnimation);
    }

    public void PlayGameOver()
    {
        if (animator == null) return;
        if (!HasState(gameoverAnimation)) return;
        ChangeAnimation(gameoverAnimation);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (animatorController == null)
        {
            animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DefaultAnimatorControllerPath);
        }

        ApplyAnimatorController();
    }
#endif
}
