using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic; 
using TheDeveloperTrain.SciFiGuns;

public class WeaponHandler : MonoBehaviour
{
    public static event System.Action<Vector3, float> PlayerShotFired;
    public bool IsAiming => isAiming;

    private sealed class FinisherTarget
    {
        public AdvancedEnemyAI advancedEnemy;
        public Enemytest enemyTest;
        public BossEnemy bossEnemy;

        public Transform Transform => advancedEnemy?.transform ?? enemyTest?.transform ?? bossEnemy?.transform;

        public bool CanBeFinished() => 
            advancedEnemy?.CanBeFinished() ?? enemyTest?.CanBeFinished() ?? false;

        public void KillDuringStun()
        {
            advancedEnemy?.KillDuringStun();
            enemyTest?.KillDuringStun();
            
        }
    }

    [System.Serializable]
    private class WeaponGlowProfile
    {
        [Tooltip("Индекс glow-материала в Renderer.materials")]
        public int materialIndex = 0;
        public float glowBaseIntensity = 0.2f;
        public float glowMaxIntensity = 2f;
        public AnimationCurve glowScaling = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public bool invertGlow = false;
    }

    [Header("=== Точки ===")]
    [SerializeField] private Transform weaponHoldPoint;
    [SerializeField] private Transform pistolHoldPoint;
    [SerializeField] private Transform shotgunHoldPoint;
    [SerializeField] private Transform defaultMuzzlePoint;

    [Header("=== Hold визуалы ===")]
    [SerializeField] private Transform pistolHoldVisualPoint;
    [SerializeField] private Transform gunHoldVisualPoint;
    [SerializeField] private GameObject pistolHoldVisualPrefab;
    [SerializeField] private GameObject gunHoldVisualPrefab;

    [Header("=== Модели оружия ===")]
    [SerializeField] private GameObject gunPrefab;
    [SerializeField] private Vector3 gunScale = Vector3.one;
    [SerializeField] private GameObject pistolPrefab;
    [SerializeField] private Vector3 pistolScale = Vector3.one;

    [Header("=== Звуки ===")]
    [SerializeField] public AudioSource audioSource;
    [SerializeField] public AudioClip emptyMagSound;
    [SerializeField, Tooltip("Минимальный интервал между звуками пустого магазина")]
    private float emptyMagSoundCooldown = 0.2f;
    [SerializeField, Tooltip("Громкость выстрела для системы слуха врагов")]
    private float pistolShotLoudness = 1f;
    [SerializeField, Tooltip("Громкость выстрела для системы слуха врагов")]
    private float gunShotLoudness = 1.35f;
    [Space(5)]
    [SerializeField, Tooltip("Звуки выстрела (Лазерная винтовка)")] private AudioClip[] gunShootSounds;
    [SerializeField, Tooltip("Звук перезарядки (Лазерная винтовка)")] private AudioClip gunReloadSound;
    [Space(5)]
    [SerializeField, Tooltip("Звуки выстрела (Пистолет)")] private AudioClip[] pistolShootSounds;
    [SerializeField, Tooltip("Звук перезарядки (Пистолет)")] private AudioClip pistolReloadSound;

    [Header("=== ЛАЗЕРНАЯ ВИНТОВКА ===")]
    [SerializeField] private GunStats shotgunStats;
    [SerializeField] private RecoilProfile shotgunRecoilProfile;
    [SerializeField] private WeaponGlowProfile shotgunGlowProfile = new WeaponGlowProfile();

    [Header("=== ПИСТОЛЕТ ===")]
    [SerializeField] private GunStats pistolStats;
    [SerializeField] private RecoilProfile pistolRecoilProfile;
    [SerializeField] private WeaponGlowProfile pistolGlowProfile = new WeaponGlowProfile();

    [Header("=== ВИЗУАЛЬНЫЕ ЭФФЕКТЫ ===")]
    [SerializeField] private GameObject gunTracerPrefab;
    [SerializeField] private float gunTracerDuration = 0.12f;
    [SerializeField, Tooltip("Толщина лазерного луча (Gun)")]
    private float gunTracerThickness = 0.5f;
    [SerializeField] private GameObject pistolTracerPrefab;
    [SerializeField] private float pistolTracerDuration = 0.1f;
    [SerializeField, Tooltip("Толщина трассировщика (Pistol)")]
    private float pistolTracerThickness = 0.08f;
    [SerializeField, Tooltip("Скорость полёта трассера (ед/с)")]
    private float tracerTravelSpeed = 400f;
    [SerializeField, Tooltip("Максимальная доля длины трассера от фактической дистанции")]
    private float tracerLengthFactor = 0.9f;
    [SerializeField, Tooltip("Сдвиг центра трассера ближе к дулу (м)")]
    private float tracerMuzzleOffset = 0.08f;
    [SerializeField, Tooltip("Максимальная длина луча, если не попал во что-то")]
    private float maxTracerDistance = 300f;
    [SerializeField, Tooltip("Стрелять строго по forward оружия (из дула), а не по направлению курсора")]
    private bool shootByMuzzleForward = true;
    [SerializeField, Tooltip("Базовый разброс одиночного выстрела вокруг forward дула (градусы)")]
    private float muzzleForwardSpread = 0.1f;
    [SerializeField] private GameObject gunHitEffect;
    [SerializeField] private GameObject pistolHitEffect;
    [SerializeField, Tooltip("Префаб вспышки выстрела (Gun)")]
    private GameObject gunMuzzleFlashPrefab;
    [SerializeField, Tooltip("Префаб вспышки выстрела (Pistol)")]
    private GameObject pistolMuzzleFlashPrefab;
    [SerializeField, Tooltip("Префаб света вспышки (Gun)")]
    private GameObject gunMuzzleLightPrefab;
    [SerializeField, Tooltip("Префаб света вспышки (Pistol)")]
    private GameObject pistolMuzzleLightPrefab;
    [SerializeField, Tooltip("Длительность вспышки (сек)")]
    private float muzzleFlashLifetime = 0.06f;

