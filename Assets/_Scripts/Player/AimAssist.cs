using UnityEngine;

public class AimAssist : MonoBehaviour
{
    [SerializeField] private float captureRange = 10f;
    [SerializeField] private float correctionFactor = 0.15f;
    [SerializeField] private float maxAimDistance = 300f;
    [SerializeField] private float maxAssistAngle = 12f;
    [SerializeField] private LayerMask aimRayMask = ~0;

    private Transform muzzlePoint;
    private Transform currentTarget;
    private Vector3 lastKnownPosition;
    private Vector3 rawAimDirection = Vector3.forward;
    private Camera cachedMainCamera;

    public bool Enabled { get; private set; }

    private LayerMask enemyLayerMask;
    
    public void Initialize(LayerMask mask)
    {
        enemyLayerMask = mask;
    }

    public void SetAiming(bool enabled, Transform muzzle)
    {
        Enabled = enabled;
        muzzlePoint = muzzle;
        if (!enabled)
        {
            currentTarget = null;
            rawAimDirection = transform.forward;
        }
    }

    private void Update()
    {
        if (!Enabled || muzzlePoint == null) return;

        UpdateRawAimDirection();
        FindBestTarget();
    }

    private Camera GetMainCamera()
    {
        if (cachedMainCamera != null)
            return cachedMainCamera;

        cachedMainCamera = Camera.main;
        return cachedMainCamera;
    }

    private void UpdateRawAimDirection()
    {
        Camera cam = GetMainCamera();
        if (cam == null)
        {
            rawAimDirection = muzzlePoint.forward;
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 aimPoint;

        int mask = aimRayMask.value & ~(1 << gameObject.layer);
        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            Mathf.Max(1f, maxAimDistance),
            mask,
            QueryTriggerInteraction.Ignore
        );

        if (hasHit)
        {
            aimPoint = hit.point;
        }
        else
        {
            Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, muzzlePoint.position.y, 0f));
            if (!fallbackPlane.Raycast(ray, out float planeDistance))
            {
                rawAimDirection = muzzlePoint.forward;
                return;
            }

            aimPoint = ray.GetPoint(planeDistance);
        }

        Vector3 toAimPoint = aimPoint - muzzlePoint.position;
        if (toAimPoint.sqrMagnitude < 0.0001f)
        {
            rawAimDirection = muzzlePoint.forward;
            return;
        }

        rawAimDirection = toAimPoint.normalized;
    }

    private void FindBestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(muzzlePoint.position, captureRange, enemyLayerMask);
        if (hits.Length == 0)
        {
            currentTarget = null;
            return;
        }

        Transform best = null;
        float bestScore = float.MaxValue;

        foreach (var col in hits)
        {
            if (col == null)
                continue;

            Vector3 targetPos = col.bounds.center;
            Vector3 toTarget = targetPos - muzzlePoint.position;
            float dist = toTarget.magnitude;
            if (dist <= 0.001f)
                continue;

            Vector3 targetDir = toTarget / dist;
            float angle = Vector3.Angle(rawAimDirection, targetDir);
            if (angle > Mathf.Max(0.1f, maxAssistAngle))
                continue;

            float score = angle + dist * 0.05f;
            if (score < bestScore)
            {
                bestScore = score;
                best = col.transform;
                lastKnownPosition = targetPos;
            }
        }

        currentTarget = best;
    }

    public Vector3 GetAimDirection()
    {
        if (!Enabled || muzzlePoint == null)
            return transform.forward;

        Vector3 baseDirection = rawAimDirection.sqrMagnitude > 0.0001f ? rawAimDirection.normalized : muzzlePoint.forward;

        if (currentTarget == null)
            return baseDirection;

        Vector3 toTarget = (lastKnownPosition - muzzlePoint.position).normalized;
        return Vector3.Slerp(baseDirection, toTarget, Mathf.Clamp01(correctionFactor)).normalized;
    }

    public float GetSpread() => Random.Range(0.5f, 1.5f);
}