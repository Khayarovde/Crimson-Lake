using UnityEngine;
using Yarn.Unity;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class YarnDialogueInteract : MonoBehaviour {

    [Header("Yarn Settings")]
    [SerializeField] private string startNode = "Start"; // Имя ноды в .yarn файле
    [SerializeField] private DialogueRunner dialogueRunnerOverride;

    [Header("Monster Activation")]
    [SerializeField] private BloodFleshMonsster monsterToActivate; // Ссылка на монстра для активации

    [Header("Trigger & Input")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    private bool isInTriggerZone = false;
    private bool hasBeenActivated = false; // Флаг для предотвращения повторного взаимодействия

    [Header("Canvas UI")]
    [SerializeField] private Canvas interactionCanvas; // Canvas для отображения подсказки
    [SerializeField] private Image hintImage; // Image компонент для подсказки
    [SerializeField] private float hintScaleDuration = 0.5f;
    [SerializeField] private float hintScaleMultiplier = 1.1f;

    private Sequence hintSequence;
    private Vector3 hintBaseScale;
    private bool hintAnimating;

    private static DialogueRunner cachedRunner;
    private DialogueRunner activeRunner;
    private Coroutine startDialogueRoutine;

    private const float RunnerResolveTimeoutSeconds = 1.5f;

    private void Awake() {
        if (hintImage != null) {
            hintBaseScale = hintImage.transform.localScale;
            HideHint();
        }

        if (interactionCanvas != null) {
            interactionCanvas.enabled = false;
        }
    }

    private void OnDisable() {
        if (startDialogueRoutine != null) {
            StopCoroutine(startDialogueRoutine);
            startDialogueRoutine = null;
        }

        isInTriggerZone = false;
        StopHintAnimation();
        HideHint();
    }

    private void Update() {
        if (!isInTriggerZone || hasBeenActivated) {
            return;
        }

        if (Input.GetKeyDown(interactionKey)) {
            StartDialogue();
        }
    }

    private void OnTriggerEnter(Collider collision) {
        if (collision.CompareTag("Player")) {
            isInTriggerZone = true;
            if (!hasBeenActivated) {
                ShowHint();
            }
        }
    }

    private void OnTriggerExit(Collider collision) {
        if (collision.CompareTag("Player")) {
            isInTriggerZone = false;
            HideHint();
        }
    }

    public void StartDialogue() {
        if (hasBeenActivated) {
            Debug.LogWarning($"[YarnDialogueInteract] Диалог уже был проигран на {gameObject.name}. Повторное взаимодействие невозможно.");
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
                    hasBeenActivated = true;
                    activeRunner = runner;
                    
                    // Регистрируем команду activateMonster
                    RegisterYarnCommands(activeRunner);
                    
                    activeRunner.StartDialogue(startNode);
                    HideHint();
                }

                startDialogueRoutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning($"[YarnDialogueInteract] Не удалось найти DialogueRunner для запуска диалога на {gameObject.name}.");
        startDialogueRoutine = null;
    }

    private void RegisterYarnCommands(DialogueRunner runner) {
        if (runner == null) {
            return;
        }

        runner.AddCommandHandler("activateMonster", ActivateMonsterCommand);
    }

    private void ActivateMonsterCommand() {
        if (monsterToActivate != null) {
            Debug.Log($"[YarnDialogueInteract] Команда activateMonster выполнена. Активируем монстра: {monsterToActivate.gameObject.name}");
            monsterToActivate.PlayAttackAnimation();
        }
        else {
            Debug.LogWarning("[YarnDialogueInteract] Monster To Activate не назначена в инспектор!");
        }
    }

    private void ShowHint() {
        if (hintImage == null || interactionCanvas == null) {
            return;
        }

        interactionCanvas.enabled = true;
        hintImage.gameObject.SetActive(true);
        PlayHintAnimation();
    }

    private void HideHint() {
        if (interactionCanvas == null) {
            return;
        }

        StopHintAnimation();
        if (hintImage != null) {
            hintImage.gameObject.SetActive(false);
        }

        interactionCanvas.enabled = false;
    }

    private void PlayHintAnimation() {
        if (hintImage == null || hintAnimating) {
            return;
        }

        hintImage.transform.localScale = hintBaseScale;

        hintSequence = DOTween.Sequence();
        hintSequence.Append(hintImage.transform.DOScale(hintBaseScale * hintScaleMultiplier, hintScaleDuration).SetEase(Ease.InOutSine));
        hintSequence.Append(hintImage.transform.DOScale(hintBaseScale, hintScaleDuration).SetEase(Ease.InOutSine));
        hintSequence.SetLoops(-1);
        hintSequence.SetLink(gameObject, LinkBehaviour.KillOnDisable);

        hintAnimating = true;
    }

    private void StopHintAnimation() {
        if (hintSequence != null) {
            hintSequence.Kill();
            hintSequence = null;
        }

        hintAnimating = false;

        if (hintImage != null) {
            hintImage.transform.localScale = hintBaseScale;
        }
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
