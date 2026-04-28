using UnityEngine;
using Yarn.Unity;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class Interact : MonoBehaviour {
    public static event Action<InventoryItem, PlayerInventory, Interact> ItemPickedUp;

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
    private static Interact activeInteract;
    private static readonly HashSet<DialogueRunner> runnersWithPickupCommand = new HashSet<DialogueRunner>();

    [Header("Yarn")]
    [SerializeField] private DialogueRunner dialogueRunnerOverride;

    private DialogueRunner activeRunner;
    private Coroutine startDialogueRoutine;

    private const float RunnerResolveTimeoutSeconds = 1.5f;

    private void Awake() {
        if (interactionHint != null) {
            EnsureHintState();
            interactionHint.SetActive(false);
        }
    }

    private void OnDisable() {
        if (activeInteract == this) {
            activeInteract = null;
        }

        if (startDialogueRoutine != null) {
            StopCoroutine(startDialogueRoutine);
            startDialogueRoutine = null;
        }

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

        if (startDialogueRoutine != null) {
            StopCoroutine(startDialogueRoutine);
        }

        startDialogueRoutine = StartCoroutine(StartDialogueWhenRunnerReady());
    }

    private System.Collections.IEnumerator StartDialogueWhenRunnerReady() {
        float elapsed = 0f;

        while (elapsed < RunnerResolveTimeoutSeconds) {
            var runner = GetDialogueRunner();
            if (runner != null) {
                if (!runner.IsDialogueRunning) {
                    RegisterYarnCommands(runner);
                    activeInteract = this;
                    activeRunner = runner;
                    runner.StartDialogue(startNode);
                }

                startDialogueRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fallback for build timing issues when DialogueRunner appears late or is absent.
        bool pickupSucceeded = TryPickupAssignedItem();
        activeRunner?.VariableStorage?.SetValue("$pickup_success", pickupSucceeded);
        if (!pickupSucceeded) {
            Debug.LogWarning($"[Interact] Не удалось запустить диалог и подобрать предмет на {gameObject.name}.");
        }

        startDialogueRoutine = null;
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
        Interact interact = activeInteract;
        if (interact == null) {
            cachedRunner?.VariableStorage?.SetValue("$pickup_success", false);
            Debug.LogWarning("[Interact] Нет активного объекта взаимодействия для подбора предмета.");
            return;
        }

        DialogueRunner runner = interact.activeRunner;
        if (runner == null || !runner.isActiveAndEnabled) {
            runner = interact.GetDialogueRunner();
        }

        bool pickupSucceeded = false;
        try {
            pickupSucceeded = interact.TryPickupAssignedItem();
        }
        catch (Exception ex) {
            Debug.LogError($"[Interact] Ошибка при выполнении команды pickup_current_item: {ex}");
            pickupSucceeded = false;
        }

        runner?.VariableStorage?.SetValue("$pickup_success", pickupSucceeded);
    }

    private bool TryPickupAssignedItem() {
        if (isCollected) {
            return false;
        }

        if (itemToPickup == null) {
            Debug.LogWarning($"[Interact] На объекте {gameObject.name} не назначен itemToPickup.");
            return false;
        }

        PlayerInventory playerInventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
        if (playerInventory == null) {
            Debug.LogError("[Interact] PlayerInventory не найден в сцене.");
            return false;
        }

        bool added = playerInventory.AddItemToInventory(itemToPickup);
        if (!added) {
            Debug.LogWarning($"[Interact] Не удалось добавить предмет {itemToPickup.itemName} (инвентарь полон или недоступен).");
            return false;
        }

        ItemPickedUp?.Invoke(itemToPickup, playerInventory, this);

        isCollected = true;
        SetInteractionHintVisible(false);

        bool hasDiskettePickupFlow = GetComponent<EnemyPickupInteraction>() != null;
        if (destroyObjectAfterPickup && !hasDiskettePickupFlow) {
            Destroy(gameObject);
        }

        return true;
    }

    private static void RegisterYarnCommands(DialogueRunner runner) {
        if (runner == null) {
            return;
        }

        cachedRunner = runner;
        runnersWithPickupCommand.RemoveWhere(r => r == null);
        if (runnersWithPickupCommand.Contains(cachedRunner)) {
            return;
        }

        cachedRunner.AddCommandHandler("pickup_current_item", PickupCurrentItemCommand);
        runnersWithPickupCommand.Add(cachedRunner);
    }

    private DialogueRunner GetDialogueRunner() {
        if (dialogueRunnerOverride != null && dialogueRunnerOverride.isActiveAndEnabled) {
            cachedRunner = dialogueRunnerOverride;
            return cachedRunner;
        }

        if (cachedRunner != null && cachedRunner.isActiveAndEnabled) {
            return cachedRunner;
        }

#if UNITY_2020_1_OR_NEWER
        DialogueRunner[] runners = UnityEngine.Object.FindObjectsByType<DialogueRunner>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
#else
        DialogueRunner[] runners = UnityEngine.Object.FindObjectsOfType<DialogueRunner>();
#endif

        if (runners != null && runners.Length > 0) {
            Scene activeScene = SceneManager.GetActiveScene();
            for (int i = 0; i < runners.Length; i++) {
                DialogueRunner candidate = runners[i];
                if (candidate != null && candidate.isActiveAndEnabled && candidate.gameObject.scene == activeScene) {
                    cachedRunner = candidate;
                    return cachedRunner;
                }
            }

            for (int i = 0; i < runners.Length; i++) {
                DialogueRunner candidate = runners[i];
                if (candidate != null && candidate.isActiveAndEnabled) {
                    cachedRunner = candidate;
                    return cachedRunner;
                }
            }
        }

        cachedRunner = null;
        return null;
    }
}