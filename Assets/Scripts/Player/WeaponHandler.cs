using UnityEngine;
using System.Collections;
using System.Collections.Generic; // ← Добавлено для Dictionary

public class WeaponHandler : MonoBehaviour
{
    [Header("=== Точки ===")]
    [SerializeField] private Transform weaponHoldPoint;
    [SerializeField] private Transform defaultMuzzlePoint;

    [Header("=== Модели оружия ===")]
    [SerializeField] private GameObject gunPrefab;
    [SerializeField] private Vector3 gunScale = Vector3.one;
    [SerializeField] private GameObject pistolPrefab;
    [SerializeField] private Vector3 pistolScale = Vector3.one;

    [Header("=== Звуки ===")]
    [SerializeField] public AudioSource audioSource;
    [SerializeField] public AudioClip emptyMagSound;

    [Header("=== ЛАЗЕРНАЯ ВИНТОВКА ===")]
    [SerializeField] private float gunFireRate = 0.15f;
    [SerializeField] private int gunMagazineSize = 7;
    [SerializeField] private int gunStartReserve = 35;
    [SerializeField] private float gunReloadTime = 2f;
    [SerializeField] private AudioClip[] gunShootSounds;
    [SerializeField] private AudioClip gunReloadSound;

    [Header("=== ПИСТОЛЕТ ===")]
    [SerializeField] private float pistolFireRate = 0.35f;
    [SerializeField] private int pistolMagazineSize = 12;
    [SerializeField] private int pistolStartReserve = 120;
    [SerializeField] private float pistolReloadTime = 1.5f;
    [SerializeField] private AudioClip[] pistolShootSounds;
    [SerializeField] private AudioClip pistolReloadSound;

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
    [SerializeField] private GameObject gunHitEffect;
    [SerializeField] private GameObject pistolHitEffect;

    [Header("=== Прицел и Aim Assist ===")]
    [SerializeField] private float aimWalkSpeed = 1.5f; 
    [SerializeField] private AimAssist aimAssist;
    [SerializeField] private LayerMask enemyLayerMask;

    [Header("=== Анимация и melee ===")]
    [SerializeField] public Animator playerAnimator;
    [SerializeField] public string meleeTrigger = "MeleePush";
    [SerializeField] private string finisherAnimation = "attack_stun_enemy";
    [SerializeField] private float finisherRange = 1.4f;
    [SerializeField] private bool requireFrontForFinisher = true;
    [SerializeField, Range(1f, 179f)] private float finisherFrontMaxAngle = 85f;
    [SerializeField, Tooltip("Задержка перед запуском смерти врага (сек)")]
    private float finisherEnemyDeathDelay = 0.6f;

    [Header("=== ОГЛУШЕНИЕ ВРАГОВ ===")]
    [SerializeField, Tooltip("Сколько попаданий из пистолета нужно для оглушения врага")]
    private int pistolHitsToStun = 12;

    [SerializeField, Tooltip("Сколько попаданий из лазерной винтовки нужно для оглушения врага")]
    private int gunHitsToStun = 3;

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
    private float originalWalkSpeed = 5f;
    private bool isAiming = false;
    private bool isReloading = false;
    private Coroutine firingCoroutine;
    private InventoryItem.ItemType currentWeaponType = InventoryItem.ItemType.Empty;
    private float currentFireRate;
    private int currentMagazineSize;
    private int currentReserveAmmo;
    private int currentAmmoInMag;
    private float currentReloadTime;
    private AudioClip[] currentShootSounds;
    private AudioClip currentReloadSound;
    [SerializeField] private Animator m_Animator;
    private GameObject currentWeaponModel;
    private float nextFireTime = 0f;
    // Словарь для отслеживания количества попаданий по каждому врагу
    private Dictionary<AdvancedEnemyAI, int> enemyHitCount = new Dictionary<AdvancedEnemyAI, int>();

