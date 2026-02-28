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
    public float moveSpeed = 2.6f; // Forward-backward speed
    [SerializeField] private float aimMoveSpeed = 1.25f;
    [Header("Movement Feel")]
    [SerializeField] private float acceleration = 8.5f;
    [SerializeField] private float deceleration = 11.5f;
    [SerializeField] private float aimAcceleration = 6.5f;
    [SerializeField] private float aimDeceleration = 13.5f;
    [SerializeField] private float movingVelocityThreshold = 0.08f;
    [Header("Rigidbody Setup")]
    [SerializeField] private bool autoConfigureRigidbody = true;
    [SerializeField] private float rigidbodyMass = 75f;
#if UNITY_6000_0_OR_NEWER
    [SerializeField] private float rigidbodyLinearDamping = 4f;
#else
    [SerializeField] private float rigidbodyDrag = 4f;
#endif
    public float rotateSpeed = 10f; // Rotation damping
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [Header("Aim Feel")]
    [SerializeField] private float aimRotationSmoothTime = 0.07f;
    [SerializeField, Range(0.01f, 0.5f)] private float aimInputDeadZone = 0.12f;
    [SerializeField, Range(0.01f, 0.5f)] private float aimAxisDominanceBias = 0.2f;
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
    private bool wasMoveInput;
    private float rotationVelocity;
    private Vector2 lastMoveDirection;
    private Vector3 aimForward = Vector3.forward;
    private Vector3 currentPlanarVelocity;
    private bool animationLockActive;
    private string lockedAnimationState;

    public float CurrentPlanarSpeed => new Vector2(currentPlanarVelocity.x, currentPlanarVelocity.z).magnitude;
    public bool IsAnimationLocked => animationLockActive;

    private const string GameAnimatorControllerPath = "Assets/Animate/Phylanc/Player_GameScene";

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();

        ConfigureRigidbody();

        lastMoveTime = Time.time;
        currentPlanarVelocity = Vector3.zero;
        ApplyAnimatorControllerForScene();
    }

    void Update()
    {
        if (animationLockActive)
        {
            inputHorizontal = 0f;
            inputVertical = 0f;
            SetBlendVelocity(Vector2.zero);

            if (!string.IsNullOrEmpty(lockedAnimationState))
                TryChangeAnimation(lockedAnimationState);

            return;
        }

        bool hasActiveWeapon = HasActiveWeaponSelected();
        isAiming = hasActiveWeapon && Input.GetMouseButton(1);
        ProcessMovement();
        bool isMoving = IsCharacterMoving();
        if (isAiming)
            RotateByMouse();
        else if (isMoving)
            RotateByMovement(new Vector2(currentPlanarVelocity.x, currentPlanarVelocity.z));
        else
            RotateByMouse();
        wasAiming = isAiming;
    }

    private void FixedUpdate()
    {
        if (animationLockActive)
        {
            currentPlanarVelocity = Vector3.zero;
            return;
        }

        ApplyMovement();
    }

    public void SetAnimationLock(bool isLocked, string animationState = null)
    {
        animationLockActive = isLocked;
        lockedAnimationState = isLocked ? animationState : null;

        if (isLocked)
        {
            inputHorizontal = 0f;
            inputVertical = 0f;
            currentPlanarVelocity = Vector3.zero;
            idleActive = false;
            idleSwitchScheduled = false;
            SetBlendVelocity(Vector2.zero);

            if (!string.IsNullOrEmpty(lockedAnimationState))
                TryChangeAnimation(lockedAnimationState);

            return;
        }

        lastMoveTime = Time.time;
        idleActive = false;
        idleSwitchScheduled = false;
        SetBlendVelocity(Vector2.zero);
    }

    void ProcessMovement()
    {
        inputHorizontal = Input.GetAxisRaw("Horizontal");
        inputVertical = Input.GetAxisRaw("Vertical");

        bool hasMoveInput = Mathf.Abs(inputHorizontal) > 0.1f || Mathf.Abs(inputVertical) > 0.1f;

        if (hasMoveInput)
        {
            lastMoveTime = Time.time;
            idleActive = false;
            idleSwitchScheduled = false;
            lastMoveDirection = new Vector2(inputHorizontal, inputVertical);
        }
        else if (wasMoveInput)
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

        wasMoveInput = hasMoveInput;

        Vector2 movementForAnimation = isAiming
            ? GetAimRelativeInput(new Vector2(inputHorizontal, inputVertical))
            : new Vector2(currentPlanarVelocity.x, currentPlanarVelocity.z);

        CheckAnimation(movementForAnimation, isAiming);
    }

    void ApplyMovement()
    {
        Vector3 input = new Vector3(inputHorizontal, 0f, inputVertical);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector3 movement = input;

        float targetSpeed = isAiming ? aimMoveSpeed : moveSpeed;
        Vector3 targetVelocity = movement * targetSpeed;

        bool hasMovementInput = input.sqrMagnitude > 0.001f;
        float accel = isAiming ? aimAcceleration : acceleration;
        float decel = isAiming ? aimDeceleration : deceleration;
        float velocityChange = (hasMovementInput ? accel : decel) * Time.fixedDeltaTime;

        currentPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, targetVelocity, Mathf.Max(0f, velocityChange));
        rb.MovePosition(rb.position + currentPlanarVelocity * Time.fixedDeltaTime);
    }

    private bool IsCharacterMoving()
    {
        return currentPlanarVelocity.sqrMagnitude > movingVelocityThreshold * movingVelocityThreshold;
    }

        private void ConfigureRigidbody()
        {
        if (!autoConfigureRigidbody || rb == null) return;

        rb.mass = Mathf.Max(1f, rigidbodyMass);
    #if UNITY_6000_0_OR_NEWER
        rb.linearDamping = Mathf.Max(0f, rigidbodyLinearDamping);
    #else
        rb.drag = Mathf.Max(0f, rigidbodyDrag);
    #endif
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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
        float smoothTime = isAiming ? aimRotationSmoothTime : rotationSmoothTime;
        float smoothAngle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref rotationVelocity,
            Mathf.Max(0.001f, smoothTime),
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
            ChangeAnimation(baseIdleAnimation);
            SetBlendVelocity(Vector2.zero);
        }
        else
        {
            bool canPlayIdleVariants = Time.time >= lastMoveTime + Mathf.Max(0f, idleStartDelay);
            if (canPlayIdleVariants)
                CheckIdle();
            else
            {
                idleActive = false;
                idleSwitchScheduled = false;
                ChangeAnimation(baseIdleAnimation);
            }
            SetBlendVelocity(Vector2.zero);
        }

        if (!isAimMode && aiming)
            ChangeAnimation(baseIdleAnimation);
    }

    private Vector2 GetAimDirection(Vector2 movement)
    {
        float deadZone = Mathf.Clamp(aimInputDeadZone, 0.01f, 0.5f);
        if (movement.sqrMagnitude < deadZone * deadZone)
            return Vector2.zero;

        Vector2 normalized = movement.normalized;
        float absX = Mathf.Abs(normalized.x);
        float absY = Mathf.Abs(normalized.y);
        float dominance = Mathf.Clamp(aimAxisDominanceBias, 0.01f, 0.5f);

        if (Mathf.Abs(absX - absY) > dominance)
        {
            if (absX > absY)
                return new Vector2(Mathf.Sign(normalized.x), 0f);

            return new Vector2(0f, Mathf.Sign(normalized.y));
        }

        float snappedX = Mathf.Sign(normalized.x);
        float snappedY = Mathf.Sign(normalized.y);
        return new Vector2(snappedX, snappedY);
    }

    private Vector2 GetAimRelativeInput(Vector2 worldInput)
    {
        if (worldInput.sqrMagnitude > 1f)
            worldInput.Normalize();

        Vector3 worldMove = new Vector3(worldInput.x, 0f, worldInput.y);
        if (worldMove.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        Vector3 flatAimForward = new Vector3(aimForward.x, 0f, aimForward.z);
        if (flatAimForward.sqrMagnitude < 0.0001f)
            flatAimForward = transform.forward;

        flatAimForward.Normalize();
        Vector3 aimRight = Vector3.Cross(Vector3.up, flatAimForward).normalized;
        float localX = Vector3.Dot(worldMove, aimRight);
        float localY = Vector3.Dot(worldMove, flatAimForward);

        return new Vector2(localX, localY);
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

        float speed01 = Mathf.Clamp01(new Vector2(currentPlanarVelocity.x, currentPlanarVelocity.z).magnitude / Mathf.Max(0.01f, moveSpeed));
        direction *= speed01;

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
