using UnityEngine;
using System.Collections;

public class WeaponHandler : MonoBehaviour
{
    [Header("=== Точки ===")]
    [SerializeField] private Transform weaponHoldPoint;
    [SerializeField] private Transform defaultMuzzlePoint; // точка на камере или теле, если оружие не экипировано

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
    [SerializeField] private float pistolFireRate = 0.09f;
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

    [SerializeField, Tooltip("Максимальная длина луча, если не попал во что-то")]
    private float maxTracerDistance = 300f;

    [SerializeField] private GameObject gunHitEffect;    // Искры / вспышка для лазера
    [SerializeField] private GameObject pistolHitEffect; // Искры / вспышка для пистолета

    [Header("=== Прицел и Aim Assist ===")]
    [SerializeField] private float aimWalkSpeed = 1.5f;
    [SerializeField] private AimAssist aimAssist;
    [SerializeField] private LayerMask enemyLayerMask;

    [Header("=== Анимация и melee ===")]
    [SerializeField] public Animator playerAnimator;
    [SerializeField] public string meleeTrigger = "MeleePush";

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

    private GameObject currentWeaponModel;
    private float nextFireTime = 0f;

    private AdvancedEnemyAI closestEnemy;
    private bool isTooCloseToEnemy = false;

    private void Awake()
    {
        playerInventory = GetComponent<PlayerInventory>();
        tankController = GetComponent<TankController>();
        if (tankController) originalWalkSpeed = tankController.moveSpeed;

        muzzlePoint = defaultMuzzlePoint;

        // Инициализация патронов
        if (PlayerAmmoData.gunReserve == 0) PlayerAmmoData.gunReserve = gunStartReserve;
        if (PlayerAmmoData.pistolReserve == 0) PlayerAmmoData.pistolReserve = pistolStartReserve;

        // AimAssist
        if (aimAssist == null) aimAssist = gameObject.AddComponent<AimAssist>();
        aimAssist.Initialize(enemyLayerMask);
    }

    private void Update()
    {
        CheckForCloseEnemy();
        HandleInput();

        if (Input.GetKeyDown(KeyCode.R)) TryManualReload();
    }

    private void CheckForCloseEnemy()
    {
        var melee = GetComponent<MeleeHandler>();
        if (melee == null) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, melee.minShootDistance, enemyLayerMask);

        closestEnemy = null;
        float minDist = float.MaxValue;

        foreach (var col in hits)
        {
            var enemy = col.GetComponent<AdvancedEnemyAI>();
            if (enemy != null)
            {
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestEnemy = enemy;
                }
            }
        }

        isTooCloseToEnemy = closestEnemy != null;
    }

    private void HandleInput()
    {
        bool aiming = Input.GetMouseButton(1);

        if (aiming && !isAiming)
            StartAiming();
        else if (!aiming && isAiming)
            StopAiming();

        if (aiming && Input.GetMouseButton(0) && CanShoot() && firingCoroutine == null)
            firingCoroutine = StartCoroutine(ShootingRoutine());
    }

    private void StartAiming()
    {
        isAiming = true;
        EquipActiveWeapon();

        if (tankController)
            tankController.moveSpeed = aimWalkSpeed;

        aimAssist.SetAiming(true, muzzlePoint);
    }

    private void StopAiming()
    {
        isAiming = false;
        UnequipWeapon();

        if (tankController)
            tankController.moveSpeed = originalWalkSpeed;

        aimAssist.SetAiming(false, null);
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

        var melee = GetComponent<MeleeHandler>();

        // Толчок при близком контакте
        if (isTooCloseToEnemy && closestEnemy != null && melee != null)
        {
            if (melee.TryMeleeAttack(closestEnemy))
            {
                nextFireTime = Time.time + currentFireRate;
                return;
            }
        }

        // Нет патронов
        if (currentAmmoInMag <= 0)
        {
            PlayEmptyMagSound();
            if (currentReserveAmmo > 0 && isAiming) StartReload();
            return;
        }

        // Стрельба
        currentAmmoInMag--;
        PlayShootSound();
        PerformRaycastShot();

        nextFireTime = Time.time + currentFireRate;

        if (currentAmmoInMag <= 0 && currentReserveAmmo > 0 && isAiming)
            StartReload();
    }

    private void PerformRaycastShot()
    {
        Vector3 direction = aimAssist.GetAimDirection();
        float spread = aimAssist.GetSpread();

        direction = Quaternion.Euler(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0
        ) * direction;

        Ray ray = new Ray(muzzlePoint.position, direction);

        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, maxTracerDistance);

        // Урон врагу
        if (hitSomething && hit.collider.TryGetComponent<AdvancedEnemyAI>(out var enemy))
        {
            Destroy(enemy.gameObject);
        }

        // Искры
        GameObject hitFx = currentWeaponType == InventoryItem.ItemType.Gun ? gunHitEffect : pistolHitEffect;
        if (hitFx != null && hitSomething)
        {
            var fx = Instantiate(hitFx, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(fx, 2f); // автоудаление искр
        }

        // Трассировщик
        float distance = hitSomething ? hit.distance : maxTracerDistance;
        CreateTracer(direction, distance);
    }

    private void CreateTracer(Vector3 direction, float distance)
    {
        GameObject prefab = currentWeaponType == InventoryItem.ItemType.Gun ? gunTracerPrefab : pistolTracerPrefab;
        float duration = currentWeaponType == InventoryItem.ItemType.Gun ? gunTracerDuration : pistolTracerDuration;
        float thickness = currentWeaponType == InventoryItem.ItemType.Gun ? gunTracerThickness : pistolTracerThickness;

        if (prefab == null) return;

        // Ограничиваем дистанцию, чтобы луч не улетал в космос
        float finalDistance = Mathf.Min(distance, maxTracerDistance);

        var tracer = Instantiate(prefab, muzzlePoint.position, Quaternion.LookRotation(direction), muzzlePoint);
        tracer.transform.localPosition = Vector3.forward * (finalDistance * 0.5f);
        tracer.transform.localScale = new Vector3(thickness, thickness, finalDistance);

        Destroy(tracer, duration);
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
}