    private void Awake()
    {
        m_Animator = GetComponent<Animator>();
        playerInventory = GetComponent<PlayerInventory>();
        tankController = GetComponent<TankController>();
        if (tankController) originalWalkSpeed = tankController.moveSpeed;
        muzzlePoint = defaultMuzzlePoint;

        // Unity сериализует поля: если значение уже задано в инспекторе/префабе
        // изменение дефолта в коде не применится
        // минимум задержки между выстрелами пистолета
        pistolFireRate = Mathf.Max(pistolFireRate, 0.35f);

        if (PlayerAmmoData.gunReserve == 0) PlayerAmmoData.gunReserve = gunStartReserve;
        if (PlayerAmmoData.pistolReserve == 0) PlayerAmmoData.pistolReserve = pistolStartReserve;

        if (aimAssist == null) aimAssist = gameObject.AddComponent<AimAssist>();
        aimAssist.Initialize(enemyLayerMask);
    }

    private void Update()
    {
        if (TryFinisherAttack())
            return;
        HandleInput();
        if (Input.GetKeyDown(KeyCode.R)) TryManualReload();
    }

    private void HandleInput()
    {
        bool hasActiveWeapon = HasActiveWeaponSelected();
        bool aiming = hasActiveWeapon && Input.GetMouseButton(1);
        if (aiming && !isAiming)
            StartAiming();
        else if (!aiming && isAiming)
            StopAiming();

        if (aiming && Input.GetMouseButton(0) && CanShoot() && firingCoroutine == null)
            firingCoroutine = StartCoroutine(ShootingRoutine());
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

    private bool TryFinisherAttack()
    {
        if (!Input.GetMouseButtonDown(0)) return false;

        var enemy = FindClosestStunnedEnemy(finisherRange);
        if (enemy == null) return false;

        FaceEnemy(enemy.transform);
        PlayFinisherAnimation();
        StartCoroutine(KillEnemyAfterDelay(enemy, finisherEnemyDeathDelay));
        return true;
    }

    private AdvancedEnemyAI FindClosestStunnedEnemy(float range)
    {
        float bestDist = float.MaxValue;
        AdvancedEnemyAI best = null;

        var hits = Physics.OverlapSphere(transform.position, range, enemyLayerMask);
        foreach (var h in hits)
        {
            if (h == null) continue;
            var enemy = h.GetComponentInParent<AdvancedEnemyAI>();
            if (enemy == null || !enemy.CanBeFinished()) continue;
            if (requireFrontForFinisher && !IsPlayerInEnemyFront(enemy.transform)) continue;

            float d = Vector3.Distance(transform.position, enemy.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = enemy;
            }
        }

        return best;
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

    private void PlayFinisherAnimation()
    {
        Animator anim = GetAnimator();
        if (anim == null) return;
        if (string.IsNullOrEmpty(finisherAnimation)) return;

        anim.CrossFadeInFixedTime(finisherAnimation, 0.1f, 0);
    }

    private IEnumerator KillEnemyAfterDelay(AdvancedEnemyAI enemy, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        if (enemy != null)
            enemy.KillDuringStun();
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
        Animator anim = GetAnimator();
        if (anim != null)
        {
            anim.SetBool("isAiming", true);
            anim.SetLayerWeight(1, 1f);
        }
        EquipActiveWeapon();
        if (tankController)
            tankController.moveSpeed = aimWalkSpeed;
        aimAssist.SetAiming(true, muzzlePoint);
    }

    private void StopAiming()
    {
        isAiming = false;
        Animator anim = GetAnimator();
        if (anim != null)
        {
            anim.SetBool("isAiming", false);
            anim.SetLayerWeight(1, 0f);
        }
        UnequipWeapon();
        if (tankController)
            tankController.moveSpeed = originalWalkSpeed;
        aimAssist.SetAiming(false, null);
    }

    private Animator GetAnimator()
    {
        return playerAnimator != null ? playerAnimator : m_Animator;
    }

    private bool CanShoot() => currentWeaponType != InventoryItem.ItemType.Empty && !isReloading;

    private IEnumerator ShootingRoutine()
    {
        while (Input.GetMouseButton(0) && CanShoot())
        {
            ShootOnce();
            yield return new WaitForSeconds(currentAmmoInMag > 0 ? currentFireRate : 0.3f);
        }
        firingCoroutine = null;
    }

    private void ShootOnce()
    {
        if (Time.time < nextFireTime) return;

        if (currentAmmoInMag <= 0)
        {
            PlayEmptyMagSound();
            if (currentReserveAmmo > 0 && isAiming) StartReload();
            return;
        }

        currentAmmoInMag--;
        PlayShootSound();
        PerformRaycastShot();
        nextFireTime = Time.time + currentFireRate;

        if (currentAmmoInMag <= 0 && currentReserveAmmo > 0 && isAiming)
            StartReload();
    }

    private void PerformRaycastShot()
    {
        if (currentWeaponType == InventoryItem.ItemType.Gun)
        {
            PerformShotgunShot();
            return;
        }

        Vector3 direction = aimAssist.GetAimDirection();
        float spread = aimAssist.GetSpread();
        direction = Quaternion.Euler(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0
        ) * direction;

        Ray ray = new Ray(muzzlePoint.position, direction);
        bool hitSomething = Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxTracerDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        // === СИСТЕМА ОГЛУШЕНИЯ ===
        if (hitSomething && hit.collider.TryGetComponent<AdvancedEnemyAI>(out var enemy))
        {
            // В стане урон/стан не проходит
            if (enemy.IsStunned) return;

            int hitsRequired = pistolHitsToStun;

            if (!enemyHitCount.ContainsKey(enemy))
                enemyHitCount[enemy] = 0;

            enemyHitCount[enemy]++;

            if (enemyHitCount[enemy] >= hitsRequired)
            {
                enemy.ApplyStun(20f); // Длительность стана — как было раньше
                enemyHitCount[enemy] = 0; // Сбрасываем счётчик после успешного оглушения
            }
        }

        // Искры от попадания
        if (pistolHitEffect != null && hitSomething)
        {
            var fx = Instantiate(pistolHitEffect, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(fx, 2f);
        }

        // Трассировщик/луч
        float distance = hitSomething ? hit.distance : maxTracerDistance;
        CreateTracer(direction, distance);
    }

    private void PerformShotgunShot()
    {
        Vector3 baseDir = aimAssist.GetAimDirection();
        Vector3 origin = muzzlePoint.position;

        bool hitSomething = false;
        RaycastHit closestHit = default;
        float closestDistance = maxTracerDistance;

        AdvancedEnemyAI hitEnemy = null;

        int pellets = Mathf.Max(1, gunPellets);
        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = Quaternion.Euler(
                Random.Range(-gunPelletSpread, gunPelletSpread),
                Random.Range(-gunPelletSpread, gunPelletSpread),
                0f
            ) * baseDir;

            if (Physics.SphereCast(
                origin,
                gunHitRadius,
                dir,
                out RaycastHit hit,
                maxTracerDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide))
            {
                hitSomething = true;
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                }

                if (hitEnemy == null && hit.collider.TryGetComponent<AdvancedEnemyAI>(out var enemy))
                    hitEnemy = enemy;
            }
        }

        if (hitEnemy != null && !hitEnemy.IsStunned)
        {
            int hitsRequired = gunHitsToStun;

            if (!enemyHitCount.ContainsKey(hitEnemy))
                enemyHitCount[hitEnemy] = 0;

            enemyHitCount[hitEnemy]++;

            if (enemyHitCount[hitEnemy] >= hitsRequired)
            {
                hitEnemy.ApplyStun(20f);
                enemyHitCount[hitEnemy] = 0;
            }
        }

        if (gunHitEffect != null && hitSomething)
        {
            var fx = Instantiate(gunHitEffect, closestHit.point, Quaternion.LookRotation(closestHit.normal));
            Destroy(fx, 2f);
        }

        float distance = hitSomething ? closestDistance : maxTracerDistance;
        CreateTracer(baseDir, distance);
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

    private void PlayShootSound()
    {
        if (audioSource && currentShootSounds != null && currentShootSounds.Length > 0)
            audioSource.PlayOneShot(currentShootSounds[Random.Range(0, currentShootSounds.Length)]);
    }

    private void PlayEmptyMagSound()
    {
        if (audioSource && emptyMagSound)
            audioSource.PlayOneShot(emptyMagSound);
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
        var slots = playerInventory.inventoryData?.GetSlots();
        if (idx < 0 || idx >= slots?.Count || slots[idx] == null)
        {
            currentWeaponType = InventoryItem.ItemType.Empty;
            return;
        }

        var item = slots[idx];
        currentWeaponType = item.type;

        if (item.type == InventoryItem.ItemType.Gun)
        {
            currentFireRate = gunFireRate;
            currentMagazineSize = gunMagazineSize;
            currentReloadTime = gunReloadTime;
            currentShootSounds = gunShootSounds;
            currentReloadSound = gunReloadSound;
            currentReserveAmmo = PlayerAmmoData.gunReserve;
            currentAmmoInMag = PlayerAmmoData.gunInMag > 0 ? PlayerAmmoData.gunInMag : gunMagazineSize;
        }
        else if (item.type == InventoryItem.ItemType.Pistol)
        {
            currentFireRate = pistolFireRate;
            currentMagazineSize = pistolMagazineSize;
            currentReloadTime = pistolReloadTime;
            currentShootSounds = pistolShootSounds;
            currentReloadSound = pistolReloadSound;
            currentReserveAmmo = PlayerAmmoData.pistolReserve;
            currentAmmoInMag = PlayerAmmoData.pistolInMag > 0 ? PlayerAmmoData.pistolInMag : pistolMagazineSize;
        }

        isReloading = false;
    }

    private void CreateWeaponModelIfNeeded()
    {
        if (!isAiming) return;

        int idx = playerInventory.activeItemIndex;
        var slots = playerInventory.inventoryData?.GetSlots();
        if (idx < 0 || idx >= slots?.Count || slots[idx] == null) return;

        var item = slots[idx];
        GameObject prefab = item.type == InventoryItem.ItemType.Gun ? gunPrefab : pistolPrefab;
        Vector3 scale = item.type == InventoryItem.ItemType.Gun ? gunScale : pistolScale;

        if (currentWeaponModel) Destroy(currentWeaponModel);

        if (prefab != null)
        {
            currentWeaponModel = Instantiate(prefab, weaponHoldPoint, false);
            currentWeaponModel.transform.localScale = scale;
        }
    }

    private void UnequipWeapon()
    {
        SaveCurrentAmmo();
        if (currentWeaponModel)
            Destroy(currentWeaponModel);
        currentWeaponModel = null;

        if (firingCoroutine != null)
        {
            StopCoroutine(firingCoroutine);
            firingCoroutine = null;
        }
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
        isAiming;

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        if (audioSource && currentReloadSound) audioSource.PlayOneShot(currentReloadSound);

        yield return new WaitForSeconds(currentReloadTime);

        int needed = currentMagazineSize - currentAmmoInMag;
        int take = Mathf.Min(needed, currentReserveAmmo);
        currentAmmoInMag += take;
        currentReserveAmmo -= take;

        SaveCurrentAmmo();
        isReloading = false;
    }

    private void TryManualReload()
    {
        if (currentWeaponType == InventoryItem.ItemType.Empty || isReloading) return;

        if (currentAmmoInMag >= currentMagazineSize || currentReserveAmmo <= 0 || !isAiming)
        {
            PlayEmptyMagSound();
            return;
        }

        StartReload();
    }

    public void AddAmmo(InventoryItem.ItemType type, int amount)
    {
        if (type == InventoryItem.ItemType.Gun)
            PlayerAmmoData.gunReserve += amount;
        else if (type == InventoryItem.ItemType.Pistol)
            PlayerAmmoData.pistolReserve += amount;

        if (currentWeaponType == type)
        {
            currentReserveAmmo = type == InventoryItem.ItemType.Gun ? PlayerAmmoData.gunReserve : PlayerAmmoData.pistolReserve;
            if (currentAmmoInMag <= 0 && currentReserveAmmo > 0 && isAiming)
                StartReload();
        }
    }

    public void OnActiveItemChanged()
    {
        SetCurrentWeaponStats();
        if (isAiming)
        {
            CreateWeaponModelIfNeeded();
            var muzzle = currentWeaponModel?.transform.Find("Muzzle");
            muzzlePoint = muzzle != null ? muzzle : defaultMuzzlePoint;
            aimAssist.SetAiming(true, muzzlePoint);
        }
    }

    private void OnDestroy()
    {
        enemyHitCount.Clear();
    }
}