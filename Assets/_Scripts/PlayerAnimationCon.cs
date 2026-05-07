using UnityEngine;

public class PlayerAnimationCon : MonoBehaviour
{
    // Legs удалён — слои теперь:
    private const int LayerVseTelo = 0;
    private const int LayerRuka    = 1;
    private static readonly int IsWalkingParam = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningParam = Animator.StringToHash("IsRunning");
    private static readonly int IsAimingParam = Animator.StringToHash("IsAiming");
    private static readonly int HParam = Animator.StringToHash("H");
    private static readonly int VParam = Animator.StringToHash("V");

    private Animator anim;
    private TankController controller;
    private WeaponHandler weaponHandler;
    private PlayerInventory playerInventory;
    private bool wasAiming;

    void Start()
    {
        anim            = GetComponent<Animator>();
        controller      = GetComponent<TankController>();
        weaponHandler   = GetComponent<WeaponHandler>();
        playerInventory = GetComponent<PlayerInventory>();
    }

    void Update()
    {
        // ── VseTelo: движение ──────────────────────────────────────
        bool isMoving = controller.MoveInputMagnitude > 0.1f;
        anim.SetBool(IsWalkingParam, isMoving);
        anim.SetBool(IsRunningParam, controller.IsRunning);

        Vector3 localVelocity = transform.InverseTransformDirection(controller.CurrentPlanarVelocity);
        float speed = controller.CurrentPlanarSpeed;
        anim.SetFloat(HParam, speed > 0.01f ? localVelocity.x / speed : 0f, 0.1f, Time.deltaTime);
        anim.SetFloat(VParam, speed > 0.01f ? localVelocity.z / speed : 0f, 0.1f, Time.deltaTime);

        // ── Ruka: прицеливание (читаем из WeaponHandler) ───────────
        bool aiming = weaponHandler != null ? weaponHandler.IsAiming : controller.IsAiming;
        anim.SetBool(IsAimingParam, aiming);

        if (aiming && !wasAiming)
            UpdateAimingLayer();

        if (!aiming && wasAiming)
            Play("New State", LayerRuka);

        wasAiming = aiming;
    }

    // ── Публичный API ──────────────────────────────────────────────

    public void PlayHit()
    {
        if (anim == null) return;
        Play("hit", LayerVseTelo);
    }

    public void PlayGameOver()
    {
        if (anim == null) return;
        // Play("GameOver", LayerVseTelo);
    }

    public void PlayReloadAnimation(InventoryItem.ItemType weaponType, float reloadDuration)
    {
        if (anim == null) return;

        if (weaponType == InventoryItem.ItemType.Pistol)
            Play("PistolReload", LayerRuka);
        else if (weaponType == InventoryItem.ItemType.Gun)
            Play("ShotgunReload", LayerRuka);
    }

    public void PlayShoot(InventoryItem.ItemType weaponType)
    {
        if (anim == null) return;

        if (weaponType == InventoryItem.ItemType.Pistol)
            Play("PistolShooting", LayerRuka);
        else if (weaponType == InventoryItem.ItemType.Gun)
            Play("ShotgunShooting", LayerRuka);
    }

    public void SetAnimationLock(bool locked, string stateName = null)
    {
        if (controller != null)
            controller.SetMovementLock(locked);

        if (anim == null) return;
        if (locked && !string.IsNullOrEmpty(stateName))
            Play(stateName, LayerVseTelo);
    }

    // ── Приватные ──────────────────────────────────────────────────

    private void UpdateAimingLayer()
    {
        if (playerInventory == null || playerInventory.inventoryData == null) return;

        int idx = playerInventory.activeItemIndex;
        if (idx < 0 || idx >= playerInventory.inventoryData.GetSlotCount()) return;

        var item = playerInventory.inventoryData.GetItemAt(idx);
        if (item == null) return;

        if (item.type == InventoryItem.ItemType.Pistol)
            Play("PistolAiming", LayerRuka);
        else if (item.type == InventoryItem.ItemType.Gun)
            Play("ShotgunAiming", LayerRuka);
    }

    private void Play(string stateName, int layer, float transitionDuration = 0.15f)
    {
        anim.CrossFadeInFixedTime(stateName, transitionDuration, layer);
    }

    private void SetFloat(string param, float value, float dampTime = 0.1f)
    {
        anim.SetFloat(param, value, dampTime, Time.deltaTime);
    }

    private void SetBool(string param, bool value)
    {
        anim.SetBool(param, value);
    }
}