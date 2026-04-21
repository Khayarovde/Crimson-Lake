using UnityEngine;
using DG.Tweening;

public class InteractUniversal : MonoBehaviour {
    [Header("Interaction Hint")]
    [SerializeField] private GameObject interactionHint;
    [SerializeField] private float clickPressScale = 0.88f;
    [SerializeField] private float clickPressDuration = 0.08f;
    [SerializeField] private float clickReleaseDuration = 0.12f;
    [SerializeField] private float clickPauseDuration = 0.3f;
    [SerializeField] private bool hintMagnetToInteract = true;
    [SerializeField] private Vector3 hintWorldOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float hintMagnetSpeed = 12f;
    [SerializeField] private bool faceMainCamera = false;
    [Header("Enemy Condition")]
    [SerializeField] private AdvancedEnemyAI enemyAI;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private string enemyStunStateName = "Stun";

    private Sequence hintSequence;
    private Vector3 hintBaseScale;
    private Vector3 hintBaseLocalPosition;
    private bool hintInitialized;
    private bool hintVisible;
    private bool playerInsideTrigger;
    private int enemyStunStateHash;

    private void Awake() {
        if (enemyAI == null) {
            enemyAI = GetComponentInParent<AdvancedEnemyAI>();
        }

        ResolveEnemyAnimator();

        enemyStunStateHash = Animator.StringToHash(enemyStunStateName);

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

    private void Update() {
        if (!playerInsideTrigger || enemyAI == null) {
            return;
        }

        bool shouldBeVisible = IsEnemyInStunState();
        if (shouldBeVisible != hintVisible) {
            SetInteractionHintVisible(shouldBeVisible);
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag("Player")) {
            return;
        }

        playerInsideTrigger = true;
        RefreshHintVisibility();
    }

    private void OnTriggerStay(Collider other) {
        if (!other.CompareTag("Player")) {
            return;
        }

        playerInsideTrigger = true;
        RefreshHintVisibility();
    }

    private void OnTriggerExit(Collider other) {
        if (!other.CompareTag("Player")) {
            return;
        }

        playerInsideTrigger = false;
        SetInteractionHintVisible(false);
    }

    public void SetInteractionHintVisible(bool isVisible) {
        if (interactionHint == null) {
            return;
        }

        if (isVisible && !CanShowHintForCurrentState()) {
            isVisible = false;
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

    private bool CanShowHintForCurrentState() {
        if (!RequiresStunCondition()) {
            return true;
        }

        return IsEnemyInStunState();
    }

    private void RefreshHintVisibility() {
        if (!playerInsideTrigger) {
            SetInteractionHintVisible(false);
            return;
        }

        SetInteractionHintVisible(CanShowHintForCurrentState());
    }

    private bool RequiresStunCondition() {
        return enemyAI != null || enemyAnimator != null || CompareTag("Enemy");
    }

    private bool IsEnemyInStunState() {
        ResolveEnemyAnimator();
        if (enemyAnimator == null) {
            return false;
        }

        AnimatorStateInfo baseLayerState = enemyAnimator.GetCurrentAnimatorStateInfo(0);
        if (baseLayerState.shortNameHash == enemyStunStateHash) {
            return true;
        }

        if (baseLayerState.IsName($"Base Layer.{enemyStunStateName}") || baseLayerState.IsName(enemyStunStateName)) {
            return true;
        }

        if (enemyAnimator.IsInTransition(0)) {
            AnimatorStateInfo nextState = enemyAnimator.GetNextAnimatorStateInfo(0);
            if (nextState.shortNameHash == enemyStunStateHash) {
                return true;
            }

            if (nextState.IsName($"Base Layer.{enemyStunStateName}") || nextState.IsName(enemyStunStateName)) {
                return true;
            }
        }

        return false;
    }

    private void ResolveEnemyAnimator() {
        if (enemyAnimator != null) {
            return;
        }

        if (enemyAI != null) {
            enemyAnimator = enemyAI.GetComponent<Animator>();
            if (enemyAnimator == null) {
                enemyAnimator = enemyAI.GetComponentInChildren<Animator>(true);
            }
        }

        if (enemyAnimator == null) {
            enemyAnimator = GetComponentInParent<Animator>();
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

        float pressScale = Mathf.Clamp(clickPressScale, 0.1f, 1f);
        float pressDuration = Mathf.Max(0.01f, clickPressDuration);
        float releaseDuration = Mathf.Max(0.01f, clickReleaseDuration);
        float pauseDuration = Mathf.Max(0f, clickPauseDuration);
        Vector3 pressedScale = hintBaseScale * pressScale;

        hintSequence = DOTween.Sequence();
        hintSequence.Append(hintTransform.DOScale(pressedScale, pressDuration).SetEase(Ease.InOutQuad));
        hintSequence.Append(hintTransform.DOScale(hintBaseScale, releaseDuration).SetEase(Ease.OutBack));
        hintSequence.AppendInterval(pauseDuration);
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
}
