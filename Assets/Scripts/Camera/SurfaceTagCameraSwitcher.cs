using System;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Switch")]
    [SerializeField] private bool instantSwitchWithoutBlend = true;

    [Header("Cursor Camera Rotation")]
    [SerializeField] private bool disableCameraRotationByCursor;

    [Header("Camera Rotation Controllers")]
    [SerializeField] private List<Behaviour> mouseLookControllers = new List<Behaviour>();

    [Header("Detection")]
    [SerializeField] private bool useCollisionContacts = true;
    [SerializeField] private bool useTriggerContacts = true;

    private readonly List<Collider> activeSurfaceContacts = new List<Collider>();
    private Behaviour currentActiveCamera;

    private void OnEnable()
    {
        RegisterManagedCameras();
        EnsureAnyRegisteredCameraActive();
    }

    private void OnDisable()
    {
        activeSurfaceContacts.Clear();
        UnregisterManagedCameras();
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

        activeSurfaceContacts.Remove(surfaceCollider);
        EvaluateAndApplyCamera();
    }

    private void EvaluateAndApplyCamera()
    {
        CleanupDestroyedContacts();

        for (int i = activeSurfaceContacts.Count - 1; i >= 0; i--)
        {
            Collider contact = activeSurfaceContacts[i];
            if (TryGetCameraForCollider(contact, out Behaviour targetCamera))
            {
                SetActiveCamera(targetCamera);
                return;
            }
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

        for (int i = 0; i < tagCameraBindings.Count; i++)
        {
            SurfaceTagCameraBinding binding = tagCameraBindings[i];
            if (binding == null || binding.virtualCamera == null)
            {
                continue;
            }

            if (binding.surfaceTag == surfaceTag)
            {
                targetCamera = binding.virtualCamera;
                return true;
            }
        }

        return false;
    }

    private void SetActiveCamera(Behaviour targetCamera)
    {
        if (targetCamera == null || HasCinemachineBrain(targetCamera))
        {
            return;
        }

        if (currentActiveCamera == targetCamera && targetCamera.enabled)
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
        foreach (Behaviour camera in RegisteredCameras)
        {
            if (camera != null && camera.enabled)
            {
                currentActiveCamera = camera;
                return;
            }
        }

        foreach (Behaviour camera in RegisteredCameras)
        {
            if (camera == null)
            {
                continue;
            }

            SetActiveCamera(camera);
            return;
        }
    }

    private void RegisterManagedCameras()
    {
        for (int i = 0; i < tagCameraBindings.Count; i++)
        {
            SurfaceTagCameraBinding binding = tagCameraBindings[i];
            if (binding == null || binding.virtualCamera == null)
            {
                continue;
            }

            if (HasCinemachineBrain(binding.virtualCamera))
            {
                continue;
            }

            RegisteredCameras.Add(binding.virtualCamera);
        }
    }

    private void UnregisterManagedCameras()
    {
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
}
