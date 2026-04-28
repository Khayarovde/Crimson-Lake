using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class TriggerImageHintByZone : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject imageCanvas;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";

    [Header("Click Animation (DOTween)")]
    [SerializeField] private float clickScaleMultiplier = 1.12f;
    [SerializeField] private float clickDuration = 0.28f;
    [SerializeField] private Ease clickEase = Ease.InOutSine;

    [Header("Enemy Condition")]
    [SerializeField] private string stunStateName = "Stun";
    [SerializeField] private bool requireFrontAngleForHint = true;
    [SerializeField, Range(1f, 179f)] private float finisherFrontMaxAngle = 85f;

    private Sequence clickSequence;
    private Vector3 baseScale;
    private bool hasBaseScale;
    private bool playerInside;
    private Transform playerTransform;
    private bool isEnemyObject;
    private AdvancedEnemyAI advancedEnemy;
    private Enemytest enemyTest;
    private BossEnemy bossEnemy;
    private Animator enemyAnimator;
    private int stunStateHash;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();

        advancedEnemy = GetComponentInParent<AdvancedEnemyAI>();
        enemyTest = GetComponentInParent<Enemytest>();
        bossEnemy = GetComponentInParent<BossEnemy>();
        isEnemyObject = advancedEnemy != null || enemyTest != null || bossEnemy != null;
        if (isEnemyObject)
            enemyAnimator = GetComponentInParent<Animator>();

        stunStateHash = Animator.StringToHash(stunStateName);

        if (imageCanvas != null)
        {
            baseScale = imageCanvas.transform.localScale;
            hasBaseScale = true;
            imageCanvas.SetActive(false);
        }
    }

    private void OnDisable()
    {
        HideImage();
    }

    private void Update()
    {
        if (!playerInside || imageCanvas == null)
        {
            return;
        }

        bool shouldShow = ShouldShowHint();
        if (shouldShow && !imageCanvas.activeSelf)
        {
            ShowImage();
        }
        else if (!shouldShow && imageCanvas.activeSelf)
        {
            HideImage();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        playerInside = true;
        playerTransform = other.transform;

        if (ShouldShowHint())
        {
            ShowImage();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        playerInside = false;
        playerTransform = null;
        HideImage();
    }

    private bool ShouldShowHint()
    {
        if (!playerInside)
        {
            return false;
        }

        if (!isEnemyObject)
        {
            return true;
        }

        if (requireFrontAngleForHint && !IsPlayerInEnemyFront())
        {
            return false;
        }

        if (advancedEnemy != null)
        {
            return advancedEnemy.CanBeFinished();
        }

        if (bossEnemy != null)
        {
            return bossEnemy.CanBeFinished();
        }

        if (enemyTest != null)
        {
            return enemyTest.CanBeFinished();
        }

        if (enemyAnimator == null)
        {
            return false;
        }

        AnimatorStateInfo state = enemyAnimator.GetCurrentAnimatorStateInfo(0);
        return state.shortNameHash == stunStateHash || state.IsName($"Base Layer.{stunStateName}");
    }

    private bool IsPlayerInEnemyFront()
    {
        if (playerTransform == null)
            return false;

        Transform enemyRoot = null;
        if (advancedEnemy != null)
            enemyRoot = advancedEnemy.transform;
        else if (bossEnemy != null)
            enemyRoot = bossEnemy.transform;
        else if (enemyTest != null)
            enemyRoot = enemyTest.transform;
        else if (enemyAnimator != null)
            enemyRoot = enemyAnimator.transform;

        if (enemyRoot == null)
            return false;

        Vector3 toPlayer = playerTransform.position - enemyRoot.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
            return true;

        float angle = Vector3.Angle(enemyRoot.forward, toPlayer.normalized);
        return angle <= Mathf.Clamp(finisherFrontMaxAngle, 1f, 179f);
    }

    private void ShowImage()
    {
        if (imageCanvas == null)
        {
            return;
        }

        if (!hasBaseScale)
        {
            baseScale = imageCanvas.transform.localScale;
            hasBaseScale = true;
        }

        imageCanvas.SetActive(true);
        StartClickAnimation();
    }

    private void HideImage()
    {
        StopClickAnimation();

        if (imageCanvas != null)
        {
            imageCanvas.SetActive(false);
        }
    }

    private void StartClickAnimation()
    {
        if (imageCanvas == null)
        {
            return;
        }

        var target = imageCanvas.transform;

        StopClickAnimation();
        target.localScale = baseScale;

        clickSequence = DOTween.Sequence();
        clickSequence.Append(target.DOScale(baseScale * clickScaleMultiplier, clickDuration).SetEase(clickEase));
        clickSequence.Append(target.DOScale(baseScale, clickDuration).SetEase(clickEase));
        clickSequence.SetLoops(-1);
        clickSequence.SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void StopClickAnimation()
    {
        if (clickSequence != null)
        {
            clickSequence.Kill();
            clickSequence = null;
        }

        if (imageCanvas != null && hasBaseScale)
        {
            imageCanvas.transform.localScale = baseScale;
        }
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }

        boxCollider.isTrigger = true;
    }
}