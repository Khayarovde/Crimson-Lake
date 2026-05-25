using DG.Tweening;
using UnityEngine;
using Yarn.Unity;

public class MedkitPickup : MonoBehaviour
{
    [Header("Yarn Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string startNode = "medkit_pickup";

    [Header("Medkit")]
    [SerializeField] private InventoryItem medkitItem;
    [SerializeField] private MedkitProfile medkitProfile;
    [SerializeField] private bool destroyObjectAfterPickup = true;
    [SerializeField] private GameObject pickupEffect;
    [PickupId]
    [SerializeField] private string pickupId;

    [Header("Interaction Hint")]
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private float hintScaleMultiplier = 1.1f;
    [SerializeField] private float hintDuration = 0.55f;
    [SerializeField] private bool hintMagnetToPickup = true;
    [SerializeField] private Vector3 hintWorldOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float hintMagnetSpeed = 12f;
    [SerializeField] private bool faceMainCamera = false;

    private bool playerInRange;
    private bool pickedUp;
    private PlayerHealth targetPlayerHealth;
    private PlayerInventory targetPlayerInventory;

    private Sequence hintSequence;
    private Vector3 hintBaseScale;
    private Vector3 hintBaseLocalPosition;
    private bool hintInitialized;
    private bool hintVisible;

    private static DialogueRunner cachedRunner;
    private static bool pickupCommandRegistered;
    private static MedkitPickup activeMedkitPickup;

    private void Awake()
    {
        if (!string.IsNullOrWhiteSpace(pickupId) && SaveManager.HasPickedUpItem(pickupId))
        {
            pickedUp = true;
            SetInteractionHintVisible(false);
            if (destroyObjectAfterPickup)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
            return;
        }

        if (interactionHint != null)
        {
            EnsureHintState();
            interactionHint.SetActive(false);
        }

        // Гарантированная регистрация команды Yarn
        EnsureCommandRegistered();
    }

    private void OnDisable()
    {
        if (activeMedkitPickup == this)
            activeMedkitPickup = null;

        SetInteractionHintVisible(false);
    }

    private void Update()
    {
        if (!playerInRange || pickedUp)
            return;

        if (Input.GetKeyDown(interactKey))
            StartDialogue();

        LateHintUpdate();
    }

    public bool TryPickupFromGamepad()
    {
        if (!playerInRange || pickedUp)
            return false;

        bool success = TryResolvePickup();
        if (success)
            CompletePickup();

        return success;
    }

    private void StartDialogue()
    {
        if (pickedUp)
            return;

        DialogueRunner runner = GetDialogueRunner();
        if (runner == null)
        {
            bool successNoYarn = TryResolvePickup();
            if (successNoYarn)
                CompletePickup();
            return;
        }

        if (runner.IsDialogueRunning)
            return;

        RegisterYarnCommands(runner);
        activeMedkitPickup = this;
        runner.StartDialogue(startNode);
    }

    private void LateHintUpdate()
    {
        if (!hintVisible || interactionHint == null || !hintMagnetToPickup)
            return;

        Transform hintTransform = interactionHint.transform;
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

        targetPlayerHealth = other.GetComponent<PlayerHealth>();
        if (targetPlayerHealth == null)
            targetPlayerHealth = other.GetComponentInParent<PlayerHealth>();

        targetPlayerInventory = other.GetComponent<PlayerInventory>();
        if (targetPlayerInventory == null)
            targetPlayerInventory = other.GetComponentInParent<PlayerInventory>();

        playerInRange = targetPlayerInventory != null;

        if (playerInRange)
            SetInteractionHintVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;
        targetPlayerHealth = null;
        targetPlayerInventory = null;
        SetInteractionHintVisible(false);
    }

    private bool TryResolvePickup()
    {
        if (targetPlayerInventory == null || pickedUp)
            return false;

        if (medkitItem == null)
        {
            Debug.LogWarning($"[MedkitPickup] InventoryItem аптечки не назначен на {gameObject.name}.");
            return false;
        }

        bool added = targetPlayerInventory.AddItemToInventory(medkitItem);
        if (!added)
            return false;

        if (!string.IsNullOrWhiteSpace(pickupId))
            SaveManager.MarkItemPickedUp(pickupId);

        Debug.Log($"[MedkitPickup] Аптечка '{medkitItem.itemName}' добавлена в инвентарь.");
        return true;
    }

    private MedkitProfile ResolveProfile()
    {
        if (medkitProfile != null)
            return medkitProfile;

        if (medkitItem != null)
            return medkitItem.medkitProfile;

        return null;
    }

    private void CompletePickup()
    {
        if (pickedUp)
            return;

        pickedUp = true;
        SetInteractionHintVisible(false);

        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        if (destroyObjectAfterPickup)
            Destroy(gameObject);
    }

    private static void PickupCurrentMedkitCommand()
    {
        if (activeMedkitPickup == null)
        {
            cachedRunner?.VariableStorage?.SetValue("$pickup_success", false);
            Debug.LogWarning("[MedkitPickup] Нет активного объекта для подбора аптечки.");
            return;
        }

        bool success = activeMedkitPickup.TryResolvePickup();
        cachedRunner?.VariableStorage?.SetValue("$pickup_success", success);

        if (success)
            activeMedkitPickup.CompletePickup();
    }

    private static void RegisterYarnCommands(DialogueRunner runner)
    {
        if (runner == null) return;
        cachedRunner = runner;
        EnsureCommandRegistered();
    }

    private static void EnsureCommandRegistered()
    {
        if (cachedRunner == null)
            cachedRunner = FindFirstObjectByType<DialogueRunner>();

        if (cachedRunner == null || pickupCommandRegistered)
            return;

        cachedRunner.AddCommandHandler("pickup_current_medkit", PickupCurrentMedkitCommand);
        pickupCommandRegistered = true;
        // Debug.Log("[MedkitPickup] Yarn-команда 'pickup_current_medkit' зарегистрирована");
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

        Transform hintTransform = interactionHint.transform;
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
