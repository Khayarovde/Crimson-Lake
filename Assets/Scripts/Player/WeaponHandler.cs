using UnityEngine;
using System.Collections;

public class WeaponHandler : MonoBehaviour
{
    [Header("Точки")]
    [SerializeField] private Transform weaponHoldPoint;
    [SerializeField] private Transform muzzlePoint;

    [Header("Модели")]
    [SerializeField] private GameObject gunPrefab;
    [SerializeField] private Vector3 gunScale = Vector3.one;
    [SerializeField] private GameObject pistolPrefab;
    [SerializeField] private Vector3 pistolScale = Vector3.one;

    [Header("Звуки")]
    [SerializeField] public AudioSource audioSource;
    [SerializeField] private AudioClip emptyMagSound;

    [Header("=== ЛАЗЕРНАЯ ВИНТОВКА (Gun) ===")]
    [SerializeField] private float gunFireRate = 0.15f;
    [SerializeField] private float gunDamage = 40f;
    [SerializeField] private int gunMagazineSize = 7;
    [SerializeField] private int gunStartReserve = 35;
    [SerializeField] private float gunReloadTime = 2f;
    [SerializeField] private AudioClip[] gunShootSounds;
    [SerializeField] private AudioClip gunReloadSound;
    [SerializeField] private GameObject laserShotPrefab;
    [SerializeField] private float laserDuration = 0.12f;

    [Header("=== ПИСТОЛЕТ (Pistol) ===")]
    [SerializeField] private float pistolFireRate = 0.09f;
    [SerializeField] private float pistolDamage = 20f;
    [SerializeField] private int pistolMagazineSize = 12;
    [SerializeField] private int pistolStartReserve = 120;
    [SerializeField] private float pistolReloadTime = 1.5f;
    [SerializeField] private AudioClip[] pistolShootSounds;
    [SerializeField] private AudioClip pistolReloadSound;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 100f;

    [Header("Прицел")]
    [SerializeField] private GameObject aimBeamPrefab;
    [SerializeField] private float aimWalkSpeed = 1.5f;

    [Header("Анимация melee (толчок оружием)")]
    [SerializeField] public Animator playerAnimator;
    [SerializeField] public string meleeTrigger = "MeleePush";

    [Header("Layer для врагов")]
    [SerializeField] private LayerMask enemyLayerMask;

    // Runtime
    private PlayerInventory playerInventory;
    private TankController tankController;
    private float originalWalkSpeed;

    private bool isAiming = false;
    private bool isReloading = false;
    private Coroutine firingCoroutine;

    private InventoryItem.ItemType currentWeaponType = InventoryItem.ItemType.Empty;
    private float currentFireRate;
    private float currentDamage;
    private int currentMagazineSize;
    private int currentReserveAmmo;
    private int currentAmmoInMag;
    private float currentReloadTime;
    private AudioClip[] currentShootSounds;
    private AudioClip currentReloadSound;

    private GameObject currentWeaponModel;
    private GameObject currentAimBeam;

    private float nextFireTime = 0f;

    private bool isTooCloseToEnemy = false;
    private Collider closestEnemyCollider;  // ← НОВОЕ: ближайший враг

    private AdvancedEnemyAI closestEnemy = null;  // ← Теперь храним сам скрипт врага

    private void Awake()
    {
        playerInventory = GetComponent<PlayerInventory>();
        tankController = GetComponent<TankController>();
        if (tankController) originalWalkSpeed = tankController.moveSpeed;
        if (muzzlePoint == null) muzzlePoint = weaponHoldPoint;

        // Инициализация стартовых патронов
        if (PlayerAmmoData.gunReserve == 0) PlayerAmmoData.gunReserve = gunStartReserve;
        if (PlayerAmmoData.pistolReserve == 0) PlayerAmmoData.pistolReserve = pistolStartReserve;
    }

