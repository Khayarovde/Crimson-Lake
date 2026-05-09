using System.Collections;
using UnityEngine;

public class CameraYawTrackAndBlink : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private GameObject indicatorObject;

    [Header("Rotation")]
    [SerializeField] private bool smoothRotation = true;
    [SerializeField] private float yawSpeedDegreesPerSecond = 180f;
    [SerializeField] private float deadZoneRadius = 1.25f;
    [SerializeField] private float scanYawAmplitude = 45f;
    [SerializeField] private float scanYawSpeed = 1f;

    [Header("Visibility")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private float maxViewDistance = 999f;

    [Header("Blink")]
    [SerializeField] private float blinkIntervalSeconds = 0.25f;

    private Coroutine blinkRoutine;
    private bool wasVisible;
    private float scanCenterYaw;

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        RotateToTargetYaw();

        bool isVisible = IsTargetVisible();
        if (isVisible != wasVisible)
        {
            wasVisible = isVisible;
            UpdateBlinkState(isVisible);
        }
    }

    private void RotateToTargetYaw()
    {
        Vector3 toTarget = target.position - transform.position;
        Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z);
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float deadZoneSqr = deadZoneRadius * deadZoneRadius;
        if (flatDirection.sqrMagnitude <= deadZoneSqr)
        {
            ScanYaw();
            return;
        }

        float targetYaw = Mathf.Atan2(flatDirection.x, flatDirection.z) * Mathf.Rad2Deg;
        float currentYaw = transform.eulerAngles.y;
        float nextYaw = smoothRotation
            ? Mathf.MoveTowardsAngle(currentYaw, targetYaw, yawSpeedDegreesPerSecond * Time.deltaTime)
            : targetYaw;

        Vector3 currentEuler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(currentEuler.x, nextYaw, currentEuler.z);
        scanCenterYaw = nextYaw;
    }

    private void ScanYaw()
    {
        Vector3 currentEuler = transform.eulerAngles;
        float offset = Mathf.Sin(Time.time * scanYawSpeed * Mathf.PI * 2f) * scanYawAmplitude;
        float nextYaw = scanCenterYaw + offset;
        float currentYaw = currentEuler.y;
        if (smoothRotation)
        {
            nextYaw = Mathf.MoveTowardsAngle(currentYaw, nextYaw, yawSpeedDegreesPerSecond * Time.deltaTime);
        }

        transform.rotation = Quaternion.Euler(currentEuler.x, nextYaw, currentEuler.z);
    }

    private bool IsTargetVisible()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            return false;
        }

        Vector3 viewportPoint = cam.WorldToViewportPoint(target.position);
        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        if (viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f)
        {
            return false;
        }

        float distance = Vector3.Distance(cam.transform.position, target.position);
        if (distance > maxViewDistance)
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            return true;
        }

        Vector3 origin = cam.transform.position;
        Vector3 direction = (target.position - origin).normalized;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        return false;
    }

    private void UpdateBlinkState(bool shouldBlink)
    {
        if (indicatorObject == null)
        {
            return;
        }

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (shouldBlink)
        {
            blinkRoutine = StartCoroutine(BlinkIndicator());
        }
        else
        {
            indicatorObject.SetActive(false);
        }
    }

    private IEnumerator BlinkIndicator()
    {
        while (true)
        {
            indicatorObject.SetActive(!indicatorObject.activeSelf);
            yield return new WaitForSeconds(Mathf.Max(0.01f, blinkIntervalSeconds));
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null)
        {
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : GetComponent<Camera>();
        if (cam == null)
        {
            return;
        }

        Vector3 origin = cam.transform.position;
        Vector3 direction = (target.position - origin).normalized;
        float distance = Vector3.Distance(origin, target.position);

        bool hasHit = Physics.Raycast(origin, direction, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
        bool hitsTarget = hasHit && (hit.transform == target || hit.transform.IsChildOf(target));

        Gizmos.color = hitsTarget ? Color.green : Color.red;
        Vector3 endPoint = hasHit ? hit.point : target.position;
        Gizmos.DrawLine(origin, endPoint);
    }
}
