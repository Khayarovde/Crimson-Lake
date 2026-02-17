using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TankController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator; // Animator component
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private float animationTransition = 0.18f;
    [SerializeField] private float blendParameterDampTime = 0.12f;
    [SerializeField] private string blendTreeWalkState = "Blend Tree_WALK"; // Blend Tree для движения
    [SerializeField] private string blendTreeAimWalkState = "Blend Tree_AIM_WALK";
    [SerializeField] private string blendTreeXParam = "v";
    [SerializeField] private string blendTreeYParam = "h";
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
    [SerializeField] private float aimMoveSpeed = 1.5f;
    public float rotateSpeed = 10f; // Rotation damping
    [SerializeField] private float rotationSmoothTime = 0.12f;
    private Rigidbody rb;
    [SerializeField] private PlayerInventory playerInventory;
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
    private float rotationVelocity;
    private Vector2 lastMoveDirection;
    private Vector3 aimForward = Vector3.forward;

    private const string GameAnimatorControllerPath = "Assets/Animate/Phylanc/Player_GameScene";

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();

        lastMoveTime = Time.time;
        ApplyAnimatorControllerForScene();
    }

    void Update()
    {
        bool hasActiveWeapon = HasActiveWeaponSelected();
        isAiming = hasActiveWeapon && Input.GetMouseButton(1);
        ProcessMovement();
        bool isMoving = Mathf.Abs(inputHorizontal) > 0.1f || Mathf.Abs(inputVertical) > 0.1f;
        if (isAiming)
            RotateByMouse();
        else if (isMoving)
            RotateByMovement(lastMoveDirection);
        else
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
            lastMoveDirection = new Vector2(inputHorizontal, inputVertical);
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
        Vector3 input = new Vector3(inputHorizontal, 0f, inputVertical);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector3 movement = input;
        if (isAiming)
        {
            Vector3 aimRight = Vector3.Cross(Vector3.up, aimForward).normalized;
            movement = aimRight * inputHorizontal + aimForward * inputVertical;
            movement.y = 0f;
        }

        float currentSpeed = isAiming ? aimMoveSpeed : moveSpeed;
        rb.MovePosition(rb.position + movement * currentSpeed * Time.fixedDeltaTime);
    }

    void RotateByMovement(Vector2 movement)
    {
        if (movement.sqrMagnitude < 0.001f) return;
        float targetAngle = Mathf.Atan2(movement.x, movement.y) * Mathf.Rad2Deg;
        float smoothAngle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref rotationVelocity,
            rotationSmoothTime,
            Mathf.Max(1f, rotateSpeed)
        );

        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
    }

    void RotateByMouse()
    {
        Camera cameraMain = Camera.main;
        if (cameraMain == null) return;

        Ray mouseRay = cameraMain.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!groundPlane.Raycast(mouseRay, out float hitDistance)) return;

        Vector3 targetPoint = mouseRay.GetPoint(hitDistance);
        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        aimForward = direction.normalized;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float smoothAngle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref rotationVelocity,
            rotationSmoothTime,
            Mathf.Max(1f, rotateSpeed)
        );

        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);
    }

    private void CheckAnimation(Vector2 movement, bool aiming)
    {
        if (animator == null) return;
        bool hasActiveWeapon = HasActiveWeaponSelected();
        bool isAimMode = hasActiveWeapon && aiming;

        if (movement.sqrMagnitude > 0.01f)
        {
            if (isAimMode)
            {
                Vector2 direction = GetAimDirection(movement);
                ChangeMovementAnimation(true);
                SetBlendVelocity(direction);
            }
            else
            {
                Vector2 direction = GetStableDirection(movement);
                ChangeMovementAnimation(false);
                SetBlendVelocity(direction);
            }
            return;
        }

        if (isAimMode)
        {
            ChangeMovementAnimation(true);
            SetBlendVelocity(Vector2.zero);
        }
        else
        {
            SetBlendVelocity(Vector2.zero);
        }

        if (!isAimMode)
        {
            if (aiming)
                ChangeAnimation(baseIdleAnimation);
            else if (Time.time - lastMoveTime >= Mathf.Max(0f, idleStartDelay))
                CheckIdle();
        }
    }

    private Vector2 GetAimDirection(Vector2 movement)
    {
        float horizontal = Mathf.Abs(movement.x) > 0.1f ? Mathf.Sign(movement.x) : 0f;
        float vertical = Mathf.Abs(movement.y) > 0.1f ? Mathf.Sign(movement.y) : 0f;

        Vector2 direction = new Vector2(horizontal, vertical);

        return direction;
    }

    private void SetBlendVelocity(Vector2 direction)
    {
        if (animator == null) return;
        animator.SetFloat(blendTreeXParam, direction.x, Mathf.Max(0f, blendParameterDampTime), Time.deltaTime);
        animator.SetFloat(blendTreeYParam, direction.y, Mathf.Max(0f, blendParameterDampTime), Time.deltaTime);
    }

    private void ChangeMovementAnimation(bool isAimMode)
    {
        ChangeAnimation(isAimMode ? blendTreeAimWalkState : blendTreeWalkState);
    }


    private Vector2 GetStableDirection(Vector2 movement)
    {
        Vector2 direction = movement.normalized;
        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        if (Mathf.Abs(absX - absY) < 0.1f && lastMoveDirection.sqrMagnitude > 0.01f)
            direction = lastMoveDirection.normalized;
        return direction;
    }

    private bool HasActiveWeaponSelected()
    {
        if (playerInventory == null || playerInventory.inventoryData == null) return false;
        int index = playerInventory.activeItemIndex;
        if (index < 0) return false;

        var slots = playerInventory.inventoryData.GetSlots();
        if (index >= slots.Count) return false;

        var item = slots[index];
        if (item == null) return false;

        return item.type == InventoryItem.ItemType.Gun || item.type == InventoryItem.ItemType.Pistol;
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
        TryChangeAnimation(stateName);
    }

    private bool TryChangeAnimation(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return false;
        if (stateName == currentState) return true;
        if (!HasState(stateName)) return false;

        animator.CrossFadeInFixedTime(stateName, animationTransition, 0);
        currentState = stateName;
        return true;
    }



    private bool HasState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;
        return animator.HasState(0, Animator.StringToHash(stateName));
    }

    private void ApplyAnimatorControllerForScene()
    {
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        if (sceneIndex == 0)
            return;

        ApplyAnimatorController(animatorController);
    }

    private void ApplyAnimatorController(RuntimeAnimatorController controller)
    {
        if (animator == null) return;
        if (controller == null) return;
        if (animator.runtimeAnimatorController == controller) return;

        animator.runtimeAnimatorController = controller;
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
            animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GameAnimatorControllerPath);

        ApplyAnimatorControllerForScene();
    }
#endif
}
