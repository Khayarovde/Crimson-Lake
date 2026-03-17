using UnityEngine;

public class CameraDeadZoneFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Behaviour activationSource;

    [Header("Activation")]
    [SerializeField] private bool workOnlyWhenActivationSourceEnabled = true;

    [Header("Dead Zone (Viewport 0..1)")]
    [SerializeField, Range(0f, 1f)] private float deadZoneLeft = 0.35f;
    [SerializeField, Range(0f, 1f)] private float deadZoneRight = 0.65f;
    [SerializeField, Range(0f, 1f)] private float deadZoneBottom = 0.35f;
    [SerializeField, Range(0f, 1f)] private float deadZoneTop = 0.65f;

    [Header("Movement")]
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY;
    [SerializeField] private bool followZ = true;
    [SerializeField] private float smoothTime = 0.2f;

    private Vector3 velocity;

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
        activationSource = FindLocalActivationSource();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Awake()
    {
        if (activationSource == null)
        {
            activationSource = FindLocalActivationSource();
        }
    }

    private void LateUpdate()
    {
        if (workOnlyWhenActivationSourceEnabled && activationSource != null && !activationSource.enabled)
        {
            return;
        }

        if (target == null)
        {
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        if (deadZoneLeft > deadZoneRight)
        {
            float swap = deadZoneLeft;
            deadZoneLeft = deadZoneRight;
            deadZoneRight = swap;
        }

        if (deadZoneBottom > deadZoneTop)
        {
            float swap = deadZoneBottom;
            deadZoneBottom = deadZoneTop;
            deadZoneTop = swap;
        }

        Vector3 viewportPoint = cam.WorldToViewportPoint(target.position);
        if (viewportPoint.z <= 0f)
        {
            return;
        }

        float clampedX = Mathf.Clamp(viewportPoint.x, deadZoneLeft, deadZoneRight);
        float clampedY = Mathf.Clamp(viewportPoint.y, deadZoneBottom, deadZoneTop);

        bool outOfDeadZone = !Mathf.Approximately(clampedX, viewportPoint.x) || !Mathf.Approximately(clampedY, viewportPoint.y);
        if (!outOfDeadZone)
        {
            return;
        }

        Vector3 targetAtCurrentViewport = cam.ViewportToWorldPoint(new Vector3(viewportPoint.x, viewportPoint.y, viewportPoint.z));
        Vector3 targetAtClampedViewport = cam.ViewportToWorldPoint(new Vector3(clampedX, clampedY, viewportPoint.z));
        Vector3 cameraDelta = targetAtCurrentViewport - targetAtClampedViewport;

        Vector3 desiredPosition = transform.position + cameraDelta;

        if (!followX)
        {
            desiredPosition.x = transform.position.x;
        }

        if (!followY)
        {
            desiredPosition.y = transform.position.y;
        }

        if (!followZ)
        {
            desiredPosition.z = transform.position.z;
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, Mathf.Max(0.001f, smoothTime));
    }

    private Behaviour FindLocalActivationSource()
    {
        Camera localCamera = GetComponent<Camera>();
        if (localCamera != null)
        {
            return localCamera;
        }

        Behaviour[] localBehaviours = GetComponents<Behaviour>();
        for (int i = 0; i < localBehaviours.Length; i++)
        {
            Behaviour behaviour = localBehaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            string typeName = behaviour.GetType().Name;
            bool looksLikeCinemachineCamera = typeName.Contains("Cinemachine") && typeName.Contains("Camera");
            bool isBrain = typeName.Contains("Brain");
            if (looksLikeCinemachineCamera && !isBrain)
            {
                return behaviour;
            }
        }

        return null;
    }
}
