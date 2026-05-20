using UnityEngine;
using UnityEngine.SceneManagement;

public class TankController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.6f;
    [SerializeField] private float runMoveSpeed = 4.2f;
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
    public float rotateSpeed = 10f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float extraGravityAcceleration = 60f;

    [Header("Aim Feel")]
    [SerializeField] private float aimRotationSmoothTime = 0.07f;
    [SerializeField] private bool strictCursorAimRotation = true;
    [SerializeField] private bool updateDrivenAimRotation = false;
    [SerializeField] private float updateAimTurnSpeed = 1440f;
    [SerializeField] private float aimSnapAngle = 0.35f;
    [SerializeField] private bool preferCursorPlaneForRotation = true;
    [SerializeField] private float rotationCursorPlaneHeightOffset = 0f;
    [SerializeField] private float aimYawOffsetDegrees = 0f;
    [SerializeField] private float minTurnSpeed = 240f;
    [SerializeField] private float minAimTurnSpeed = 720f;
    [SerializeField] private float cursorAimMaxDistance = 300f;
    [SerializeField] private LayerMask cursorAimMask = ~0;
    [SerializeField] private bool mouseRotationEnabled = true;
    [SerializeField] private bool cameraRelativeMovement = true;

    [SerializeField] private PlayerInventory playerInventory;

    private Rigidbody rb;
    public float inputHorizontal;
    public float inputVertical;
    private float rotationVelocity;
    private Vector3 desiredMoveDirectionWorld;
    private Vector3 currentPlanarVelocity;
    private Vector3 aimForward = Vector3.forward;
    private Quaternion targetRotation;
    private bool hasRotationTarget;
    private Camera cachedMainCamera;

    private bool isAiming;
    private bool isRunning;
    private float moveInputMagnitude;
    private bool movementLocked;

    // Public read-only state (used by PlayerAnimationCon)
    public float CurrentPlanarSpeed => new Vector2(currentPlanarVelocity.x, currentPlanarVelocity.z).magnitude;
    public bool IsAiming => isAiming;
    public bool IsRunning => isRunning;
    public float MoveInputMagnitude => moveInputMagnitude;
    public Vector3 AimForward => aimForward;
    public Vector3 CurrentPlanarVelocity => currentPlanarVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (playerInventory == null)
            playerInventory = GetComponent<PlayerInventory>();

        ConfigureRigidbody();

        currentPlanarVelocity = Vector3.zero;
        targetRotation = rb != null ? rb.rotation : transform.rotation;
        hasRotationTarget = true;
    }

    private Camera GetMainCameraCached()
    {
        if (cachedMainCamera != null) return cachedMainCamera;
        cachedMainCamera = Camera.main;
        return cachedMainCamera;
    }

    /// <summary>Блокирует/разблокирует движение (вызывается из PlayerAnimationCon).</summary>
    public void SetMovementLock(bool locked)
    {
        movementLocked = locked;
        if (locked)
        {
            desiredMoveDirectionWorld = Vector3.zero;
            currentPlanarVelocity = Vector3.zero;
        }
    }

    void Update()
    {
        if (movementLocked) return;

        bool hasActiveWeapon = HasActiveWeaponSelected();
        isAiming = hasActiveWeapon && Input.GetMouseButton(1);

        ProcessMovement();

        bool isMoving = IsCharacterMoving();
        if (mouseRotationEnabled)
        {
            if (isAiming)
                UpdateRotationTargetByMouse();
            else if (isMoving)
                UpdateRotationTargetByMovement(new Vector2(currentPlanarVelocity.x, currentPlanarVelocity.z));
            else
                UpdateRotationTargetByMouse();
        }
        else if (isMoving)
        {
            UpdateRotationTargetByMovement(new Vector2(currentPlanarVelocity.x, currentPlanarVelocity.z));
        }

        ApplyUpdateAimRotation();
    }

    private void FixedUpdate()
    {
        if (movementLocked) return;
        ApplyMovement();
        ApplyRotation();
        ApplyExtraGravity();
    }

    private void ApplyExtraGravity()
    {
        if (rb == null || extraGravityAcceleration <= 0f) return;
        if (rb.linearVelocity.y > -0.01f) return;
        rb.AddForce(Vector3.down * extraGravityAcceleration, ForceMode.Acceleration);
    }

    private void ApplyUpdateAimRotation()
    {
        if (!updateDrivenAimRotation || !isAiming || !strictCursorAimRotation || rb == null || !hasRotationTarget)
            return;

        float step = Mathf.Max(180f, updateAimTurnSpeed) * Time.deltaTime;
        Quaternion nextRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, step);

        if (Quaternion.Angle(nextRotation, targetRotation) <= Mathf.Max(0.01f, aimSnapAngle))
            nextRotation = targetRotation;

        rb.rotation = nextRotation;
    }

    public void SetMouseRotationEnabled(bool isEnabled)
    {
        mouseRotationEnabled = isEnabled;
    }

    void ProcessMovement()
    {
        inputHorizontal = Input.GetAxisRaw("Horizontal");
        inputVertical = Input.GetAxisRaw("Vertical");

        desiredMoveDirectionWorld = GetMovementDirectionWorld(inputHorizontal, inputVertical);

        bool hasMoveInput = Mathf.Abs(inputHorizontal) > 0.1f || Mathf.Abs(inputVertical) > 0.1f;
        moveInputMagnitude = Mathf.Clamp01(new Vector2(inputHorizontal, inputVertical).magnitude);

        bool sprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isRunning = hasMoveInput && sprintHeld && !Input.GetMouseButton(1);
    }

    void ApplyMovement()
    {
        Vector3 input = desiredMoveDirectionWorld;
        if (input.sqrMagnitude > 1f) input.Normalize();

        float targetSpeed = isAiming ? aimMoveSpeed : (isRunning ? runMoveSpeed : moveSpeed);
        Vector3 targetVelocity = input * targetSpeed;

        bool hasMovementInput = input.sqrMagnitude > 0.001f;
        float accel = isAiming ? aimAcceleration : acceleration;
        float decel = isAiming ? aimDeceleration : deceleration;
        float velocityChange = (hasMovementInput ? accel : decel) * Time.fixedDeltaTime;

        currentPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, targetVelocity, Mathf.Max(0f, velocityChange));
        rb.MovePosition(rb.position + currentPlanarVelocity * Time.fixedDeltaTime);
    }

    private Vector3 GetMovementDirectionWorld(float horizontal, float vertical)
    {
        Vector3 rawInput = new Vector3(horizontal, 0f, vertical);
        if (rawInput.sqrMagnitude < 0.0001f) return Vector3.zero;

        if (!cameraRelativeMovement) return rawInput.normalized;

        Camera mainCamera = GetMainCameraCached();
        if (mainCamera == null) return rawInput.normalized;

        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camRight = mainCamera.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;

        if (camForward.sqrMagnitude < 0.0001f || camRight.sqrMagnitude < 0.0001f)
            return rawInput.normalized;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 worldDirection = camRight * horizontal + camForward * vertical;
        return worldDirection.sqrMagnitude < 0.0001f ? Vector3.zero : worldDirection.normalized;
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

    void UpdateRotationTargetByMovement(Vector2 movement)
    {
        if (movement.sqrMagnitude < 0.001f) return;
        float targetAngle = Mathf.Atan2(movement.x, movement.y) * Mathf.Rad2Deg;
        targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
        hasRotationTarget = true;
    }

    void UpdateRotationTargetByMouse()
    {
        Camera cameraMain = GetMainCameraCached();
        if (cameraMain == null) return;

        Ray mouseRay = cameraMain.ScreenPointToRay(Input.mousePosition);
        Vector3 targetPoint = Vector3.zero;
        bool hitFound = false;

        if (preferCursorPlaneForRotation)
        {
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + rotationCursorPlaneHeightOffset, 0f));
            if (groundPlane.Raycast(mouseRay, out float hitDistance))
            {
                targetPoint = mouseRay.GetPoint(hitDistance);
                hitFound = true;
            }
        }

        if (!hitFound)
        {
            if (Physics.Raycast(mouseRay, out RaycastHit hit, cursorAimMaxDistance, cursorAimMask))
            {
                targetPoint = hit.point;
                hitFound = true;
            }
            else
            {
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y + rotationCursorPlaneHeightOffset, 0f));
                if (!groundPlane.Raycast(mouseRay, out float hitDistance)) return;
                targetPoint = mouseRay.GetPoint(hitDistance);
            }
        }

        Vector3 direction = targetPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        aimForward = direction.normalized;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        targetAngle += aimYawOffsetDegrees;
        targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
        hasRotationTarget = true;
    }

    private void ApplyRotation()
    {
        if (!hasRotationTarget || rb == null) return;

        if (isAiming && strictCursorAimRotation)
        {
            rotationVelocity = 0f;
            float step = Mathf.Max(1f, minAimTurnSpeed) * Time.fixedDeltaTime;
            Quaternion next = Quaternion.RotateTowards(rb.rotation, targetRotation, step);
            if (Quaternion.Angle(next, targetRotation) <= Mathf.Max(0.01f, aimSnapAngle))
                next = targetRotation;
            rb.MoveRotation(next);
            return;
        }

        float smoothTime = isAiming ? aimRotationSmoothTime : rotationSmoothTime;
        float currentAngle = rb.rotation.eulerAngles.y;
        float targetAngle = targetRotation.eulerAngles.y;
        float maxTurnSpeed = Mathf.Max(Mathf.Max(1f, rotateSpeed), isAiming ? Mathf.Max(1f, minAimTurnSpeed) : Mathf.Max(1f, minTurnSpeed));

        float smoothedAngle = Mathf.SmoothDampAngle(
            currentAngle, targetAngle, ref rotationVelocity,
            Mathf.Max(0.001f, smoothTime), maxTurnSpeed, Time.fixedDeltaTime
        );

        rb.MoveRotation(Quaternion.Euler(0f, smoothedAngle, 0f));
    }

    private bool HasActiveWeaponSelected()
    {
        if (playerInventory == null || playerInventory.inventoryData == null) return false;
        int index = playerInventory.activeItemIndex;
        if (index < 0) return false;

        if (index >= playerInventory.inventoryData.GetSlotCount()) return false;

        var item = playerInventory.inventoryData.GetItemAt(index);
        if (item == null) return false;

        return item.type == InventoryItem.ItemType.Gun || item.type == InventoryItem.ItemType.Pistol;
    }
}