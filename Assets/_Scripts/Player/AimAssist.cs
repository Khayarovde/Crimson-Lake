using UnityEngine;

public class AimAssist : MonoBehaviour
{
    [SerializeField] private float captureRange = 10f;
    [SerializeField] private float correctionFactor = 0.15f; // чуть сильнее, если хочешь

    private Transform muzzlePoint;
    private Transform currentTarget;
    private Vector3 lastKnownPosition;

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
        if (!enabled) currentTarget = null;
    }

    private void Update()
    {
        if (!Enabled || muzzlePoint == null) return;

        FindBestTarget();
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
        float closestDist = float.MaxValue;

        foreach (var col in hits)
        {
            float dist = Vector3.Distance(muzzlePoint.position, col.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                best = col.transform;
            }
        }

        currentTarget = best;
        if (currentTarget != null)
            lastKnownPosition = currentTarget.position;
    }

    public Vector3 GetAimDirection()
    {
        if (!Enabled || muzzlePoint == null || currentTarget == null)
            return muzzlePoint.forward;

        Vector3 toTarget = (lastKnownPosition - muzzlePoint.position).normalized;
        return Vector3.Lerp(muzzlePoint.forward, toTarget, correctionFactor);
    }

    public float GetSpread() => Random.Range(0.5f, 1.5f);
}