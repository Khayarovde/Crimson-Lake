using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class SurfaceTagCameraBinding
{
    public string surfaceTag;
    public Behaviour virtualCamera;
}

[RequireComponent(typeof(Collider))]
public class SurfaceTagCameraSwitcher : MonoBehaviour
{
    private static readonly HashSet<Behaviour> RegisteredCameras = new HashSet<Behaviour>();

    [Header("Tag -> Camera")]
    [SerializeField] private List<SurfaceTagCameraBinding> tagCameraBindings = new List<SurfaceTagCameraBinding>();

    [Header("Default")]
    [SerializeField] private Behaviour defaultCamera;
    [SerializeField] private bool useDefaultCameraWhenNoTaggedSurface = true;

    [Header("Switch")]
    [SerializeField] private bool instantSwitchWithoutBlend = true;

    [Header("Cinemachine")]
    [SerializeField] private bool allowCinemachineBrainTargets = false;

    [Header("Fade Transition")]
    [SerializeField] private bool useFadeTransition = true;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private int fadeCanvasSortOrder = 10000;

    [Header("Cursor Camera Rotation")]
    [SerializeField] private bool disableCameraRotationByCursor;

    [Header("Camera Rotation Controllers")]
    [SerializeField] private List<Behaviour> mouseLookControllers = new List<Behaviour>();

    [Header("Detection")]
    [SerializeField] private bool useCollisionContacts = true;
    [SerializeField] private bool useTriggerContacts = true;

    [Header("Startup")]
    [SerializeField] private bool forceDeterministicStartupCamera = true;
    [SerializeField] private bool evaluateSurfaceOnStartup = true;
    [SerializeField] private float startupRaycastDistance = 3f;
    [SerializeField] private float startupRaycastHeight = 0.2f;
    [SerializeField] private LayerMask startupSurfaceLayers = ~0;
    [SerializeField] private QueryTriggerInteraction startupTriggerInteraction = QueryTriggerInteraction.Collide;

    private readonly List<Collider> activeSurfaceContacts = new List<Collider>();
    private Behaviour currentActiveCamera;
    private Coroutine startupEvaluateCoroutine;
    private Coroutine fadeSwitchCoroutine;
    private Behaviour pendingTargetCamera;
    private CanvasGroup fadeCanvasGroup;

    private void OnEnable()
    {
        RegisterManagedCameras();

        if (forceDeterministicStartupCamera)
        {
            ForceStartupCamera();
        }
        else
        {
            EnsureAnyRegisteredCameraActive();
        }

        if (evaluateSurfaceOnStartup)
        {
            startupEvaluateCoroutine = StartCoroutine(EvaluateSurfaceAtStartupNextFrame());
        }
    }

    private void OnDisable()
    {
        if (startupEvaluateCoroutine != null)
        {
            StopCoroutine(startupEvaluateCoroutine);
            startupEvaluateCoroutine = null;
        }

        if (fadeSwitchCoroutine != null)
        {
            StopCoroutine(fadeSwitchCoroutine);
            fadeSwitchCoroutine = null;
        }

        pendingTargetCamera = null;

        activeSurfaceContacts.Clear();
        UnregisterManagedCameras();
    }

    private IEnumerator EvaluateSurfaceAtStartupNextFrame()
    {
        yield return null;
        startupEvaluateCoroutine = null;

        if (!isActiveAndEnabled)
        {
            yield break;
        }

        if (TryGetStartupSurfaceContact(out Collider startupSurface))
        {
            RegisterContact(startupSurface);
            yield break;
        }

        EvaluateAndApplyCamera();
    }