    [Header("=== Прицел и Aim Assist ===")]
    [SerializeField] private float aimWalkSpeed = 1.5f; 
    [SerializeField] private AimAssist aimAssist;
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField, Range(0f, 1f), Tooltip("Насколько сильно уменьшается разброс при полном удержании прицела на цели")]
    private float lockOnSpreadReduction = 0.85f;

    [Header("=== Анимация и melee ===")]
    [SerializeField] public Animator playerAnimator;
    [SerializeField] public string meleeTrigger = "MeleePush";
    [SerializeField] private string finisherAnimation = "attack_stun_enemy";
    [SerializeField] private string defaultIdleAnimation = "Idle";
    [SerializeField] private float finisherReturnToIdleDelay = 0.8f;
    [SerializeField, Tooltip("Таймаут ожидания входа в state добивания")]
    private float finisherEnterStateTimeout = 0.35f;
    [SerializeField, Tooltip("Максимальное ожидание окончания анимации добивания, чтобы не зависнуть при ошибочной конфигурации Animator")]
    private float finisherMaxWaitForAnimationEnd = 3f;
    [SerializeField] private float finisherRange = 1.4f;
    [SerializeField, Tooltip("Автоподшаг к позиции добивания перед запуском анимации")]
    private bool autoSnapToFinisherPosition = true;
    [SerializeField, Tooltip("Дистанция от врага для позиции добивания (впереди врага)")]
    private float finisherSnapDistance = 0.9f;
    [SerializeField, Tooltip("Длительность автоподшага перед добиванием")]
    private float finisherSnapDuration = 0.12f;
    [SerializeField, Tooltip("Минимальная дистанция автоподшага, если у врага тесно")]
    private float finisherSnapMinDistance = 0.45f;
    [SerializeField, Tooltip("Сколько вариантов точки проверять при тесном окружении")]
    private int finisherSnapPositionProbeSteps = 6;
    [SerializeField, Tooltip("Маска препятствий для безопасного автоподшага")]
    private LayerMask finisherSnapObstacleMask = ~0;
    [SerializeField, Tooltip("Запас по радиусу капсулы при проверке безопасной позиции")]
    private float finisherSnapClearancePadding = 0.03f;
    [SerializeField] private bool requireFrontForFinisher = true;
    [SerializeField, Range(1f, 179f)] private float finisherFrontMaxAngle = 85f;
    [SerializeField, Tooltip("Разрешать запуск добивания только при почти нулевой скорости игрока")]
    private bool requireLowSpeedForFinisher = true;
    [SerializeField, Tooltip("Порог скорости игрока для старта добивания")]
    private float finisherStartSpeedThreshold = 0.2f;
    [SerializeField, Tooltip("Запрещать прицеливание (ПКМ), если рядом есть оглушённый враг для добивания")]
    private bool blockRightMouseNearStunnedEnemy = true;
    [SerializeField, Range(0.05f, 0.5f), Tooltip("Порог нажатия RT для запуска добивания с геймпада")]
    private float finisherGamepadTriggerThreshold = 0.2f;
    [SerializeField, Tooltip("Задержка перед запуском смерти врага (сек)")]
    private float finisherEnemyDeathDelay = 0.6f;
    [SerializeField, Tooltip("Если строгие условия не прошли, автоматически ослаблять проверки (скорость), но не угол фронта")]
    private bool finisherAutoRelaxConstraints = true;
    [SerializeField, Tooltip("Дополнительный радиус поиска цели добивания при fallback")]
    private float finisherFallbackExtraRange = 0.7f;
    [SerializeField, Tooltip("Игнорировать enemyLayerMask при fallback-поиске цели добивания")]
    private bool finisherFallbackIgnoreLayerMask = true;
    [SerializeField, Tooltip("Запускать FinishingManager для переключения камеры и эффекта добивания")]
    private bool useFinisherManagerCamera = true;
    [SerializeField] private FinishingManager finisherManager;
    [SerializeField, Tooltip("Игнорировать проверку " + nameof(FinishingManager.requireFrontSide) + " при старте от WeaponHandler")]
    private bool ignoreFrontSideForFinisherManager = false;

    [Header("=== ОГЛУШЕНИЕ ВРАГОВ ===")]
    [SerializeField, Tooltip("Сколько попаданий из пистолета нужно для оглушения врага")]
    private int pistolHitsToStun = 12;

    [SerializeField, Tooltip("Сколько попаданий из лазерной винтовки нужно для оглушения врага")]
    private int gunHitsToStun = 3;

    [Header("=== DAMAGE TO Enemytest ===")]
    [SerializeField, Tooltip("Урон от пистолета по Enemytest")]
    private float pistolDamageToEnemytest = 20f;
    [SerializeField, Tooltip("Урон от выстрела лазерной винтовки по Enemytest")]
    private float gunDamageToEnemytest = 30f;

    [Header("=== ТОЧНОСТЬ GUN ===")]
    [SerializeField, Tooltip("Радиус SphereCast для Gun, чтобы попадания регистрировались стабильнее")]
    private float gunHitRadius = 0.18f;
    [SerializeField, Tooltip("Количество дробин у Gun (shotgun)")]
    private int gunPellets = 7;
    [SerializeField, Tooltip("Разброс дроби (в градусах)")]
    private float gunPelletSpread = 6f;

    // Runtime переменные
    private Transform muzzlePoint;
    private PlayerInventory playerInventory;
    private TankController tankController;
    private PlayerAnimationCon animCon;
    private float originalWalkSpeed = 5f;
    private bool isAiming = false;
    private bool isReloading = false;
    private Coroutine firingCoroutine;
    private InventoryItem.ItemType currentWeaponType = InventoryItem.ItemType.Empty;
    private GunStats currentWeaponStats;
    private RecoilProfile currentRecoilProfile;
    private WeaponGlowProfile currentGlowProfile;
    private Renderer currentWeaponRenderer;
    private Material currentGlowMaterial;
    private float currentShotInterval;
    private float currentShootDelay;
    private FireMode currentFireMode = FireMode.Single;
    private int currentBurstCount = 1;
    private float currentBurstInterval;
    private int currentMagazineSize;
    private int currentReserveAmmo;
    private int currentAmmoInMag;
    private float currentReloadTime;
    private AudioClip[] currentShootSounds;
    private AudioClip currentReloadSound;
    private bool isShotSequenceRunning;
    [SerializeField] private Animator m_Animator;
    private GameObject currentWeaponModel;
    private float nextFireTime = 0f;
    private Coroutine finisherReturnCoroutine;
    private Coroutine finisherKillCoroutine;
    private Coroutine finisherSequenceCoroutine;
    private bool isFinisherInProgress;
    private CapsuleCollider playerCapsule;
    private Coroutine recoilCoroutine;
    private Coroutine reloadGlowCoroutine;
    private Vector3 weaponModelBaseLocalPos;
    private Quaternion weaponModelBaseLocalRot;
    private Color currentGlowBaseColor = Color.white;
    private bool hasGlowBaseColor;
    private float nextEmptyMagSoundTime;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private readonly Dictionary<GameObject, Queue<GameObject>> muzzleFlashPool = new Dictionary<GameObject, Queue<GameObject>>();
    private WaitForSeconds muzzleFlashWait;
    // Словарь для отслеживания количества попаданий по каждому врагу
    private Dictionary<AdvancedEnemyAI, int> enemyHitCount = new Dictionary<AdvancedEnemyAI, int>();
    private GameObject holdVisualInstance;
    private InventoryItem.ItemType holdVisualType = InventoryItem.ItemType.Empty;
    private bool gamepadAimHeld;
    private bool gamepadFireHeld;
    private bool gamepadModeActive;
    private bool wasFinisherGamepadTriggerHeld;

    private void Awake()
    {
        // Если AudioSource назначен вручную — используем его
        if (audioSource == null)
        {
            // Иначе создаем автономный источник
            GameObject audioObj = new GameObject("WeaponAudioSourceFallback");
            audioObj.transform.SetParent(transform);
            audioObj.transform.localPosition = Vector3.zero;
            
            audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; 
            audioSource.playOnAwake = false;
        }

        m_Animator = GetComponent<Animator>();
        playerCapsule = GetComponent<CapsuleCollider>();
        playerInventory = GetComponent<PlayerInventory>();
        tankController = GetComponent<TankController>();
        animCon = GetComponent<PlayerAnimationCon>();
        if (tankController) originalWalkSpeed = tankController.moveSpeed;
        muzzlePoint = defaultMuzzlePoint;

        int startShotgunReserve = shotgunStats != null ? Mathf.Max(0, shotgunStats.totalAmmo) : 35;
        int startShotgunMag = shotgunStats != null ? Mathf.Max(1, shotgunStats.magazineSize) : 7;
        int startPistolReserve = pistolStats != null ? Mathf.Max(0, pistolStats.totalAmmo) : 120;
        int startPistolMag = pistolStats != null ? Mathf.Max(1, pistolStats.magazineSize) : 12;
        PlayerAmmoData.InitializeIfNeeded(startShotgunReserve, startShotgunMag, startPistolReserve, startPistolMag);

        if (aimAssist == null) aimAssist = gameObject.AddComponent<AimAssist>();
        aimAssist.Initialize(enemyLayerMask);
        muzzleFlashWait = new WaitForSeconds(Mathf.Max(0.01f, muzzleFlashLifetime));
    }

    private void Update()
    {
        if (TryFinisherAttack())
            return;

        if (isFinisherInProgress)
            return;

        HandleInput();
        if (Input.GetKeyDown(KeyCode.R)) TryManualReload();
        UpdateWeaponGlowVisual();
        UpdateHoldVisual();
    }

    private void HandleInput()
    {
        bool hasActiveWeapon = HasActiveWeaponSelected();
        bool blockWeaponUseNow = ShouldBlockAimingForFinisher() || IsEnemyTooCloseToShoot();
        bool aimHeld = gamepadModeActive ? gamepadAimHeld : (gamepadAimHeld || Input.GetMouseButton(1));
        bool fireHeld = gamepadModeActive ? gamepadFireHeld : (gamepadFireHeld || Input.GetMouseButton(0));
        bool aiming = hasActiveWeapon && !blockWeaponUseNow && aimHeld;

        if (blockWeaponUseNow && isAiming)
            StopAiming();

        if (aiming && !isAiming)
            StartAiming();
        else if (!aiming && isAiming)
            StopAiming();

        if (aiming && fireHeld && CanShoot() && firingCoroutine == null)
            firingCoroutine = StartCoroutine(ShootingRoutine());
    }

    public void SetGamepadAimHeld(bool isHeld)
    {
        gamepadAimHeld = isHeld;
    }

    public void SetGamepadFireHeld(bool isHeld)
    {
        gamepadFireHeld = isHeld;
    }

    public void SetGamepadModeActive(bool isActive)
    {
        gamepadModeActive = isActive;
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

    private bool TryFinisherAttack()
    {
        if (isFinisherInProgress) return false;

        bool mouseFinisherPressed = Input.GetMouseButtonDown(0);
        bool gamepadFinisherPressed = IsFinisherGamepadPressedThisFrame();
        if (!mouseFinisherPressed && !gamepadFinisherPressed) return false;

        bool allowLowSpeedCheck = requireLowSpeedForFinisher;
        bool allowFrontCheck = requireFrontForFinisher;

        FinisherTarget enemy = FindClosestStunnedEnemy(finisherRange, allowFrontCheck, false);
        if (enemy == null && finisherAutoRelaxConstraints)
        {
            allowLowSpeedCheck = false;
            allowFrontCheck = true;
            float relaxedRange = finisherRange + Mathf.Max(0f, finisherFallbackExtraRange);
            enemy = FindClosestStunnedEnemy(relaxedRange, allowFrontCheck, finisherFallbackIgnoreLayerMask);
        }

        if (enemy == null) return false;

        if (allowLowSpeedCheck && tankController != null && tankController.CurrentPlanarSpeed > Mathf.Max(0.01f, finisherStartSpeedThreshold))
            return false;

        if (isAiming)
            StopAiming();

        isFinisherInProgress = true;
        ApplyFinisherMovementLock();

        if (finisherReturnCoroutine != null)
            StopCoroutine(finisherReturnCoroutine);
        if (finisherKillCoroutine != null)
            StopCoroutine(finisherKillCoroutine);
        if (finisherSequenceCoroutine != null)
            StopCoroutine(finisherSequenceCoroutine);

        finisherSequenceCoroutine = StartCoroutine(BeginFinisherSequence(enemy));
        return true;
    }

    private bool IsFinisherGamepadPressedThisFrame()
    {
        Gamepad gamepad = Gamepad.current;
        bool triggerHeld = gamepadModeActive && gamepad != null && gamepad.rightTrigger.ReadValue() >= Mathf.Max(0.05f, finisherGamepadTriggerThreshold);
        bool pressedThisFrame = triggerHeld && !wasFinisherGamepadTriggerHeld;
        wasFinisherGamepadTriggerHeld = triggerHeld;
        return pressedThisFrame;
    }

    private IEnumerator BeginFinisherSequence(FinisherTarget enemy)
    {
        if (enemy == null || !enemy.CanBeFinished())
        {
            ReleaseFinisherAnimationLock();
            isFinisherInProgress = false;
            finisherSequenceCoroutine = null;
            yield break;
        }

        if (autoSnapToFinisherPosition)
            yield return SnapToFinisherPosition(enemy.Transform);

        if (enemy == null || !enemy.CanBeFinished())
        {
            ReleaseFinisherAnimationLock();
            isFinisherInProgress = false;
            finisherSequenceCoroutine = null;
            yield break;
        }

        FaceEnemy(enemy.Transform);
        TryStartFinisherManagerCamera(enemy.Transform);

        if (!PlayFinisherAnimation())
        {
            ReleaseFinisherAnimationLock();
            isFinisherInProgress = false;
            finisherSequenceCoroutine = null;
            yield break;
        }

        ApplyFinisherAnimationLock();
        finisherReturnCoroutine = StartCoroutine(ReturnToIdleAfterFinisher());
        finisherKillCoroutine = StartCoroutine(KillEnemyAfterFinisherAnimation(enemy));
        finisherSequenceCoroutine = null;
    }

    private void TryStartFinisherManagerCamera(Transform enemyTransform)
    {
        if (!useFinisherManagerCamera)
            return;
        if (enemyTransform == null)
            return;

        if (finisherManager == null)
            finisherManager = FindFirstObjectByType<FinishingManager>();

        if (finisherManager == null)
            return;
        if (finisherManager.IsFinishingActive)
            return;

        bool previousFrontRequirement = finisherManager.requireFrontSide;
        if (ignoreFrontSideForFinisherManager)
            finisherManager.requireFrontSide = false;

        finisherManager.StartFinishingImmediate(transform, enemyTransform);

        if (ignoreFrontSideForFinisherManager)
            finisherManager.requireFrontSide = previousFrontRequirement;
    }

    private IEnumerator SnapToFinisherPosition(Transform enemyTransform)
    {
        if (enemyTransform == null)
            yield break;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        TryGetSafeSnapTarget(enemyTransform, startPos, out Vector3 targetPos);

        Vector3 toEnemy = enemyTransform.position - targetPos;
        toEnemy.y = 0f;
        Quaternion targetRot = toEnemy.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toEnemy.normalized, Vector3.up)
            : startRot;

        float duration = Mathf.Max(0f, finisherSnapDuration);
        if (duration <= 0.001f)
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (enemyTransform == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
    }

    private bool TryGetSafeSnapTarget(Transform enemyTransform, Vector3 startPos, out Vector3 targetPos)
    {
        float preferredDistance = Mathf.Max(0.1f, finisherSnapDistance);
        float minDistance = Mathf.Clamp(finisherSnapMinDistance, 0.1f, preferredDistance);
        int probeSteps = Mathf.Max(1, finisherSnapPositionProbeSteps);

        for (int i = 0; i <= probeSteps; i++)
        {
            float t = probeSteps == 0 ? 0f : i / (float)probeSteps;
            float distance = Mathf.Lerp(preferredDistance, minDistance, t);
            Vector3 candidate = enemyTransform.position + enemyTransform.forward * distance;
            candidate.y = startPos.y;

            if (!IsSnapCandidateClear(candidate))
                continue;

            if (!IsSnapPathClear(startPos, candidate))
                continue;

            targetPos = candidate;
            return true;
        }

        targetPos = startPos;
        return false;
    }

    private bool IsSnapCandidateClear(Vector3 candidatePosition)
    {
        GetPlayerCapsuleAtPosition(candidatePosition, out Vector3 top, out Vector3 bottom, out float radius);
        int mask = GetSnapObstacleMaskWithoutPlayerAndEnemy();
        return !Physics.CheckCapsule(
            top,
            bottom,
            Mathf.Max(0.01f, radius + Mathf.Max(0f, finisherSnapClearancePadding)),
            mask,
            QueryTriggerInteraction.Ignore
        );
    }

    private bool IsSnapPathClear(Vector3 startPos, Vector3 targetPos)
    {
        Vector3 direction = targetPos - startPos;
        float distance = direction.magnitude;
        if (distance < 0.0001f)
            return true;

        direction /= distance;
        GetPlayerCapsuleAtPosition(startPos, out Vector3 top, out Vector3 bottom, out float radius);
        int mask = GetSnapObstacleMaskWithoutPlayerAndEnemy();

        return !Physics.CapsuleCast(
            top,
            bottom,
            Mathf.Max(0.01f, radius),
            direction,
            distance,
            mask,
            QueryTriggerInteraction.Ignore
        );
    }

    private int GetSnapObstacleMaskWithoutPlayerAndEnemy()
    {
        int mask = finisherSnapObstacleMask.value;
        int playerLayerBit = 1 << gameObject.layer;
        mask &= ~playerLayerBit;
        mask &= ~enemyLayerMask.value;
        return mask;
    }

    private void GetPlayerCapsuleAtPosition(Vector3 worldPosition, out Vector3 top, out Vector3 bottom, out float radius)
    {
        if (playerCapsule != null)
        {
            Vector3 lossyScale = transform.lossyScale;
            float scaleX = Mathf.Abs(lossyScale.x);
            float scaleY = Mathf.Abs(lossyScale.y);
            float scaleZ = Mathf.Abs(lossyScale.z);

            radius = playerCapsule.radius * Mathf.Max(scaleX, scaleZ);
            float height = Mathf.Max(radius * 2f, playerCapsule.height * scaleY);
            float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius);

            Vector3 centerOffset = transform.rotation * new Vector3(
                playerCapsule.center.x * scaleX,
                playerCapsule.center.y * scaleY,
                playerCapsule.center.z * scaleZ
            );

            Vector3 center = worldPosition + centerOffset;
            Vector3 up = transform.up;
            top = center + up * halfSegment;
            bottom = center - up * halfSegment;
            return;
        }

        radius = 0.28f;
        float halfSegmentFallback = 0.55f;
        Vector3 centerFallback = worldPosition + Vector3.up * 0.9f;
        top = centerFallback + Vector3.up * halfSegmentFallback;
        bottom = centerFallback - Vector3.up * halfSegmentFallback;
    }

    private bool ShouldBlockAimingForFinisher()
    {
        if (!blockRightMouseNearStunnedEnemy)
            return false;

        return FindClosestStunnedEnemy(finisherRange, requireFrontForFinisher, false) != null;
    }

    private bool IsEnemyTooCloseToShoot()
    {
        float blockRange = Mathf.Max(0.1f, finisherRange);
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, blockRange, overlapColliders, enemyLayerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapColliders[i];
            if (hit == null)
                continue;

            if (CreateFinisherTarget(hit) != null)
                return true;
        }

        return false;
    }

    private Collider[] overlapColliders = new Collider[32];

    private FinisherTarget FindClosestStunnedEnemy(float range, bool requireFrontCheck, bool ignoreLayerMask)
    {
        float bestDist = float.MaxValue;
        FinisherTarget best = null;

        int mask = ignoreLayerMask ? ~0 : enemyLayerMask.value;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, range, overlapColliders, mask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider h = overlapColliders[i];
            if (h == null) continue;
            FinisherTarget target = CreateFinisherTarget(h);
            if (target == null || !target.CanBeFinished()) continue;
            if (requireFrontCheck && !IsPlayerInEnemyFront(target.Transform)) continue;

            float d = Vector3.Distance(transform.position, target.Transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = target;
            }
        }

        return best;
    }

    private FinisherTarget CreateFinisherTarget(Collider hit)
    {
        var target = new FinisherTarget
        {
            bossEnemy = hit.GetComponentInParent<BossEnemy>(),
            advancedEnemy = hit.GetComponentInParent<AdvancedEnemyAI>(),
            enemyTest = hit.GetComponentInParent<Enemytest>()
        };
        return target.Transform != null ? target : null;
    }

    private bool IsPlayerInEnemyFront(Transform enemyTransform)
    {
        if (enemyTransform == null) return false;

        Vector3 toPlayer = transform.position - enemyTransform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return true;

        float angle = Vector3.Angle(enemyTransform.forward, toPlayer.normalized);
        return angle <= Mathf.Clamp(finisherFrontMaxAngle, 1f, 179f);
    }

    private bool PlayFinisherAnimation()
    {
        Animator anim = GetAnimator();
        if (anim == null) return false;
        if (string.IsNullOrEmpty(finisherAnimation)) return false;

        int finisherHash = Animator.StringToHash(finisherAnimation);
        if (!anim.HasState(0, finisherHash))
        {
            Debug.LogWarning("WeaponHandler: не найдено состояние анимации добивания '" + finisherAnimation + "' в base layer.");
            return false;
        }

        anim.Play(finisherHash, 0, 0f);
        anim.Update(0f);
        return true;
    }

    private IEnumerator KillEnemyAfterFinisherAnimation(FinisherTarget enemy)
    {
        Animator anim = GetAnimator();
        if (anim == null)
        {
            if (finisherReturnCoroutine != null)
            {
                StopCoroutine(finisherReturnCoroutine);
                finisherReturnCoroutine = null;
            }
            ReleaseFinisherAnimationLock();
            isFinisherInProgress = false;
            finisherKillCoroutine = null;
            yield break;
        }

        int finisherHash = Animator.StringToHash(finisherAnimation);
        float enterTimeout = Mathf.Max(0.05f, finisherEnterStateTimeout);
        float elapsed = 0f;
        bool finisherStarted = false;

        while (elapsed < enterTimeout)
        {
            AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextState = anim.GetNextAnimatorStateInfo(0);

            if (currentState.shortNameHash == finisherHash || nextState.shortNameHash == finisherHash)
            {
                finisherStarted = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!finisherStarted)
        {
            Debug.LogWarning("WeaponHandler: анимация добивания не стартовала, враг не будет убит без анимации.");
            if (finisherReturnCoroutine != null)
            {
                StopCoroutine(finisherReturnCoroutine);
                finisherReturnCoroutine = null;
            }
            ReleaseFinisherAnimationLock();
            isFinisherInProgress = false;
            finisherKillCoroutine = null;
            yield break;
        }

        float delay = Mathf.Max(0.05f, finisherEnemyDeathDelay);
        yield return new WaitForSeconds(delay);

        if (enemy != null && enemy.CanBeFinished())
            enemy.KillDuringStun();

        finisherKillCoroutine = null;
    }

    private IEnumerator ReturnToIdleAfterFinisher()
    {
        Animator anim = GetAnimator();
        if (anim == null)
        {
            ReleaseFinisherAnimationLock();
            isFinisherInProgress = false;
            finisherReturnCoroutine = null;
            yield break;
        }

        float enterTimeout = Mathf.Max(0.05f, finisherEnterStateTimeout);
        float elapsed = 0f;
        int finisherHash = Animator.StringToHash(finisherAnimation);
        bool finisherStarted = false;

        while (elapsed < enterTimeout)
        {
            if (anim == null)
                break;

            AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextState = anim.GetNextAnimatorStateInfo(0);
            if (currentState.shortNameHash == finisherHash || nextState.shortNameHash == finisherHash)
            {
                finisherStarted = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (finisherStarted)
        {
            float waitEndTimeout = Mathf.Max(0.1f, finisherMaxWaitForAnimationEnd);
            float waitElapsed = 0f;

            while (waitElapsed < waitEndTimeout)
            {
                if (anim == null)
                    break;

                AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo nextState = anim.GetNextAnimatorStateInfo(0);
                bool isStillFinisher = currentState.shortNameHash == finisherHash || nextState.shortNameHash == finisherHash;

                if (!isStillFinisher)
                    break;

                if (currentState.shortNameHash == finisherHash && currentState.normalizedTime >= 1f && !anim.IsInTransition(0))
                    break;

                waitElapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            float delay = Mathf.Max(0.05f, finisherReturnToIdleDelay);
            delay = Mathf.Max(delay, finisherEnemyDeathDelay + 0.05f);
            yield return new WaitForSeconds(delay);
        }

        ForceDefaultIdle();
        ReleaseFinisherAnimationLock();
        isFinisherInProgress = false;
        finisherReturnCoroutine = null;
    }

    private void FaceEnemy(Transform enemyTransform)
    {
        if (enemyTransform == null) return;
        Vector3 dir = enemyTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void StartAiming()
    {
        isAiming = true;
        EquipActiveWeapon();
        ClearHoldVisual();
        if (tankController)
            tankController.moveSpeed = aimWalkSpeed;
        aimAssist.SetAiming(true, muzzlePoint);
    }

    private void StopAiming()
    {
        isAiming = false;
        UnequipWeapon(true);
        if (tankController)
            tankController.moveSpeed = originalWalkSpeed;
        aimAssist.SetAiming(false, null);
        UpdateHoldVisual();
    }

    private void ForceDefaultIdle()
    {
        Animator anim = GetAnimator();
        if (anim == null) return;
        if (string.IsNullOrEmpty(defaultIdleAnimation)) return;

        int stateHash = Animator.StringToHash(defaultIdleAnimation);
        if (!anim.HasState(0, stateHash)) return;

        anim.CrossFadeInFixedTime(defaultIdleAnimation, 0.1f, 0);
    }

    private void ApplyFinisherAnimationLock()
    {
        if (tankController == null)
            return;

        animCon?.SetAnimationLock(true, finisherAnimation);
    }

    private void ApplyFinisherMovementLock()
    {
        if (tankController == null)
            return;

        animCon?.SetAnimationLock(true);
    }

    private void ReleaseFinisherAnimationLock()
    {
        if (tankController == null)
            return;

        animCon?.SetAnimationLock(false);
    }

    private void OnDisable()
    {
        ReleaseFinisherAnimationLock();
        isFinisherInProgress = false;

        if (finisherReturnCoroutine != null)
        {
            StopCoroutine(finisherReturnCoroutine);
            finisherReturnCoroutine = null;
        }

        if (finisherKillCoroutine != null)
        {
            StopCoroutine(finisherKillCoroutine);
            finisherKillCoroutine = null;
        }

        if (finisherSequenceCoroutine != null)
        {
            StopCoroutine(finisherSequenceCoroutine);
            finisherSequenceCoroutine = null;
        }
    }

    private Animator GetAnimator()
    {
        return playerAnimator != null ? playerAnimator : m_Animator;
    }

    private bool CanShoot() =>
        currentWeaponType != InventoryItem.ItemType.Empty &&
        !isReloading &&
        !isShotSequenceRunning &&
        !IsEnemyTooCloseToShoot();

    private IEnumerator ShootingRoutine()
    {
        while (IsFireHeld() && CanShoot())
        {
            ShootOnce();
            float delay = currentAmmoInMag <= 0 ? Mathf.Max(0.05f, emptyMagSoundCooldown) : 0.01f;
            yield return new WaitForSeconds(delay);
        }
        firingCoroutine = null;
    }

    private bool IsFireHeld()
    {
        return gamepadFireHeld || Input.GetMouseButton(0);
    }

    private void ShootOnce()
    {
        if (Time.time < nextFireTime) return;

        if (currentAmmoInMag <= 0)
        {
            PlayEmptyMagSound();
            return;
        }

        StartCoroutine(ShootSequenceRoutine());
    }

    private IEnumerator ShootSequenceRoutine()
    {
        isShotSequenceRunning = true;

        float shootDelay = Mathf.Max(0f, currentShootDelay);
        if (shootDelay > 0f)
            yield return new WaitForSeconds(shootDelay);

        int shotsInSequence = currentFireMode == FireMode.Burst ? Mathf.Max(1, currentBurstCount) : 1;
        float intraBurstInterval = Mathf.Max(0f, currentBurstInterval);

        for (int i = 0; i < shotsInSequence; i++)
        {
            if (currentAmmoInMag <= 0)
                break;

            currentAmmoInMag--;
            PlayShootSound();
            SpawnMuzzleFlash();
            NotifyShotHeardByEnemies();
            PerformRaycastShot();
            ApplyWeaponRecoil();

            if (i < shotsInSequence - 1 && intraBurstInterval > 0f)
                yield return new WaitForSeconds(intraBurstInterval);
        }

        nextFireTime = Time.time + Mathf.Max(0.0001f, currentShotInterval);

        isShotSequenceRunning = false;
    }

    private void PerformRaycastShot()
    {
        if (currentWeaponType == InventoryItem.ItemType.Gun)
        {
            PerformShotgunShot();
            return;
        }

        Vector3 direction = GetShotDirectionFromMuzzle(Mathf.Max(0f, muzzleForwardSpread));
        Ray ray = new Ray(muzzlePoint.position, direction);
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxTracerDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (hitSomething)
        {
            ApplyHitEffectsToTargets(
                hit.collider.GetComponentInParent<AdvancedEnemyAI>(),
                hit.collider.GetComponentInParent<Enemytest>(),
                hit.collider.GetComponentInParent<BossEnemy>(),
                pistolDamageToEnemytest,
                pistolHitsToStun,
                muzzlePoint.position
            );

            if (pistolHitEffect != null)
                Destroy(Instantiate(pistolHitEffect, hit.point, Quaternion.LookRotation(hit.normal)), 2f);
        }
        CreateTracer(direction, hitSomething ? hit.distance : maxTracerDistance);
    }

    private void PerformShotgunShot()
    {
        Vector3 baseDir = GetShotDirectionFromMuzzle(0f);
        Vector3 origin = muzzlePoint.position;
        bool hitSomething = false;
        RaycastHit closestHit = default;
        float closestDistance = maxTracerDistance;

        AdvancedEnemyAI hitEnemy = null;
        Enemytest hitTestEnemy = null;
        BossEnemy hitBossEnemy = null;
        int pellets = Mathf.Max(1, gunPellets);

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = ApplyAngularSpread(baseDir, gunPelletSpread);
            if (Physics.SphereCast(origin, gunHitRadius, dir, out RaycastHit hit, maxTracerDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                hitSomething = true;
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                }

                hitEnemy ??= hit.collider.GetComponentInParent<AdvancedEnemyAI>();
                hitTestEnemy ??= hit.collider.GetComponentInParent<Enemytest>();
                hitBossEnemy ??= hit.collider.GetComponentInParent<BossEnemy>();
            }
        }

        ApplyHitEffectsToTargets(hitEnemy, hitTestEnemy, hitBossEnemy, gunDamageToEnemytest, gunHitsToStun, muzzlePoint.position);

        if (gunHitEffect != null && hitSomething)
            Destroy(Instantiate(gunHitEffect, closestHit.point, Quaternion.LookRotation(closestHit.normal)), 2f);

        CreateTracer(baseDir, hitSomething ? closestDistance : maxTracerDistance);
    }

    private void ApplyHitEffectsToTargets(AdvancedEnemyAI hitEnemy, Enemytest hitTestEnemy, BossEnemy hitBossEnemy, float damageAmount, int hitsToStun, Vector3 hitPosition)
    {
        if (hitEnemy != null && !hitEnemy.IsStunned)
        {
            hitEnemy.NotifyShotHitByPlayer(hitPosition);
            enemyHitCount[hitEnemy] = enemyHitCount.GetValueOrDefault(hitEnemy) + 1;

            if (enemyHitCount[hitEnemy] >= hitsToStun)
            {
                hitEnemy.ApplyStun(20f);
                enemyHitCount[hitEnemy] = 0;
            }
        }

        hitTestEnemy?.TakeWeaponDamage(damageAmount);
        hitBossEnemy?.TakeDamage(damageAmount);
    }

    private Vector3 GetShotDirectionFromMuzzle(float spreadDegrees)
    {
        Transform muzzle = muzzlePoint != null ? muzzlePoint : transform;
        Vector3 baseDirection;

        if (shootByMuzzleForward)
        {
            baseDirection = muzzle.forward;
        }
        else
        {
            baseDirection = aimAssist != null ? aimAssist.GetAimDirection() : muzzle.forward;
        }

        float effectiveSpread = Mathf.Max(0f, spreadDegrees);
        if (isAiming && aimAssist != null && effectiveSpread > 0.001f)
        {
            float lock01 = Mathf.Clamp01(aimAssist.GetLockAccuracy01());
            float reduction = Mathf.Clamp01(lockOnSpreadReduction) * lock01;
            effectiveSpread *= (1f - reduction);
        }

        if (effectiveSpread <= 0.001f)
            return baseDirection.normalized;

        return ApplyAngularSpread(baseDirection, effectiveSpread);
    }

    private Vector3 ApplyAngularSpread(Vector3 baseDirection, float spreadDegrees)
    {
        Transform muzzle = muzzlePoint != null ? muzzlePoint : transform;
        float yaw = Random.Range(-spreadDegrees, spreadDegrees);
        float pitch = Random.Range(-spreadDegrees, spreadDegrees);
        Quaternion spreadRotation = Quaternion.AngleAxis(yaw, muzzle.up) * Quaternion.AngleAxis(pitch, muzzle.right);
        return (spreadRotation * baseDirection).normalized;
    }

    private void NotifyShotHeardByEnemies()
    {
        float loudness = currentWeaponType == InventoryItem.ItemType.Gun
            ? Mathf.Max(0.1f, gunShotLoudness)
            : Mathf.Max(0.1f, pistolShotLoudness);

        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
        PlayerShotFired?.Invoke(origin, loudness);
    }

    private void CreateTracer(Vector3 direction, float distance)
    {
        GameObject prefab = currentWeaponType == InventoryItem.ItemType.Gun ? gunTracerPrefab : pistolTracerPrefab;
        float duration = currentWeaponType == InventoryItem.ItemType.Gun ? gunTracerDuration : pistolTracerDuration;
        float thickness = currentWeaponType == InventoryItem.ItemType.Gun ? gunTracerThickness : pistolTracerThickness;

        if (prefab == null) return;

        float finalDistance = Mathf.Min(distance, maxTracerDistance);
        // Spawn tracer in world space so it always starts at the muzzle and grows toward the hit point
        var tracer = Instantiate(prefab, muzzlePoint.position, Quaternion.LookRotation(direction));
        tracer.transform.localScale = new Vector3(thickness, thickness, 0.05f);
        StartCoroutine(AnimateTracer(tracer, direction, finalDistance, thickness, duration));
    }

    private void SpawnMuzzleFlash()
    {
        if (defaultMuzzlePoint == null)
            return;

        GameObject prefab = currentWeaponType == InventoryItem.ItemType.Gun
            ? gunMuzzleFlashPrefab
            : pistolMuzzleFlashPrefab;
        GameObject lightPrefab = currentWeaponType == InventoryItem.ItemType.Gun
            ? gunMuzzleLightPrefab
            : pistolMuzzleLightPrefab;

        if (prefab == null)
            return;

        GameObject flash = GetMuzzleFlashFromPool(prefab);
        flash.transform.SetPositionAndRotation(defaultMuzzlePoint.position, defaultMuzzlePoint.rotation);
        flash.SetActive(true);
        StartCoroutine(DisableMuzzleFlashAfterDelay(flash, prefab));

        if (lightPrefab == null)
            return;

        GameObject lightObj = GetMuzzleFlashFromPool(lightPrefab);
        lightObj.transform.SetPositionAndRotation(defaultMuzzlePoint.position, defaultMuzzlePoint.rotation);
        lightObj.SetActive(true);
        StartCoroutine(DisableMuzzleFlashAfterDelay(lightObj, lightPrefab));
    }

    private GameObject GetMuzzleFlashFromPool(GameObject prefab)
    {
        if (!muzzleFlashPool.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            muzzleFlashPool[prefab] = pool;
        }

        while (pool.Count > 0)
        {
            GameObject candidate = pool.Dequeue();
            if (candidate != null)
                return candidate;
        }

        GameObject instance = Instantiate(prefab);
        instance.SetActive(false);
        return instance;
    }

    private IEnumerator DisableMuzzleFlashAfterDelay(GameObject flash, GameObject prefab)
    {
        if (flash == null)
            yield break;

        if (muzzleFlashLifetime > 0f)
            yield return muzzleFlashWait ?? new WaitForSeconds(Mathf.Max(0.01f, muzzleFlashLifetime));

        if (flash == null)
            yield break;

        flash.SetActive(false);

        if (!muzzleFlashPool.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            muzzleFlashPool[prefab] = pool;
        }

        pool.Enqueue(flash);
    }

    private IEnumerator AnimateTracer(GameObject tracer, Vector3 direction, float distance, float thickness, float duration)
    {
        Vector3 start = muzzlePoint.position;
        float targetDistance = Mathf.Max(0.05f, distance * tracerLengthFactor);
        float travelTime = Mathf.Max(0.02f, distance / tracerTravelSpeed);
        float t = 0f;

        while (t < travelTime && tracer)
        {
            float frac = t / travelTime;
            float len = Mathf.Max(0.05f, targetDistance * frac);
            float half = len * 0.5f;
            float backShift = (half > 0.01f) ? Mathf.Min(half - 0.01f, tracerMuzzleOffset) : 0f;
            tracer.transform.position = start + direction * (half - backShift);
            tracer.transform.localScale = new Vector3(thickness, thickness, len);

            t += Time.deltaTime;
            yield return null;
        }

        if (tracer)
        {
            float len = targetDistance;
            float half = len * 0.5f;
            float backShift = (half > 0.01f) ? Mathf.Min(half - 0.01f, tracerMuzzleOffset) : 0f;
            tracer.transform.position = start + direction * (half - backShift);
            tracer.transform.localScale = new Vector3(thickness, thickness, len);
            Destroy(tracer, duration);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void PlayShootSound()
    {
        if (currentShootSounds != null && currentShootSounds.Length > 0)
            PlaySound(currentShootSounds[Random.Range(0, currentShootSounds.Length)]);
    }

    private void PlayEmptyMagSound()
    {
        if (emptyMagSound != null && Time.time >= nextEmptyMagSoundTime)
        {
            PlaySound(emptyMagSound);
            nextEmptyMagSoundTime = Time.time + Mathf.Max(0.05f, emptyMagSoundCooldown);
        }
    }

    // ===================================================================
    // ЭКИПИРОВКА И ПЕРЕЗАРЯДКА
    // ===================================================================

    private void EquipActiveWeapon()
    {
        SetCurrentWeaponStats();
        CreateWeaponModelIfNeeded();
        var muzzle = currentWeaponModel?.transform.Find("Muzzle");
        muzzlePoint = muzzle != null ? muzzle : defaultMuzzlePoint;
        if (isAiming)
            aimAssist.SetAiming(true, muzzlePoint);
    }

    private void SetCurrentWeaponStats()
    {
        SaveCurrentAmmo();
        if (playerInventory == null) return;

        int idx = playerInventory.activeItemIndex;
        if (idx < 0 || playerInventory.inventoryData == null) return;
        if (idx >= playerInventory.inventoryData.GetSlotCount())
        {
            currentWeaponType = InventoryItem.ItemType.Empty;
            return;
        }

        var item = playerInventory.inventoryData.GetItemAt(idx);
        if (item == null)
        {
            currentWeaponType = InventoryItem.ItemType.Empty;
            return;
        }
        currentWeaponType = item.type;

        if (item.type == InventoryItem.ItemType.Gun)
        {
            currentWeaponStats = shotgunStats;
            currentRecoilProfile = shotgunRecoilProfile;
            currentGlowProfile = shotgunGlowProfile;
            currentShootSounds = gunShootSounds;
            currentReloadSound = gunReloadSound;
            currentReserveAmmo = PlayerAmmoData.gunReserve;
            ApplyStatsFromProfile(currentWeaponStats, 7, 2f);
            currentAmmoInMag = Mathf.Clamp(PlayerAmmoData.gunInMag, 0, currentMagazineSize);
        }
        else if (item.type == InventoryItem.ItemType.Pistol)
        {
            currentWeaponStats = pistolStats;
            currentRecoilProfile = pistolRecoilProfile;
            currentGlowProfile = pistolGlowProfile;
            currentShootSounds = pistolShootSounds;
            currentReloadSound = pistolReloadSound;
            currentReserveAmmo = PlayerAmmoData.pistolReserve;
            ApplyStatsFromProfile(currentWeaponStats, 12, 1.5f);
            currentAmmoInMag = Mathf.Clamp(PlayerAmmoData.pistolInMag, 0, currentMagazineSize);
        }

        isReloading = false;
        isShotSequenceRunning = false;
    }

    private void ApplyStatsFromProfile(GunStats stats, int fallbackMag, float fallbackReload)
    {
        if (stats != null)
        {
            currentMagazineSize = Mathf.Max(1, stats.magazineSize);
            currentReloadTime = Mathf.Max(0f, stats.reloadDuration);
            currentShotInterval = 1f / Mathf.Max(0.0001f, stats.fireRate);
            currentShootDelay = Mathf.Max(0f, stats.shootDelay);
            currentFireMode = stats.fireMode;
            currentBurstCount = Mathf.Clamp(stats.burstCount, 1, currentMagazineSize);
            currentBurstInterval = Mathf.Max(0f, stats.burstInterval);
            return;
        }

        currentMagazineSize = Mathf.Max(1, fallbackMag);
        currentReloadTime = Mathf.Max(0f, fallbackReload);
        currentShotInterval = 0.25f;
        currentShootDelay = 0f;
        currentFireMode = FireMode.Single;
        currentBurstCount = 1;
        currentBurstInterval = 0f;
    }

    private void CreateWeaponModelIfNeeded()
    {
        int idx = playerInventory.activeItemIndex;
        if (idx < 0 || playerInventory.inventoryData == null) return;
        if (idx >= playerInventory.inventoryData.GetSlotCount()) return;

        var item = playerInventory.inventoryData.GetItemAt(idx);
        if (item == null)
        {
            UnequipWeapon();
            return;
        }

        GameObject prefab = item.type == InventoryItem.ItemType.Gun ? gunPrefab : pistolPrefab;
        Vector3 scale = item.type == InventoryItem.ItemType.Gun ? gunScale : pistolScale;
        Transform holdPoint = item.type == InventoryItem.ItemType.Gun ? shotgunHoldPoint : pistolHoldPoint;
        if (holdPoint == null)
            holdPoint = weaponHoldPoint;

        if (currentWeaponModel) Destroy(currentWeaponModel);

        if (prefab != null)
        {
            currentWeaponModel = Instantiate(prefab, holdPoint, false);
            currentWeaponModel.transform.localScale = scale;
            weaponModelBaseLocalPos = currentWeaponModel.transform.localPosition;
            weaponModelBaseLocalRot = currentWeaponModel.transform.localRotation;
            currentWeaponRenderer = currentWeaponModel.GetComponentInChildren<Renderer>();
            CaptureGlowBaseColor();
        }
    }

    private void UnequipWeapon(bool destroyModel = true)
    {
        SaveCurrentAmmo();
        if (destroyModel && currentWeaponModel)
            Destroy(currentWeaponModel);
        if (destroyModel)
        {
            currentWeaponModel = null;
            currentWeaponRenderer = null;
            currentGlowMaterial = null;
            hasGlowBaseColor = false;
        }

        if (firingCoroutine != null)
        {
            StopCoroutine(firingCoroutine);
            firingCoroutine = null;
        }

        if (recoilCoroutine != null)
        {
            StopCoroutine(recoilCoroutine);
            recoilCoroutine = null;
        }

        if (reloadGlowCoroutine != null)
        {
            StopCoroutine(reloadGlowCoroutine);
            reloadGlowCoroutine = null;
        }

        isShotSequenceRunning = false;
        UpdateHoldVisual();
    }

    private void SaveCurrentAmmo()
    {
        if (currentWeaponType == InventoryItem.ItemType.Gun)
        {
            PlayerAmmoData.gunReserve = currentReserveAmmo;
            PlayerAmmoData.gunInMag = currentAmmoInMag;
        }
        else if (currentWeaponType == InventoryItem.ItemType.Pistol)
        {
            PlayerAmmoData.pistolReserve = currentReserveAmmo;
            PlayerAmmoData.pistolInMag = currentAmmoInMag;
        }
    }

    private void StartReload()
    {
        if (!CanReload()) return;
        StartCoroutine(ReloadRoutine());
    }

    private bool CanReload() =>
        currentWeaponType != InventoryItem.ItemType.Empty &&
        !isReloading &&
        currentAmmoInMag < currentMagazineSize &&
        currentReserveAmmo > 0 &&
        HasAmmoItemForCurrentWeapon() &&
        isAiming;

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        if (tankController != null)
            animCon?.PlayReloadAnimation(currentWeaponType, currentReloadTime);

        SyncCurrentReserveFromData();
        currentAmmoInMag = Mathf.Clamp(currentAmmoInMag, 0, currentMagazineSize);
        currentReserveAmmo = Mathf.Max(0, currentReserveAmmo);

        int needed = currentMagazineSize - currentAmmoInMag;
        if (needed <= 0 || currentReserveAmmo <= 0 || !ConsumeOneAmmoItemForCurrentWeapon())
        {
            isReloading = false;
            yield break;
        }

        PlaySound(currentReloadSound);
        StartReloadGlowTransition();

        yield return new WaitForSeconds(currentReloadTime);

        int take = Mathf.Min(needed, currentReserveAmmo);
        currentAmmoInMag = Mathf.Clamp(currentAmmoInMag + take, 0, currentMagazineSize);
        currentReserveAmmo = Mathf.Max(0, currentReserveAmmo - take);

        SaveCurrentAmmo();
        isReloading = false;
    }

    private void ApplyWeaponRecoil()
    {
        if (currentWeaponModel == null || currentRecoilProfile == null)
            return;

        if (recoilCoroutine != null)
            StopCoroutine(recoilCoroutine);

        recoilCoroutine = StartCoroutine(PlayWeaponRecoil(currentRecoilProfile));
    }

    private IEnumerator PlayWeaponRecoil(RecoilProfile profile)
    {
        Transform weaponTransform = currentWeaponModel != null ? currentWeaponModel.transform : null;
        if (weaponTransform == null)
            yield break;

        Vector3 recoilPos = weaponModelBaseLocalPos + (-Vector3.forward * profile.movementAmplitude);
        Quaternion recoilRot = weaponModelBaseLocalRot * Quaternion.Euler(
            -profile.rotationAmplitude,
            Random.Range(0.25f, 0.5f) * profile.rotationAmplitude,
            Random.Range(-0.2f, 0.2f) * profile.rotationAmplitude
        );

        float recoilTime = Mathf.Max(0.001f, profile.recoilDuration);
        float t = 0f;
        while (t < recoilTime && weaponTransform != null)
        {
            t += Time.deltaTime;
            float k = profile.recoilCurve.Evaluate(Mathf.Clamp01(t / recoilTime));
            weaponTransform.localPosition = Vector3.Lerp(weaponModelBaseLocalPos, recoilPos, k);
            weaponTransform.localRotation = Quaternion.Slerp(weaponModelBaseLocalRot, recoilRot, k);
            yield return null;
        }

        float recoverTime = Mathf.Max(0.001f, profile.recoveryDuration);
        t = 0f;
        while (t < recoverTime && weaponTransform != null)
        {
            t += Time.deltaTime;
            float k = profile.recoveryCurve.Evaluate(Mathf.Clamp01(t / recoverTime));
            weaponTransform.localPosition = Vector3.Lerp(recoilPos, weaponModelBaseLocalPos, k);
            weaponTransform.localRotation = Quaternion.Slerp(recoilRot, weaponModelBaseLocalRot, k);
            yield return null;
        }

        if (weaponTransform != null)
        {
            weaponTransform.localPosition = weaponModelBaseLocalPos;
            weaponTransform.localRotation = weaponModelBaseLocalRot;
        }

        recoilCoroutine = null;
    }

    private void UpdateWeaponGlowVisual()
    {
        if (!isAiming || currentWeaponModel == null || currentGlowProfile == null)
            return;

        float mag = Mathf.Max(1f, currentMagazineSize);
        float transitionFactor = currentGlowProfile.invertGlow
            ? currentAmmoInMag / mag
            : 1f - (currentAmmoInMag / mag);

        float curveValue = currentGlowProfile.glowScaling != null
            ? currentGlowProfile.glowScaling.Evaluate(Mathf.Clamp01(transitionFactor))
            : Mathf.Clamp01(transitionFactor);

        float glowIntensity = currentGlowProfile.invertGlow
            ? Mathf.Lerp(currentGlowProfile.glowMaxIntensity, currentGlowProfile.glowBaseIntensity, curveValue)
            : Mathf.Lerp(currentGlowProfile.glowBaseIntensity, currentGlowProfile.glowMaxIntensity, curveValue);

        SetGlowIntensity(glowIntensity);
    }

    private void StartReloadGlowTransition()
    {
        if (currentGlowProfile == null || currentWeaponModel == null)
            return;

        if (reloadGlowCoroutine != null)
            StopCoroutine(reloadGlowCoroutine);

        reloadGlowCoroutine = StartCoroutine(ReloadGlowTransitionRoutine(Mathf.Max(0.01f, currentReloadTime)));
    }

    private IEnumerator ReloadGlowTransitionRoutine(float duration)
    {
        float startIntensity = currentGlowProfile.glowMaxIntensity;
        float targetIntensity = currentGlowProfile.invertGlow
            ? currentGlowProfile.glowMaxIntensity
            : currentGlowProfile.glowBaseIntensity;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            SetGlowIntensity(intensity);
            yield return null;
        }

        SetGlowIntensity(targetIntensity);
        reloadGlowCoroutine = null;
    }

    private void CaptureGlowBaseColor()
    {
        hasGlowBaseColor = false;

        if (currentWeaponRenderer == null || currentGlowProfile == null)
            return;

        Material[] materials = currentWeaponRenderer.materials;
        if (materials == null || materials.Length == 0)
            return;

        int idx = Mathf.Clamp(currentGlowProfile.materialIndex, 0, materials.Length - 1);
        Material mat = materials[idx];
        if (mat == null || !mat.HasProperty(EmissionColorId))
            return;

        currentGlowMaterial = mat;
        currentGlowBaseColor = mat.GetColor(EmissionColorId);
        hasGlowBaseColor = true;
    }

    private void SetGlowIntensity(float intensity)
    {
        if (currentGlowMaterial == null || currentGlowProfile == null)
            return;

        if (!currentGlowMaterial.HasProperty(EmissionColorId))
            return;

        if (!hasGlowBaseColor)
        {
            currentGlowBaseColor = currentGlowMaterial.GetColor(EmissionColorId);
            hasGlowBaseColor = true;
        }

        currentGlowMaterial.EnableKeyword("_EMISSION");
        currentGlowMaterial.SetColor(EmissionColorId, currentGlowBaseColor * Mathf.Max(0f, intensity));
    }

    private void TryManualReload()
    {
        if (currentWeaponType == InventoryItem.ItemType.Empty || isReloading) return;
        SyncCurrentReserveFromData();

        if (currentAmmoInMag >= currentMagazineSize || currentReserveAmmo <= 0 || !isAiming)
        {
            PlayEmptyMagSound();
            return;
        }

        StartReload();
    }

    public void RequestGamepadReload()
    {
        TryManualReload();
    }

    public void AddAmmo(InventoryItem.ItemType type, int amount)
    {
        if (type == InventoryItem.ItemType.Gun || type == InventoryItem.ItemType.ShotgunAmmo)
            PlayerAmmoData.gunReserve += amount;
        else if (type == InventoryItem.ItemType.Pistol || type == InventoryItem.ItemType.PistolAmmo)
            PlayerAmmoData.pistolReserve += amount;

        if (currentWeaponType == InventoryItem.ItemType.Gun && (type == InventoryItem.ItemType.Gun || type == InventoryItem.ItemType.ShotgunAmmo))
        {
            currentReserveAmmo = PlayerAmmoData.gunReserve;
        }
        else if (currentWeaponType == InventoryItem.ItemType.Pistol && (type == InventoryItem.ItemType.Pistol || type == InventoryItem.ItemType.PistolAmmo))
        {
            currentReserveAmmo = PlayerAmmoData.pistolReserve;
        }
    }

    public void OnActiveItemChanged()
    {
        SetCurrentWeaponStats();
        if (currentWeaponType == InventoryItem.ItemType.Empty)
        {
            UnequipWeapon();
            ClearHoldVisual();
            return;
        }

        if (isAiming)
        {
            CreateWeaponModelIfNeeded();
            var muzzle = currentWeaponModel?.transform.Find("Muzzle");
            muzzlePoint = muzzle != null ? muzzle : defaultMuzzlePoint;
            aimAssist.SetAiming(true, muzzlePoint);
        }
        else
        {
            UnequipWeapon();
        }

        UpdateHoldVisual();
    }

    private InventoryItem.ItemType GetActiveWeaponTypeFromInventory()
    {
        if (playerInventory == null || playerInventory.inventoryData == null)
            return InventoryItem.ItemType.Empty;

        int idx = playerInventory.activeItemIndex;
        if (idx < 0 || idx >= playerInventory.inventoryData.GetSlotCount())
            return InventoryItem.ItemType.Empty;

        var item = playerInventory.inventoryData.GetItemAt(idx);
        if (item == null)
            return InventoryItem.ItemType.Empty;

        if (item.type != InventoryItem.ItemType.Gun && item.type != InventoryItem.ItemType.Pistol)
            return InventoryItem.ItemType.Empty;

        return item.type;
    }

    private void UpdateHoldVisual()
    {
        if (isAiming)
        {
            ClearHoldVisual();
            return;
        }

        if (!HasActiveWeaponSelected())
        {
            ClearHoldVisual();
            return;
        }

        InventoryItem.ItemType activeType = GetActiveWeaponTypeFromInventory();
        if (activeType != InventoryItem.ItemType.Gun && activeType != InventoryItem.ItemType.Pistol)
        {
            ClearHoldVisual();
            return;
        }

        if (holdVisualInstance != null && holdVisualType == activeType)
            return;

        ClearHoldVisual();

        GameObject prefab = activeType == InventoryItem.ItemType.Gun ? gunHoldVisualPrefab : pistolHoldVisualPrefab;
        Vector3 scale = activeType == InventoryItem.ItemType.Gun ? gunScale : pistolScale;

        if (prefab == null)
            return;

        Transform holdPoint = activeType == InventoryItem.ItemType.Gun ? gunHoldVisualPoint : pistolHoldVisualPoint;
        if (holdPoint == null)
            holdPoint = activeType == InventoryItem.ItemType.Gun ? shotgunHoldPoint : pistolHoldPoint;
        if (holdPoint == null)
            holdPoint = weaponHoldPoint;

        if (holdPoint == null)
            return;

        holdVisualInstance = Instantiate(prefab, holdPoint, false);
        holdVisualInstance.transform.localScale = scale;
        holdVisualInstance.transform.localPosition = Vector3.zero;
        holdVisualInstance.transform.localRotation = Quaternion.identity;
        holdVisualType = activeType;
    }

    private void ClearHoldVisual()
    {
        if (holdVisualInstance != null)
            Destroy(holdVisualInstance);

        holdVisualInstance = null;
        holdVisualType = InventoryItem.ItemType.Empty;
    }

    private void SyncCurrentReserveFromData()
    {
        if (currentWeaponType == InventoryItem.ItemType.Gun)
            currentReserveAmmo = Mathf.Max(0, PlayerAmmoData.gunReserve);
        else if (currentWeaponType == InventoryItem.ItemType.Pistol)
            currentReserveAmmo = Mathf.Max(0, PlayerAmmoData.pistolReserve);
    }

    private bool HasAmmoItemForCurrentWeapon()
    {
        if (playerInventory == null || playerInventory.inventoryData == null)
            return false;

        var requiredType = GetRequiredAmmoItemTypeForCurrentWeapon();
        if (requiredType == InventoryItem.ItemType.Empty)
            return false;

        return playerInventory.inventoryData.CountItemsByType(requiredType) > 0;
    }

    private bool ConsumeOneAmmoItemForCurrentWeapon()
    {
        if (playerInventory == null || playerInventory.inventoryData == null)
            return false;

        var requiredType = GetRequiredAmmoItemTypeForCurrentWeapon();
        if (requiredType == InventoryItem.ItemType.Empty)
            return false;

        bool consumed = playerInventory.inventoryData.ConsumeOneItemByType(requiredType);
        if (consumed && playerInventory.inventoryUI != null)
            playerInventory.inventoryUI.UpdateInventoryUI();

        return consumed;
    }

    private InventoryItem.ItemType GetRequiredAmmoItemTypeForCurrentWeapon()
    {
        if (currentWeaponType == InventoryItem.ItemType.Pistol)
            return InventoryItem.ItemType.PistolAmmo;

        if (currentWeaponType == InventoryItem.ItemType.Gun)
            return InventoryItem.ItemType.ShotgunAmmo;

        return InventoryItem.ItemType.Empty;
    }

    private void OnDestroy()
    {
        if (finisherReturnCoroutine != null)
            StopCoroutine(finisherReturnCoroutine);

        if (recoilCoroutine != null)
            StopCoroutine(recoilCoroutine);

        if (reloadGlowCoroutine != null)
            StopCoroutine(reloadGlowCoroutine);

        enemyHitCount.Clear();
    }
}