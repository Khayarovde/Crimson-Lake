using UnityEngine;
using DG.Tweening;
using Yarn.Unity;
using UnityEngine.InputSystem;

public class AmmoPickup : MonoBehaviour
{
    [Header("Yarn Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string startNode = "ammo_pickup";
    [SerializeField] private bool triggerYarnOnPickup = true;
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private float hintScaleMultiplier = 1.1f;
    [SerializeField] private float hintDuration = 0.55f;
    [SerializeField] private bool hintMagnetToPickup = true;
    [SerializeField] private Vector3 hintWorldOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float hintMagnetSpeed = 12f;
    [SerializeField] private bool faceMainCamera = false;

    public InventoryItem.ItemType ammoType; // Выбери в инспекторе PistolAmmo/ShotgunAmmo или Pistol/Gun
    public int amount = 30;
    public GameObject pickupEffect;
    [Header("Inventory")]
    [SerializeField] private bool addAmmoItemToInventory = true;
    [SerializeField] private InventoryItem ammoInventoryItem;
    [PickupId]
    [SerializeField] private string pickupId;

    private WeaponHandler targetWeaponHandler;
    private PlayerInventory targetInventory;
    private bool playerInRange;
    private bool pickedUp;
    private Sequence hintSequence;
    private Vector3 hintBaseScale;
    private Vector3 hintBaseLocalPosition;
    private bool hintInitialized;
    private bool hintVisible;

    private static DialogueRunner cachedRunner;
    private static bool pickupCommandRegistered;
    private static AmmoPickup activeAmmoPickup;

    private void Awake()
    {
        if (!string.IsNullOrWhiteSpace(pickupId) && SaveManager.HasPickedUpItem(pickupId))
        {
            pickedUp = true;
            SetInteractionHintVisible(false);
            Destroy(gameObject);
            return;
        }

        if (interactionHint != null)
        {
            EnsureHintState();
            interactionHint.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (activeAmmoPickup == this)
            activeAmmoPickup = null;

        SetInteractionHintVisible(false);
    }

    private void Update()
    {
        if (!playerInRange || pickedUp)
            return;

        if (Input.GetKeyDown(interactKey) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))
        {
            if (triggerYarnOnPickup)
            {
                TryStartDialogue();
            }
            else
            {
                bool success = TryResolvePickup();
                if (success)
                    CompletePickup();
            }
        }

        LateHintUpdate();
    }

    public bool TryPickupFromGamepad()
    {
        if (!playerInRange || pickedUp)
            return false;

        if (triggerYarnOnPickup)
            return TryStartDialogue();

        bool success = TryResolvePickup();
        if (success)
            CompletePickup();

        return success;
    }

    private bool TryStartDialogue()
    {
        if (pickedUp)
            return false;

        DialogueRunner runner = GetDialogueRunner();
        if (runner == null || runner.IsDialogueRunning)
            return false;

        RegisterYarnCommands(runner);
        activeAmmoPickup = this;
        runner.StartDialogue(startNode);
        return true;
    }

    private void LateHintUpdate()
    {
        if (!hintVisible || interactionHint == null || !hintMagnetToPickup)
            return;

        var hintTransform = interactionHint.transform;
        Vector3 targetPosition = GetHintTargetPosition();
        float followFactor = 1f - Mathf.Exp(-hintMagnetSpeed * Time.deltaTime);
        hintTransform.position = Vector3.Lerp(hintTransform.position, targetPosition, followFactor);

        if (faceMainCamera && Camera.main != null)
            hintTransform.forward = Camera.main.transform.forward;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        targetWeaponHandler = other.GetComponent<WeaponHandler>();
        targetInventory = other.GetComponent<PlayerInventory>();
        playerInRange = targetWeaponHandler != null;

        if (playerInRange)
            SetInteractionHintVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        targetWeaponHandler = null;
        targetInventory = null;
        SetInteractionHintVisible(false);
    }

    private bool TryResolvePickup()
    {
        if (targetWeaponHandler == null || pickedUp)
            return false;

        if (addAmmoItemToInventory &&
            targetInventory != null &&
            targetInventory.inventoryData != null &&
            ammoInventoryItem != null &&
            !targetInventory.inventoryData.HasItemType(ammoInventoryItem.type))
        {
            bool itemAdded = targetInventory.AddItemToInventory(ammoInventoryItem);
            if (!itemAdded)
                return false;
        }

        targetWeaponHandler.AddAmmo(ammoType, amount);

        if (!string.IsNullOrWhiteSpace(pickupId))
            SaveManager.MarkItemPickedUp(pickupId);

        return true;
    }

    private void CompletePickup()
    {
        if (pickedUp)
            return;

        pickedUp = true;
        SetInteractionHintVisible(false);

        if (pickupEffect)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    private static void PickupCurrentAmmoCommand()
    {
        if (activeAmmoPickup == null)
        {
            cachedRunner?.VariableStorage?.SetValue("$pickup_success", false);
            Debug.LogWarning("[AmmoPickup] Нет активного объекта для подбора патронов.");
            return;
        }

        bool success = activeAmmoPickup.TryResolvePickup();
        cachedRunner?.VariableStorage?.SetValue("$pickup_success", success);

        if (success)
            activeAmmoPickup.CompletePickup();
    }

    private static void RegisterYarnCommands(DialogueRunner runner)
    {
        if (pickupCommandRegistered && cachedRunner == runner)
            return;

        cachedRunner = runner;
        cachedRunner.AddCommandHandler("pickup_current_ammo", PickupCurrentAmmoCommand);
        pickupCommandRegistered = true;
    }

    private static DialogueRunner GetDialogueRunner()
    {
        if (cachedRunner == null)
            cachedRunner = FindFirstObjectByType<DialogueRunner>();

        return cachedRunner;
    }

    public void SetInteractionHintVisible(bool isVisible)
    {
        if (interactionHint == null)
            return;

        EnsureHintState();

        if (isVisible)
        {
            if (!interactionHint.activeSelf)
                interactionHint.SetActive(true);

            if (hintMagnetToPickup)
                interactionHint.transform.position = GetHintTargetPosition();

            PlayHintAnimation();
            hintVisible = true;
        }
        else
        {
            hintVisible = false;
            StopHintAnimation();
            interactionHint.SetActive(false);
        }
    }

    private void EnsureHintState()
    {
        if (hintInitialized || interactionHint == null)
            return;

        hintBaseScale = interactionHint.transform.localScale;
        hintBaseLocalPosition = interactionHint.transform.localPosition;
        hintInitialized = true;
    }

    private void PlayHintAnimation()
    {
        if (interactionHint == null)
            return;

        if (hintSequence != null && hintSequence.IsActive())
            return;

        var hintTransform = interactionHint.transform;
        hintTransform.localScale = hintBaseScale;
        if (!hintMagnetToPickup)
            hintTransform.localPosition = hintBaseLocalPosition;

        hintSequence = DOTween.Sequence();
        hintSequence.Append(hintTransform.DOScale(hintBaseScale * hintScaleMultiplier, hintDuration).SetEase(Ease.InOutSine));
        hintSequence.Append(hintTransform.DOScale(hintBaseScale, hintDuration).SetEase(Ease.InOutSine));
        hintSequence.SetLoops(-1);
        hintSequence.SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void StopHintAnimation()
    {
        if (hintSequence != null)
        {
            hintSequence.Kill();
            hintSequence = null;
        }

        if (interactionHint != null && hintInitialized)
        {
            interactionHint.transform.localScale = hintBaseScale;
            if (!hintMagnetToPickup)
                interactionHint.transform.localPosition = hintBaseLocalPosition;
        }
    }

    private Vector3 GetHintTargetPosition()
    {
        return transform.position + hintWorldOffset;
    }
}