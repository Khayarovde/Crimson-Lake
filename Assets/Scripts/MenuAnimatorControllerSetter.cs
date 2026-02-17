using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MenuAnimatorControllerSetter : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController menuAnimatorController;
    [Header("Menu Click Sequence")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string idleOneState = "Idle 1";
    [SerializeField] private string idleTwoState = "Idle 2";
    [SerializeField] private Transform clickTargetRoot;
    [SerializeField] private float clickRayDistance = 200f;
    [SerializeField] private LayerMask clickRayMask = Physics.DefaultRaycastLayers;
    [SerializeField] private int clicksToTrigger = 4;
    [SerializeField] private float clickWindowSeconds = 2f;
    [SerializeField] private float idleBetweenRepeats = 3f;
    [SerializeField] private int maxRepeats = 3;
    [SerializeField] private float quitDelaySeconds = 1f;
    [SerializeField] private float crossFadeTime = 0.1f;
    [SerializeField] private bool useAnimatorPlay = true;
    [SerializeField] private bool debugClicks = false;
    [SerializeField] private bool fadeOnExit = true;
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    private const string MenuAnimatorControllerPath = "Assets/Animate/Phylanc/Player_Menu";
    private float firstClickTime = -1f;
    private int clickCount;
    private bool sequenceRunning;
    private bool continueRequested;
    private bool sequenceCompleted;

    private void Awake()
    {
        ApplyMenuControllerIfNeeded();
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().buildIndex != 0)
            return;

        if (!sequenceRunning)
        {
            if (TryRegisterClick() && clickCount >= clicksToTrigger)
            {
                clickCount = 0;
                if (sequenceCompleted)
                    StartCoroutine(PlayExitSequence());
                else
                    StartCoroutine(PlayClickSequence());
            }

            return;
        }

        if (TryRegisterClick() && clickCount >= clicksToTrigger)
        {
            continueRequested = true;
            clickCount = 0;
        }
    }

    private void ApplyMenuControllerIfNeeded()
    {
        if (SceneManager.GetActiveScene().buildIndex != 0)
            return;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null || menuAnimatorController == null)
            return;

        if (animator.runtimeAnimatorController == menuAnimatorController)
            return;

        animator.runtimeAnimatorController = menuAnimatorController;
    }

    private bool TryRegisterClick()
    {
        if (!Input.GetMouseButtonDown(0))
            return false;

        if (!IsClickOnCharacter())
            return false;

        float now = Time.time;
        if (firstClickTime < 0f || now - firstClickTime > clickWindowSeconds)
        {
            firstClickTime = now;
            clickCount = 0;
        }

        clickCount++;
        if (debugClicks)
            Debug.Log($"Menu click: {clickCount}/{clicksToTrigger}", this);
        return true;
    }

    private bool IsClickOnCharacter()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, clickRayDistance, clickRayMask, QueryTriggerInteraction.Ignore))
            return false;

        Transform root = clickTargetRoot != null ? clickTargetRoot : transform;
        return hit.transform == root || hit.transform.IsChildOf(root);
    }



    private IEnumerator PlayClickSequence()
    {
        if (sequenceRunning)
            yield break;

        sequenceRunning = true;
        continueRequested = false;

        for (int i = 0; i < maxRepeats; i++)
        {
            PlayState(idleOneState);
            yield return WaitForStateComplete(idleOneState);

            if (i == maxRepeats - 1)
                break;

            PlayState(idleState);
            float waitUntil = Time.time + idleBetweenRepeats;
            continueRequested = false;
            while (Time.time < waitUntil)
            {
                if (continueRequested)
                    break;
                yield return null;
            }

            if (!continueRequested)
            {
                sequenceRunning = false;
                yield break;
            }
        }

            sequenceCompleted = true;
            PlayState(idleState);
            sequenceRunning = false;
    }

            private IEnumerator PlayExitSequence()
            {
            if (sequenceRunning)
                yield break;

            sequenceRunning = true;
            PlayState(idleTwoState);
                yield return WaitForStateComplete(idleTwoState);

            if (fadeOnExit)
                yield return FadeToBlack();

            yield return new WaitForSeconds(quitDelaySeconds);

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
            }

    private void PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;
        if (!HasState(stateName))
        {
            if (debugClicks)
                Debug.LogWarning($"Animator state not found: {stateName}", this);
            return;
        }

        if (debugClicks)
            Debug.Log($"Play state: {stateName}", this);

        if (useAnimatorPlay)
            animator.Play(stateName, 0, 0f);
        else
            animator.CrossFadeInFixedTime(stateName, crossFadeTime, 0);
    }

    private bool HasState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return false;

        return animator.HasState(0, Animator.StringToHash(stateName));
    }

    private IEnumerator WaitForStateComplete(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            yield break;

        while (animator.IsInTransition(0))
            yield return null;

        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(stateName) && info.normalizedTime >= 1f && !animator.IsInTransition(0))
                break;
            yield return null;
        }
    }

    private IEnumerator FadeToBlack()
    {
        if (fadeCanvasGroup == null)
            yield break;

        float duration = Mathf.Max(0.01f, fadeDuration);
        float startAlpha = fadeCanvasGroup.alpha;
        float start = Time.time;
        while (Time.time - start < duration)
        {
            float t = Mathf.Clamp01((Time.time - start) / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (menuAnimatorController == null)
            menuAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MenuAnimatorControllerPath);

        ApplyMenuControllerIfNeeded();
    }
#endif
}