    private bool TryGetStartupSurfaceContact(out Collider startupSurface)
    {
        startupSurface = null;

        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0f, startupRaycastHeight);
        if (TryGetComponent(out Collider ownCollider))
        {
            origin = ownCollider.bounds.center + Vector3.up * (ownCollider.bounds.extents.y + Mathf.Max(0f, startupRaycastHeight));
        }

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            Mathf.Max(0.01f, startupRaycastDistance),
            startupSurfaceLayers,
            startupTriggerInteraction);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Transform selfRoot = transform.root;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            Transform hitRoot = hitCollider.transform.root;
            if (hitRoot == selfRoot)
            {
                continue;
            }

            startupSurface = hitCollider;
            return true;
        }

        return false;
    }

    private void ForceStartupCamera()
    {
        DisableAllRegisteredCameras();

        if (defaultCamera != null)
        {
            SetActiveCamera(defaultCamera);
            return;
        }

        for (int i = 0; i < tagCameraBindings.Count; i++)
        {
            SurfaceTagCameraBinding binding = tagCameraBindings[i];
            if (binding == null || binding.virtualCamera == null)
            {
                continue;
            }

            SetActiveCamera(binding.virtualCamera);
            return;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!useCollisionContacts)
        {
            return;
        }

        RegisterContact(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!useCollisionContacts)
        {
            return;
        }

        RegisterContact(collision.collider);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!useCollisionContacts)
        {
            return;
        }

        UnregisterContact(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTriggerContacts)
        {
            return;
        }

        RegisterContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!useTriggerContacts)
        {
            return;
        }

        RegisterContact(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useTriggerContacts)
        {
            return;
        }

        UnregisterContact(other);
    }

    private void RegisterContact(Collider surfaceCollider)
    {
        if (surfaceCollider == null)
        {
            return;
        }

        int existingIndex = activeSurfaceContacts.IndexOf(surfaceCollider);
        if (existingIndex >= 0)
        {
            if (existingIndex == activeSurfaceContacts.Count - 1)
            {
                return;
            }

            activeSurfaceContacts.RemoveAt(existingIndex);
        }

        activeSurfaceContacts.Add(surfaceCollider);
        EvaluateAndApplyCamera();
    }

    private void UnregisterContact(Collider surfaceCollider)
    {
        if (surfaceCollider == null)
        {
            return;
        }

        if (!activeSurfaceContacts.Remove(surfaceCollider))
        {
            return;
        }

        EvaluateAndApplyCamera();
    }

    private void EvaluateAndApplyCamera()
    {
        CleanupDestroyedContacts();

        bool hasAnySurfaceContact = false;

        for (int i = activeSurfaceContacts.Count - 1; i >= 0; i--)
        {
            Collider contact = activeSurfaceContacts[i];
            if (contact != null)
            {
                hasAnySurfaceContact = true;
            }

            if (TryGetCameraForCollider(contact, out Behaviour targetCamera))
            {
                SetActiveCamera(targetCamera);
                return;
            }
        }

        if (hasAnySurfaceContact && useDefaultCameraWhenNoTaggedSurface && defaultCamera != null)
        {
            SetActiveCamera(defaultCamera);
        }
    }

    private void CleanupDestroyedContacts()
    {
        for (int i = activeSurfaceContacts.Count - 1; i >= 0; i--)
        {
            if (activeSurfaceContacts[i] == null)
            {
                activeSurfaceContacts.RemoveAt(i);
            }
        }
    }

    private bool TryGetCameraForCollider(Collider surfaceCollider, out Behaviour targetCamera)
    {
        targetCamera = null;

        if (surfaceCollider == null)
        {
            return false;
        }

        if (TryGetCameraByTag(surfaceCollider.tag, out targetCamera))
        {
            return true;
        }

        Transform root = surfaceCollider.transform.root;
        if (root != null && root != surfaceCollider.transform)
        {
            return TryGetCameraByTag(root.tag, out targetCamera);
        }

        return false;
    }

    private bool TryGetCameraByTag(string surfaceTag, out Behaviour targetCamera)
    {
        targetCamera = null;
        if (string.IsNullOrWhiteSpace(surfaceTag))
        {
            return false;
        }

        string normalizedSurfaceTag = surfaceTag.Trim();

        for (int i = 0; i < tagCameraBindings.Count; i++)
        {
            SurfaceTagCameraBinding binding = tagCameraBindings[i];
            if (binding == null || binding.virtualCamera == null)
            {
                continue;
            }

            if (string.Equals(binding.surfaceTag?.Trim(), normalizedSurfaceTag, StringComparison.OrdinalIgnoreCase))
            {
                targetCamera = binding.virtualCamera;
                return true;
            }
        }

        return false;
    }

    private void SetActiveCamera(Behaviour targetCamera)
    {
        if (!CanManageCamera(targetCamera))
        {
            return;
        }

        if (currentActiveCamera == targetCamera && targetCamera.enabled)
        {
            return;
        }

        if (useFadeTransition && isActiveAndEnabled && gameObject.activeInHierarchy)
        {
            pendingTargetCamera = targetCamera;
            if (fadeSwitchCoroutine == null)
            {
                fadeSwitchCoroutine = StartCoroutine(FadeSwitchRoutine());
            }

            return;
        }

        ActivateCameraImmediately(targetCamera);
    }

    private void ActivateCameraImmediately(Behaviour targetCamera)
    {
        if (!CanManageCamera(targetCamera))
        {
            return;
        }

        if (instantSwitchWithoutBlend)
        {
            DisableAllRegisteredCameras();
            InvalidatePreviousCameraState(targetCamera);
            targetCamera.enabled = true;
            currentActiveCamera = targetCamera;
            ApplyMouseLookAndCursorState();
            return;
        }

        foreach (Behaviour camera in RegisteredCameras)
        {
            if (camera == null)
            {
                continue;
            }

            camera.enabled = camera == targetCamera;
        }

        currentActiveCamera = targetCamera;

        ApplyMouseLookAndCursorState();
    }

    private IEnumerator FadeSwitchRoutine()
    {
        EnsureFadeOverlay();

        while (pendingTargetCamera != null)
        {
            Behaviour target = pendingTargetCamera;
            pendingTargetCamera = null;

            yield return FadeOverlayTo(1f, fadeOutDuration);
            ActivateCameraImmediately(target);
            yield return null;
            yield return FadeOverlayTo(0f, fadeInDuration);
        }

        fadeSwitchCoroutine = null;
    }

    private IEnumerator FadeOverlayTo(float targetAlpha, float duration)
    {
        EnsureFadeOverlay();
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        float startAlpha = fadeCanvasGroup.alpha;
        float total = Mathf.Max(0.0001f, duration);

        if (duration <= 0f)
        {
            fadeCanvasGroup.alpha = targetAlpha;
            fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.001f;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / total);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
        fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.001f;
    }

    private void EnsureFadeOverlay()
    {
        if (fadeCanvasGroup != null)
        {
            Image existingImage = fadeCanvasGroup.GetComponent<Image>();
            if (existingImage != null)
            {
                existingImage.color = fadeColor;
            }

            Canvas existingCanvas = fadeCanvasGroup.GetComponent<Canvas>();
            if (existingCanvas != null)
            {
                existingCanvas.sortingOrder = fadeCanvasSortOrder;
            }

            return;
        }

        string overlayName = "SurfaceTagCameraFadeOverlay";
        Transform overlayTransform = transform.Find(overlayName);
        GameObject overlayObject = overlayTransform != null ? overlayTransform.gameObject : new GameObject(overlayName);

        if (overlayObject.transform.parent != transform)
        {
            overlayObject.transform.SetParent(transform, false);
        }

        RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = overlayObject.AddComponent<RectTransform>();
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Canvas canvas = overlayObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = overlayObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = fadeCanvasSortOrder;

        GraphicRaycaster raycaster = overlayObject.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = overlayObject.AddComponent<GraphicRaycaster>();
        }

        raycaster.enabled = false;

        Image image = overlayObject.GetComponent<Image>();
        if (image == null)
        {
            image = overlayObject.AddComponent<Image>();
        }

        image.color = fadeColor;
        image.raycastTarget = false;

        fadeCanvasGroup = overlayObject.GetComponent<CanvasGroup>();
        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = overlayObject.AddComponent<CanvasGroup>();
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
    }

    private void DisableAllRegisteredCameras()
    {
        foreach (Behaviour camera in RegisteredCameras)
        {
            if (camera == null)
            {
                continue;
            }

            camera.enabled = false;
        }
    }

    private void InvalidatePreviousCameraState(Behaviour camera)
    {
        if (camera == null)
        {
            return;
        }

        var type = camera.GetType();
        var property = type.GetProperty("PreviousStateIsValid");
        if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
        {
            property.SetValue(camera, false);
            return;
        }

        var field = type.GetField("PreviousStateIsValid") ?? type.GetField("m_PreviousStateIsValid");
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(camera, false);
        }
    }

    private void EnsureAnyRegisteredCameraActive()
    {
        if (defaultCamera != null && defaultCamera.enabled)
        {
            currentActiveCamera = defaultCamera;
            return;
        }

        foreach (Behaviour camera in RegisteredCameras)
        {
            if (camera != null && camera.enabled)
            {
                currentActiveCamera = camera;
                return;
            }
        }

        if (defaultCamera != null)
        {
            SetActiveCamera(defaultCamera);
            return;
        }

        for (int i = 0; i < tagCameraBindings.Count; i++)
        {
            SurfaceTagCameraBinding binding = tagCameraBindings[i];
            if (binding == null || binding.virtualCamera == null)
            {
                continue;
            }

            SetActiveCamera(binding.virtualCamera);
            return;
        }
    }

    private void RegisterManagedCameras()
    {
        if (CanManageCamera(defaultCamera))
        {
            RegisteredCameras.Add(defaultCamera);
        }

        for (int i = 0; i < tagCameraBindings.Count; i++)
        {
            SurfaceTagCameraBinding binding = tagCameraBindings[i];
            if (binding == null || binding.virtualCamera == null)
            {
                continue;
            }

            if (CanManageCamera(binding.virtualCamera))
            {
                RegisteredCameras.Add(binding.virtualCamera);
            }
        }
    }

    private void UnregisterManagedCameras()
    {
        if (defaultCamera != null)
        {
            RegisteredCameras.Remove(defaultCamera);
        }

        for (int i = 0; i < tagCameraBindings.Count; i++)
        {
            SurfaceTagCameraBinding binding = tagCameraBindings[i];
            if (binding == null || binding.virtualCamera == null)
            {
                continue;
            }

            RegisteredCameras.Remove(binding.virtualCamera);
        }
    }

    private void ApplyMouseLookAndCursorState()
    {
        for (int i = 0; i < mouseLookControllers.Count; i++)
        {
            Behaviour controller = mouseLookControllers[i];
            if (controller == null)
            {
                continue;
            }

            controller.enabled = !disableCameraRotationByCursor;
        }

        if (disableCameraRotationByCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private bool HasCinemachineBrain(Behaviour cameraBehaviour)
    {
        return cameraBehaviour != null && cameraBehaviour.GetComponent("CinemachineBrain") != null;
    }

    private bool CanManageCamera(Behaviour cameraBehaviour)
    {
        if (cameraBehaviour == null)
        {
            return false;
        }

        if (allowCinemachineBrainTargets)
        {
            return true;
        }

        return !HasCinemachineBrain(cameraBehaviour);
    }
}
