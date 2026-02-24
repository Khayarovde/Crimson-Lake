using UnityEngine;
using DG.Tweening;

public class InteractUniversal : MonoBehaviour {
    [Header("Interaction Hint")]
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private float hintScaleMultiplier = 1.1f;
    [SerializeField] private float hintDuration = 0.55f;
    [SerializeField] private AdvancedEnemyAI enemyAI;

    private Sequence hintSequence;
    private Vector3 hintBaseScale;
    private bool hintInitialized;
    private bool playerInsideTrigger;

    private void Awake() {
        if (enemyAI == null) {
            enemyAI = GetComponentInParent<AdvancedEnemyAI>();
        }

        if (interactionHint != null) {
            EnsureHintState();
            interactionHint.SetActive(false);
        }
    }

    private void OnDisable() {
        StopHintAnimation();
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            playerInsideTrigger = true;
            RefreshHintVisibility();
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player")) {
            playerInsideTrigger = false;
            SetInteractionHintVisible(false);
        }
    }

    private void Update() {
        if (!playerInsideTrigger || enemyAI == null) {
            return;
        }

        RefreshHintVisibility();
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

            PlayHintAnimation();
        }
        else {
            StopHintAnimation();
            interactionHint.SetActive(false);
        }
    }

    private void RefreshHintVisibility() {
        if (!playerInsideTrigger) {
            SetInteractionHintVisible(false);
            return;
        }

        bool shouldShow = enemyAI == null || enemyAI.IsStunned;
        SetInteractionHintVisible(shouldShow);
    }

    private void EnsureHintState() {
        if (hintInitialized || interactionHint == null) {
            return;
        }

        hintBaseScale = interactionHint.transform.localScale;
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
        }
    }
}