    private void Update()
    {
        HandleInput();
        UpdateAimBeam();
        CheckForCloseEnemy();  // Проверяем каждый кадр

        if (Input.GetKeyDown(KeyCode.R))
            TryManualReload();
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
        {
            isAiming = true;
            int idx = playerInventory.activeItemIndex;
            var slots = playerInventory.inventoryData?.GetSlots();
            if (idx < 0 || idx >= slots?.Count || slots[idx] == null || slots[idx].type == InventoryItem.ItemType.Empty)
                return;

            EquipActiveWeapon();
            if (tankController) tankController.moveSpeed = aimWalkSpeed;
        }
        else if (!aiming && isAiming)
        {
            isAiming = false;
            UnequipWeapon();
            if (tankController) tankController.moveSpeed = originalWalkSpeed;
        }

        if (aiming && Input.GetMouseButton(0) && CanShoot() && firingCoroutine == null)
            firingCoroutine = StartCoroutine(ShootingRoutine());
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
        if (melee == null) return;

        // Если враг слишком близко — толчок (даже если не в прицеле!)
        if (isTooCloseToEnemy && closestEnemy != null)
        {
            if (melee.TryMeleeAttack(closestEnemy))  // ← Передаём врага напрямую
            {
                nextFireTime = Time.time + currentFireRate;
                return;
            }
        }

        // Обычная стрельба
        if (currentAmmoInMag > 0)
        {
            Ray ray = new Ray(muzzlePoint.position, muzzlePoint.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 300f, enemyLayerMask))
            {
                float dist = hit.distance;
                if (dist < melee.minShootDistance)
                {
                    var enemy = hit.collider.GetComponent<AdvancedEnemyAI>();
                    if (enemy && melee.TryMeleeAttack(enemy))
                    {
                        nextFireTime = Time.time + currentFireRate;
                        return;
                    }
                }
            }

            currentAmmoInMag--;
            PlayShootSound();

            if (currentWeaponType == InventoryItem.ItemType.Gun)
                ShootLaser();
            else if (currentWeaponType == InventoryItem.ItemType.Pistol)
                ShootBullet();

            nextFireTime = Time.time + currentFireRate;

            if (currentAmmoInMag <= 0 && currentReserveAmmo > 0 && isAiming)
                StartReload();
        }
        else
        {
            PlayEmptyMagSound();
            if (currentReserveAmmo > 0 && isAiming)
                StartReload();
        }
    }

    private void ShootLaser()
    {
        Ray ray = new Ray(muzzlePoint.position, muzzlePoint.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, 300f);

        if (hit && hitInfo.collider.TryGetComponent<AdvancedEnemyAI>(out var enemy))
            Destroy(enemy.gameObject);

        if (laserShotPrefab != null)
        {
            float dist = hit ? hitInfo.distance : 300f;
            var beam = Instantiate(laserShotPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
            beam.transform.localPosition = Vector3.forward * (dist * 0.5f);
            beam.transform.localScale = new Vector3(1, 1, dist);
            Destroy(beam, laserDuration);
        }
    }

    private void ShootBullet()
    {
        var bullet = Instantiate(bulletPrefab, muzzlePoint.position, muzzlePoint.rotation);
        if (bullet.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = muzzlePoint.forward * bulletSpeed;

        if (bullet.TryGetComponent<Bullet>(out var b))
            b.damage = currentDamage;
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

    // ЭКИПИРОВКА
    private void EquipActiveWeapon()
    {
        SetCurrentWeaponStats();
        CreateWeaponModelIfNeeded();

        var muzzle = currentWeaponModel?.transform.Find("Muzzle");
        if (muzzle) muzzlePoint = muzzle;
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
            currentDamage = gunDamage;
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
            currentDamage = pistolDamage;
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

        GameObject prefab = null;
        Vector3 scale = Vector3.one;
        string name = "";

        if (item.type == InventoryItem.ItemType.Gun)
        {
            prefab = gunPrefab;
            scale = gunScale;
            name = "Laser Gun";
        }
        else if (item.type == InventoryItem.ItemType.Pistol)
        {
            prefab = pistolPrefab;
            scale = pistolScale;
            name = "Pistol";
        }

        if (currentWeaponModel) Destroy(currentWeaponModel);
        var go = Instantiate(prefab ?? GameObject.CreatePrimitive(PrimitiveType.Cube), weaponHoldPoint, false);
        go.transform.localScale = scale;
        go.name = name;
        currentWeaponModel = go;
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

    private void UnequipWeapon()
    {
        SaveCurrentAmmo();
        if (currentWeaponModel) Destroy(currentWeaponModel);
        if (currentAimBeam) Destroy(currentAimBeam);
        currentWeaponModel = null;
        currentAimBeam = null;

        if (firingCoroutine != null)
        {
            StopCoroutine(firingCoroutine);
            firingCoroutine = null;
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
        if (currentAmmoInMag >= currentMagazineSize) return;
        if (currentReserveAmmo <= 0)
        {
            PlayEmptyMagSound();
            return;
        }
        if (!isAiming) return;

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

    private void UpdateAimBeam()
    {
        if (!isAiming || aimBeamPrefab == null || muzzlePoint == null)
        {
            if (currentAimBeam) Destroy(currentAimBeam);
            currentAimBeam = null;
            return;
        }

        if (currentAimBeam == null)
            currentAimBeam = Instantiate(aimBeamPrefab, muzzlePoint);

        Ray ray = new Ray(muzzlePoint.position, muzzlePoint.forward);
        float d = Physics.Raycast(ray, out RaycastHit hit, 200f) ? hit.distance : 200f;

        currentAimBeam.transform.position = muzzlePoint.position + muzzlePoint.forward * (d * 0.5f);
        currentAimBeam.transform.localScale = new Vector3(0.02f, 0.02f, d);
        currentAimBeam.transform.rotation = muzzlePoint.rotation;
    }

    public void OnActiveItemChanged()
    {
        SetCurrentWeaponStats();
        if (isAiming) CreateWeaponModelIfNeeded();
    }
}