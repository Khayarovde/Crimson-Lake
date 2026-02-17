using UnityEngine;
using Yarn.Unity;
using DG.Tweening;

public class Interact : MonoBehaviour {
    [SerializeField] public string startNode = "item"; // Имя ноды в .yarn
    [Header("Pickup")]
    [SerializeField] private InventoryItem itemToPickup;
    [SerializeField] private bool destroyObjectAfterPickup = true;

    [Header("Interaction Hint")]
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private float hintScaleMultiplier = 1.1f;
    [SerializeField] private float hintDuration = 0.55f;
    [SerializeField] private bool hintMagnetToInteract = true;
    [SerializeField] private Vector3 hintWorldOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float hintMagnetSpeed = 12f;
    [SerializeField] private bool faceMainCamera = false;

    private Sequence hintSequence;
    private Vector3 hintBaseScale;
    private Vector3 hintBaseLocalPosition;
    private bool hintInitialized;
    private bool hintVisible;
    private bool isCollected;

    private static DialogueRunner cachedRunner;
    private static bool pickupCommandRegistered;
    private static Interact activeInteract;

    private void Awake() {
        if (interactionHint != null) {
            EnsureHintState();
            interactionHint.SetActive(false);
        }
    }

    private void OnDisable() {
        StopHintAnimation();
    }

    private void LateUpdate() {
        if (!hintVisible || interactionHint == null || !hintMagnetToInteract) {
            return;
        }

        var hintTransform = interactionHint.transform;
        Vector3 targetPosition = GetHintTargetPosition();
        float followFactor = 1f - Mathf.Exp(-hintMagnetSpeed * Time.deltaTime);
        hintTransform.position = Vector3.Lerp(hintTransform.position, targetPosition, followFactor);

        if (faceMainCamera && Camera.main != null) {
            hintTransform.forward = Camera.main.transform.forward;
        }
    }

    public void StartDialogue() {
        if (isCollected) {
            return;
        }

        var runner = GetDialogueRunner();
        if (runner != null && !runner.IsDialogueRunning) {
            RegisterYarnCommands(runner);
            activeInteract = this;
            runner.StartDialogue(startNode);
        }
    }

    public void SetInteractionHintVisible(bool isVisible) {
        if (interactionHint == null) {
            return;
        }

        EnsureHintState();

        if (isVisible) {
            if (!interactionHint.activeSelf) {
                interactionHint.SetActive(true);
            }

            if (hintMagnetToInteract) {
                interactionHint.transform.position = GetHintTargetPosition();
            }

            PlayHintAnimation();
            hintVisible = true;
        }
        else {
            hintVisible = false;
            StopHintAnimation();
            interactionHint.SetActive(false);
        }
    }

    private void EnsureHintState() {
        if (hintInitialized || interactionHint == null) {
            return;
        }

        hintBaseScale = interactionHint.transform.localScale;
        hintBaseLocalPosition = interactionHint.transform.localPosition;
        hintInitialized = true;
    }

    private void PlayHintAnimation() {
        if (interactionHint == null) {
            return;
        }

        if (hintSequence != null && hintSequence.IsActive()) {
            return;
        }

        var hintTransform = interactionHint.transform;
        hintTransform.localScale = hintBaseScale;
        if (!hintMagnetToInteract) {
            hintTransform.localPosition = hintBaseLocalPosition;
        }

        hintSequence = DOTween.Sequence();
        hintSequence.Append(hintTransform.DOScale(hintBaseScale * hintScaleMultiplier, hintDuration).SetEase(Ease.InOutSine));
        hintSequence.Append(hintTransform.DOScale(hintBaseScale, hintDuration).SetEase(Ease.InOutSine));
        hintSequence.SetLoops(-1);
        hintSequence.SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void StopHintAnimation() {
        if (hintSequence != null) {
            hintSequence.Kill();
            hintSequence = null;
        }

        if (interactionHint != null && hintInitialized) {
            interactionHint.transform.localScale = hintBaseScale;
            if (!hintMagnetToInteract) {
                interactionHint.transform.localPosition = hintBaseLocalPosition;
            }
        }
    }

    private Vector3 GetHintTargetPosition() {
        return transform.position + hintWorldOffset;
    }

    private static void PickupCurrentItemCommand() {
        if (activeInteract == null) {
            cachedRunner?.VariableStorage?.SetValue("$pickup_success", false);
            Debug.LogWarning("[Interact] Нет активного объекта взаимодействия для подбора предмета.");
            return;
        }

        bool pickupSucceeded = activeInteract.TryPickupAssignedItem();
        cachedRunner?.VariableStorage?.SetValue("$pickup_success", pickupSucceeded);
    }

    private bool TryPickupAssignedItem() {
        if (isCollected) {
            return false;
        }

        if (itemToPickup == null) {
            Debug.LogWarning($"[Interact] На объекте {gameObject.name} не назначен itemToPickup.");
            return false;
        }

        PlayerInventory playerInventory = Object.FindObjectOfType<PlayerInventory>();
        if (playerInventory == null) {
            Debug.LogError("[Interact] PlayerInventory не найден в сцене.");
            return false;
        }

        bool added = playerInventory.AddItemToInventory(itemToPickup);
        if (!added) {
            Debug.LogWarning($"[Interact] Не удалось добавить предмет {itemToPickup.itemName} (инвентарь полон или недоступен).");
            return false;
        }

        isCollected = true;
        SetInteractionHintVisible(false);

        if (destroyObjectAfterPickup) {
            Destroy(gameObject);
        }

        return true;
    }

    private static void RegisterYarnCommands(DialogueRunner runner) {
        if (pickupCommandRegistered && cachedRunner == runner) {
            return;
        }

        cachedRunner = runner;
        cachedRunner.AddCommandHandler("pickup_current_item", PickupCurrentItemCommand);
        pickupCommandRegistered = true;
    }

    private static DialogueRunner GetDialogueRunner() {
        if (cachedRunner == null) {
            cachedRunner = Object.FindObjectOfType<DialogueRunner>();
        }

        return cachedRunner;
    }
}