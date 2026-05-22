using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class FinishingManager : MonoBehaviour
{
    public enum FinishingCameraPlacementMode
    {
        KeepCurrentTransform
    }

    [Header("Scene References")]
    public Camera finishingCam;
    public Canvas finishingCanvas;
    public RawImage finishingRawImage;
    public Camera playerCamera;
    public Transform player;
    public Transform enemy;
    public FinishingCameraPlacementMode finishingCameraPlacementMode = FinishingCameraPlacementMode.KeepCurrentTransform;

    [Header("Animation")]
    public string enemyAnimationState = "Stun";
    public bool playEnemyAnimation = false;
    public string playerAttackTrigger = "death";
    public string playerAnimationState = "attack_stun_enemy";
    public bool interruptPlayerAnimationOnFinishingEnd = true;
    public string playerAnimationInterruptState = "Idle";
    [Range(0f, 0.5f)] public float playerInterruptTransitionDuration = 0.08f;
    [Range(0.05f, 0.95f)] public float effectStartNormalizedTime = 0.5f;
    public bool useAutomaticTiming = true;
    public float customFinishingDuration = 2.5f; // Резервное время на случай, если длину анимации не удастся определить

    [Header("Покадрово для игрока")]
    public bool useFrameBasedPlayerDuration = false;
    [Min(1)] public int playerFinishingFrameCount = 60;
    [Range(1f, 240f)] public float playerFinishingFrameRate = 60f;
    public bool usePlayerClipFrameRate = true;

    [Header("Visual")]
    public string finishingLayerName = "FinishingOnly";
    public Color redOverlayColor = new Color(0.6f, 0f, 0f, 1f);

    [Header("Finishing Rules")]
    public bool requireFrontSide = true;
    [Range(1f, 179f)] public float maxFrontAngle = 85f;

    [Header("Gameplay")]
    [Range(0.1f, 1f)] public float slowMotionScale = 0.5f;

    [Header("Player Control Lock")]
    public bool disablePlayerControlDuringFinishing = true;
    public bool autoDetectPlayerControlScripts = true;
    public List<Behaviour> playerControlScripts = new List<Behaviour>();
    public bool disablePlayerCameraDuringFinishing = true;

    [Header("Hit Effects & Audio")]
    public Collider weaponCollider;
    public GameObject weaponHitEffectPrefab;
    public Transform particleSpawnPoint;
    [Range(0.1f, 10f)] public float particleScale = 3f;
    public bool normalizeParticleVelocityCurves = true;
    public AudioSource hitAudioSource;
    public AudioClip playerHitSound;
    public AudioClip enemyHitSound;

    private Animator playerAnim;
    private Animator enemyAnim;
    private Coroutine automaticSequenceRoutine;
    private readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
    private CameraClearFlags previousClearFlags;
    private Color previousBackgroundColor;
    private int previousCullingMask;
    private bool isFinishingActive;
    private bool usingTemporaryLayer;
    private bool previousPlayerCameraEnabled;
    private readonly List<Behaviour> temporarilyDisabledControls = new List<Behaviour>();
    private RenderTexture runtimeFinishingTexture;
    private FinishingHitDetector hitDetector;

    public bool IsFinishingActive => isFinishingActive;

    private void Awake()
    {
        if (finishingCam != null)
        {
            previousClearFlags = finishingCam.clearFlags;
            previousBackgroundColor = finishingCam.backgroundColor;
            previousCullingMask = finishingCam.cullingMask;
            finishingCam.gameObject.SetActive(false);
        }

        if (finishingCanvas != null)
        {
            finishingCanvas.gameObject.SetActive(false);
        }

        if (finishingRawImage != null)
        {
            finishingRawImage.gameObject.SetActive(false);
        }

        if (weaponCollider != null)
        {
            hitDetector = weaponCollider.GetComponent<FinishingHitDetector>();
            if (hitDetector == null)
            {
                hitDetector = weaponCollider.gameObject.AddComponent<FinishingHitDetector>();
            }
            hitDetector.manager = this;
        }

        ValidateSetup();
    }

    private void Update()
    {
    }

    public void StartFinishing(Transform p, Transform e)
    {
        StartFinishingInternal(p, e, true, false);
    }

    public void StartFinishingImmediate(Transform p, Transform e)
    {
        StartFinishingInternal(p, e, true, true);
    }

    private void StartFinishingInternal(Transform p, Transform e, bool startEffectImmediately, bool forceAutomaticSequence)
    {
        if (isFinishingActive)
        {
            Debug.Log("FinishingManager: Start ignored because finishing is already active.");
            return;
        }

        if (IsBossEnemy(e))
        {
            Debug.LogWarning("FinishingManager: Finishing is blocked for BossEnemy targets.");
            return;
        }

        EndAutomaticRoutine();
        RestoreSceneState();

        player = p;
        enemy = e;

        if (player == null || enemy == null)
        {
            Debug.LogWarning("FinishingManager: player or enemy is not assigned.");
            return;
        }

        playerAnim = player.GetComponent<Animator>();
        enemyAnim = enemy.GetComponent<Animator>();

        if (playerAnim == null || enemyAnim == null)
        {
            Debug.LogWarning("FinishingManager: Animator not found on player or enemy.");
            return;
        }

        if (requireFrontSide && !IsPlayerInEnemyFront(player, enemy))
        {
            Debug.Log("FinishingManager: Finishing blocked because player is behind enemy.");
            return;
        }

        ValidateSetup();
        DisablePlayerControl();
        DisablePlayerCamera();

        PrepareFinishingCamera();

        if (hitDetector != null)
        {
            hitDetector.targetEnemy = enemy;
        }

        if (playEnemyAnimation)
        {
            enemyAnim.Play(enemyAnimationState, 0, 0f);
        }
        playerAnim.ResetTrigger(playerAttackTrigger);
        playerAnim.SetTrigger(playerAttackTrigger);

        Debug.Log("FinishingManager: StartFinishing called. Sequence started.");

        isFinishingActive = true;

        UpdateFinishingCameraTransform();

        if (startEffectImmediately)
        {
            StartFinishingEffect();
        }

        if (useAutomaticTiming || forceAutomaticSequence || useFrameBasedPlayerDuration)
        {
            automaticSequenceRoutine = StartCoroutine(AutomaticSequence(startEffectImmediately));
        }
    }

    public void StartFinishing()
    {
        if (player == null || enemy == null)
        {
            Debug.LogWarning("FinishingManager: StartFinishing() called without assigned player/enemy in Inspector.");
            return;
        }

        StartFinishing(player, enemy);
    }

    public void StartFinishingEffect()
    {
        if (!isFinishingActive)
        {
            Debug.LogWarning("FinishingManager: StartFinishingEffect ignored because finishing is not active.");
            return;
        }

        if (finishingCam != null)
        {
            EnsureRenderTextureHasDepthBuffer();
            finishingCam.gameObject.SetActive(true);
        }
        if (finishingCanvas != null)
        {
            finishingCanvas.gameObject.SetActive(true);
        }
        if (finishingRawImage != null)
        {
            finishingRawImage.gameObject.SetActive(true);
            finishingRawImage.color = Color.white;
        }

        Time.timeScale = slowMotionScale;
        Debug.Log("FinishingManager: Finishing effect ON.");
    }

    public void EndFinishingEffect()
    {
        if (!isFinishingActive)
        {
            Debug.LogWarning("FinishingManager: EndFinishingEffect ignored because finishing is not active.");
            return;
        }

        EndAutomaticRoutine();
        RestoreSceneState();

        player = null;
        enemy = null;
        playerAnim = null;
        enemyAnim = null;

        Debug.Log("FinishingManager: Finishing effect OFF. Scene restored.");
    }

    private IEnumerator AutomaticSequence(bool effectAlreadyStarted)
    {
        // Небольшая пауза, чтобы Animator начал переход
        yield return null;
        yield return null;

        float duration = customFinishingDuration;

        if (useFrameBasedPlayerDuration)
        {
            duration = GetFrameBasedDuration(customFinishingDuration);
        }
        else if (useAutomaticTiming && playerAnim != null)
        {
            // Ждем, пока аниматор действительно перейдет в нужный стейт (до 1 секунды)
            float waitTimer = 0f;
            while (waitTimer < 1f)
            {
                AnimatorStateInfo currentInfo = playerAnim.GetCurrentAnimatorStateInfo(0);
                if (currentInfo.IsName(playerAnimationState))
                {
                    break;
                }
                
                AnimatorStateInfo nextInfo = playerAnim.GetNextAnimatorStateInfo(0);
                if (nextInfo.IsName(playerAnimationState))
                {
                    break;
                }

                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // Пытаемся взять точную длину клипа по названию, иначе берем длину стейта
            float clipLength = GetClipLength(playerAnim, playerAnimationState, -1f);
            if (clipLength > 0f)
            {
                duration = clipLength;
            }
            else
            {
                AnimatorStateInfo stateInfo = playerAnim.GetCurrentAnimatorStateInfo(0);
                duration = stateInfo.length;

                // Если длина анимации слишком короткая (например стейт еще не сменился), берем дефолтное время
                if (duration <= 0.1f)
                {
                    duration = customFinishingDuration;
                }
            }
        }

        if (effectAlreadyStarted)
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            EndFinishingEffect();
            yield break;
        }

        float firstPart = duration * effectStartNormalizedTime;
        float secondPart = duration - firstPart;

        if (firstPart > 0f)
        {
            yield return new WaitForSeconds(firstPart);
        }

        StartFinishingEffect();

        if (secondPart > 0f)
        {
            yield return new WaitForSeconds(secondPart);
        }

        EndFinishingEffect();
    }

    private void PrepareFinishingCamera()
    {
        if (finishingCam == null)
        {
            return;
        }

        previousClearFlags = finishingCam.clearFlags;
        previousBackgroundColor = finishingCam.backgroundColor;
        previousCullingMask = finishingCam.cullingMask;

        finishingCam.clearFlags = CameraClearFlags.SolidColor;
        finishingCam.backgroundColor = redOverlayColor;
        EnsureRenderTextureHasDepthBuffer();

        int targetLayer = LayerMask.NameToLayer(finishingLayerName);
        originalLayers.Clear();
        usingTemporaryLayer = targetLayer >= 0;

        if (usingTemporaryLayer)
        {
            CacheAndSetLayerRecursively(player, targetLayer);
            CacheAndSetLayerRecursively(enemy, targetLayer);
            finishingCam.cullingMask = 1 << targetLayer;
        }
        else
        {
            int playerMask = player != null ? 1 << player.gameObject.layer : 0;
            int enemyMask = enemy != null ? 1 << enemy.gameObject.layer : 0;
            finishingCam.cullingMask = playerMask | enemyMask;
            Debug.LogWarning("FinishingManager: Layer '" + finishingLayerName + "' not found. Using current player/enemy layers.");
        }
    }

    private void RestoreSceneState()
    {
        bool wasFinishingActive = isFinishingActive;

        Time.timeScale = 1f;

        if (finishingCam != null)
        {
            finishingCam.cullingMask = previousCullingMask;
            finishingCam.clearFlags = previousClearFlags;
            finishingCam.backgroundColor = previousBackgroundColor;
            finishingCam.gameObject.SetActive(false);
        }

        if (finishingCanvas != null)
        {
            finishingCanvas.gameObject.SetActive(false);
        }

        if (finishingRawImage != null)
        {
            finishingRawImage.gameObject.SetActive(false);
        }

        if (usingTemporaryLayer)
        {
            RestoreOriginalLayers();
        }

        RestorePlayerControl();
        RestorePlayerCamera();

        if (wasFinishingActive)
        {
            InterruptPlayerFinishingAnimation();
        }

        if (hitDetector != null)
        {
            hitDetector.targetEnemy = null;
        }

        isFinishingActive = false;
    }

    private void CacheAndSetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        if (!originalLayers.ContainsKey(root))
        {
            originalLayers[root] = root.gameObject.layer;
        }
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
        {
            CacheAndSetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void RestoreOriginalLayers()
    {
        foreach (var pair in originalLayers)
        {
            if (pair.Key != null)
            {
                pair.Key.gameObject.layer = pair.Value;
            }
        }
        originalLayers.Clear();
    }

    private void EndAutomaticRoutine()
    {
        if (automaticSequenceRoutine != null)
        {
            StopCoroutine(automaticSequenceRoutine);
            automaticSequenceRoutine = null;
        }
    }

    private void UpdateFinishingCameraTransform()
    {
        if (finishingCam == null || player == null || enemy == null)
        {
            return;
        }

        if (finishingCameraPlacementMode == FinishingCameraPlacementMode.KeepCurrentTransform)
        {
            return;
        }
    }

    private bool IsBossEnemy(Transform target)
    {
        return target != null && target.GetComponentInParent<BossEnemy>() != null;
    }

    private float GetClipLength(Animator animator, string clipName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return fallback;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == clipName)
            {
                return clips[i].length;
            }
        }

        return fallback;
    }

    private float GetClipFrameRate(Animator animator, string clipName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return fallback;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == clipName)
            {
                return clips[i].frameRate;
            }
        }

        return fallback;
    }

    private float GetFrameBasedDuration(float fallback)
    {
        int frameCount = Mathf.Max(1, playerFinishingFrameCount);
        float frameRate = playerFinishingFrameRate;

        if (usePlayerClipFrameRate)
        {
            float clipFrameRate = GetClipFrameRate(playerAnim, playerAnimationState, -1f);
            if (clipFrameRate > 0f)
            {
                frameRate = clipFrameRate;
            }
        }

        frameRate = Mathf.Max(1f, frameRate);
        float duration = frameCount / frameRate;

        if (duration <= 0f)
        {
            return fallback;
        }

        return duration;
    }

    private void InterruptPlayerFinishingAnimation()
    {
        if (!interruptPlayerAnimationOnFinishingEnd || playerAnim == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(playerAttackTrigger))
        {
            playerAnim.ResetTrigger(playerAttackTrigger);
        }

        if (string.IsNullOrWhiteSpace(playerAnimationInterruptState))
        {
            return;
        }

        if (TryCrossFadeStateByName(playerAnim, playerAnimationInterruptState, playerInterruptTransitionDuration))
        {
            return;
        }

        Debug.LogWarning("FinishingManager: Interrupt state '" + playerAnimationInterruptState + "' not found on player Animator.");
    }

    private bool TryCrossFadeStateByName(Animator animator, string stateName, float transitionDuration)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int stateHash = Animator.StringToHash(stateName);
        for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
        {
            if (animator.HasState(layerIndex, stateHash))
            {
                animator.CrossFade(stateHash, Mathf.Max(0f, transitionDuration), layerIndex, 0f);
                return true;
            }

            string layerQualifiedState = animator.GetLayerName(layerIndex) + "." + stateName;
            int layerQualifiedHash = Animator.StringToHash(layerQualifiedState);
            if (animator.HasState(layerIndex, layerQualifiedHash))
            {
                animator.CrossFade(layerQualifiedHash, Mathf.Max(0f, transitionDuration), layerIndex, 0f);
                return true;
            }
        }

        return false;
    }

    private float GetCurrentStateRemainingTime(Animator animator, float fallback)
    {
        if (animator == null)
        {
            return fallback;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        float speed = Mathf.Abs(stateInfo.speed * animator.speed);
        if (speed < 0.0001f)
        {
            speed = 1f;
        }

        float normalizedLoop = stateInfo.normalizedTime;
        float normalizedInCurrentLoop = normalizedLoop - Mathf.Floor(normalizedLoop);
        float remainingNormalized = Mathf.Clamp01(1f - normalizedInCurrentLoop);
        float remaining = stateInfo.length * remainingNormalized / speed;

        if (remaining <= 0f)
        {
            return fallback;
        }

        return remaining;
    }

    private bool IsPlayerInEnemyFront(Transform playerTransform, Transform enemyTransform)
    {
        if (playerTransform == null || enemyTransform == null)
        {
            return false;
        }

        Vector3 toPlayer = playerTransform.position - enemyTransform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        float angle = Vector3.Angle(enemyTransform.forward, toPlayer.normalized);
        return angle <= Mathf.Clamp(maxFrontAngle, 1f, 179f);
    }

    private void EnsureRenderTextureHasDepthBuffer()
    {
        if (finishingCam == null || finishingCam.targetTexture == null)
        {
            return;
        }

        RenderTexture current = finishingCam.targetTexture;
        if (current.depth > 0)
        {
            return;
        }

        RenderTexture replacement = new RenderTexture(
            Mathf.Max(1, current.width),
            Mathf.Max(1, current.height),
            24,
            current.format
        );

        replacement.name = current.name + "_Depth";
        replacement.antiAliasing = current.antiAliasing;
        replacement.wrapMode = current.wrapMode;
        replacement.filterMode = current.filterMode;
        replacement.useMipMap = current.useMipMap;
        replacement.autoGenerateMips = current.autoGenerateMips;
        replacement.Create();

        runtimeFinishingTexture = replacement;
        finishingCam.targetTexture = replacement;

        if (finishingRawImage != null)
        {
            finishingRawImage.texture = replacement;
        }

        Debug.Log("FinishingManager: Replaced finishing RenderTexture with depth-enabled texture to satisfy Render Graph.");
    }

    private void OnDisable()
    {
        EndAutomaticRoutine();
        RestoreSceneState();
    }

    private void OnDestroy()
    {
        if (runtimeFinishingTexture != null)
        {
            runtimeFinishingTexture.Release();
            runtimeFinishingTexture = null;
        }
    }

    private void ValidateSetup()
    {
        if (finishingCam == null)
        {
            Debug.LogWarning("FinishingManager: finishingCam is not assigned.");
        }

        if (finishingCanvas == null)
        {
            Debug.LogWarning("FinishingManager: finishingCanvas is not assigned.");
        }

        if (finishingRawImage == null)
        {
            Debug.LogWarning("FinishingManager: finishingRawImage is not assigned.");
        }

        if (finishingCam != null && finishingCam.targetTexture == null)
        {
            Debug.LogWarning("FinishingManager: finishingCam.targetTexture is empty. Assign your RenderTexture.");
        }
        else
        {
            EnsureRenderTextureHasDepthBuffer();
        }

        if (finishingRawImage != null && finishingRawImage.texture == null)
        {
            Debug.LogWarning("FinishingManager: finishingRawImage.texture is empty. Assign the same RenderTexture as finishingCam.targetTexture.");
        }

        if (playerCamera == null && player != null)
        {
            playerCamera = player.GetComponentInChildren<Camera>(true);
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        if (useFrameBasedPlayerDuration && playerFinishingFrameCount < 1)
        {
            playerFinishingFrameCount = 1;
        }
    }

    private void DisablePlayerControl()
    {
        if (!disablePlayerControlDuringFinishing || player == null)
        {
            return;
        }

        temporarilyDisabledControls.Clear();

        if (autoDetectPlayerControlScripts)
        {
            TryAutoDetectPlayerControlScripts();
        }

        for (int i = 0; i < playerControlScripts.Count; i++)
        {
            Behaviour controlScript = playerControlScripts[i];
            if (controlScript == null)
            {
                continue;
            }

            if (controlScript.enabled)
            {
                controlScript.enabled = false;
                temporarilyDisabledControls.Add(controlScript);
            }
        }
    }

    private void RestorePlayerControl()
    {
        for (int i = 0; i < temporarilyDisabledControls.Count; i++)
        {
            Behaviour controlScript = temporarilyDisabledControls[i];
            if (controlScript != null)
            {
                controlScript.enabled = true;
            }
        }

        temporarilyDisabledControls.Clear();
    }

    private void DisablePlayerCamera()
    {
        if (!disablePlayerCameraDuringFinishing)
        {
            return;
        }

        if (playerCamera == null)
        {
            if (player != null)
            {
                playerCamera = player.GetComponentInChildren<Camera>(true);
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        if (playerCamera == null || playerCamera == finishingCam)
        {
            return;
        }

        previousPlayerCameraEnabled = playerCamera.enabled;
        playerCamera.enabled = false;
    }

    private void RestorePlayerCamera()
    {
        if (!disablePlayerCameraDuringFinishing)
        {
            return;
        }

        if (playerCamera == null || playerCamera == finishingCam)
        {
            return;
        }

        playerCamera.enabled = previousPlayerCameraEnabled;
    }

    private void TryAutoDetectPlayerControlScripts()
    {
        if (player == null)
        {
            return;
        }

        Behaviour[] allBehaviours = player.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            Behaviour behaviour = allBehaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            if (behaviour == this || behaviour is Animator)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name.ToLowerInvariant();
            bool looksLikeControl =
                typeName.Contains("controller") ||
                typeName.Contains("movement") ||
                typeName.Contains("input") ||
                typeName.Contains("weapon") ||
                typeName.Contains("interaction") ||
                typeName.Contains("look");

            if (!looksLikeControl)
            {
                continue;
            }

            if (!playerControlScripts.Contains(behaviour))
            {
                playerControlScripts.Add(behaviour);
            }
        }
    }

    public void OnWeaponHit(Vector3 hitPosition)
    {
        if (weaponHitEffectPrefab != null)
        {
            Vector3 spawnPosition;
            if (particleSpawnPoint != null)
            {
                spawnPosition = particleSpawnPoint.position;
            }
            else if (weaponCollider != null)
            {
                spawnPosition = weaponCollider.bounds.center;
            }
            else
            {
                spawnPosition = hitPosition;
            }
            
            // Создаем копию префаба (партикла)
            GameObject effectInstance = Instantiate(weaponHitEffectPrefab, spawnPosition, Quaternion.LookRotation(Vector3.up));

            // Задаем ему размер
            effectInstance.transform.localScale = Vector3.one * particleScale;

            if (normalizeParticleVelocityCurves)
            {
                NormalizeVelocityCurves(effectInstance);
            }

            // Переносим спавн-объект на нужный слой, чтобы камера добивания его видела
            if (usingTemporaryLayer)
            {
                int tLayer = LayerMask.NameToLayer(finishingLayerName);
                if (tLayer >= 0)
                {
                    CacheAndSetLayerRecursively(effectInstance.transform, tLayer);
                }
            }
            else if (player != null)
            {
                effectInstance.layer = player.gameObject.layer;
            }

            // Уничтожаем объект после 3 секунд, чтобы не засорял сцену
            Destroy(effectInstance, 3f);
        }

        if (hitAudioSource != null)
        {
            if (playerHitSound != null)
            {
                hitAudioSource.PlayOneShot(playerHitSound);
            }

            if (enemyHitSound != null)
            {
                hitAudioSource.PlayOneShot(enemyHitSound);
            }
        }
    }

    private void NormalizeVelocityCurves(GameObject effectInstance)
    {
        if (effectInstance == null)
        {
            return;
        }

        ParticleSystem[] systems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null)
            {
                continue;
            }

            var velocity = system.velocityOverLifetime;
            if (!velocity.enabled)
            {
                continue;
            }

            ParticleSystem.MinMaxCurve x = velocity.x;
            ParticleSystem.MinMaxCurve y = velocity.y;
            ParticleSystem.MinMaxCurve z = velocity.z;

            if (x.mode != y.mode || x.mode != z.mode)
            {
                velocity.y = x;
                velocity.z = x;
            }
        }
    }
}

public class FinishingHitDetector : MonoBehaviour
{
    [HideInInspector] public Transform targetEnemy;
    [HideInInspector] public FinishingManager manager;

    private void OnTriggerEnter(Collider other)
    {
//        CheckHit(other.transform, other.ClosestPoint(transform.position));
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckHit(collision.transform, collision.contacts[0].point);
    }

    private void CheckHit(Transform hitTransform, Vector3 hitPoint)
    {
        if (targetEnemy != null && manager != null && manager.IsFinishingActive)
        {
            if (hitTransform == targetEnemy || hitTransform.IsChildOf(targetEnemy))
            {
                manager.OnWeaponHit(hitPoint);
                targetEnemy = null; // Сброс, чтобы не срабатывало несколько раз за одно добивание
            }
        }
    }
}