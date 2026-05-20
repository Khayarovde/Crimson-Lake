using UnityEngine;
using DG.Tweening;

public class AimAssist : MonoBehaviour
{
    [SerializeField, Tooltip("Включить автодокрутку на ближайшую цель")]
    private bool enableTargetAssist = false;
    [SerializeField] private float captureRange = 10f;
    [SerializeField] private float correctionFactor = 0.15f;
    [SerializeField, Tooltip("Минимальная сила автодокрутки сразу после наведения на цель")]
    private float minCorrectionFactor = 0.02f;
    [SerializeField, Tooltip("Время удержания цели до максимальной точности")]
    private float timeToFullAccuracy = 1.1f;
    [SerializeField] private float maxAimDistance = 300f;
    [SerializeField] private float maxAssistAngle = 12f;
    [SerializeField] private LayerMask aimRayMask = ~0;
    [SerializeField, Tooltip("Сначала брать точку прицеливания с плоскости курсора для стабильного попадания в точку мыши")]
    private bool preferCursorPlaneAim = true;
    [SerializeField, Tooltip("Смещение высоты плоскости прицеливания относительно дула")]
    private float cursorPlaneHeightOffset = 0f;
    [SerializeField, Tooltip("Минимальный разброс в режиме прицеливания")]
    private float aimingSpreadMin = 0f;
    [SerializeField, Tooltip("Максимальный разброс в режиме прицеливания")]
    private float aimingSpreadMax = 0.05f;
    [Header("Lock Marker")]
    [SerializeField, Tooltip("Показывать индикатор цели при удержании прицела")]
    private bool showLockMarker = true;
    [SerializeField, Tooltip("Опциональный префаб индикатора. Если не задан, создается runtime-квадрат")]
    private GameObject lockMarkerPrefab;
    [SerializeField] private float lockMarkerMinScale = 0.2f;
    [SerializeField] private float lockMarkerMaxScale = 1f;
    [SerializeField] private float lockMarkerCubeSize = 0.2f;
    [SerializeField] private float lockMarkerEdgeThickness = 0.03f;
    [SerializeField] private Color lockMarkerColor = new Color(0.2f, 0.65f, 1f, 1f);
    [SerializeField] private Ease lockMarkerGrowthEase = Ease.OutSine;

    private Transform muzzlePoint;
    private Transform currentTarget;
    private Transform previousTarget;
    private Vector3 lastKnownPosition;
    private Vector3 rawAimDirection = Vector3.forward;
    private Camera cachedMainCamera;
    private float targetLockTimer;

    private Transform lockMarkerTransform;
    private Material runtimeLockMarkerMaterial;
    private Tween lockMarkerScaleTween;

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
            previousTarget = null;
            targetLockTimer = 0f;
            rawAimDirection = transform.forward;
            HideLockMarker();
        }
    }

    private void Update()
    {
        if (!Enabled || muzzlePoint == null) return;

        UpdateRawAimDirection();
        if (enableTargetAssist)
            FindBestTarget();
        else
            currentTarget = null;

        UpdateLockProgress();
        UpdateLockMarkerVisual();
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

        if (preferCursorPlaneAim)
        {
            Plane cursorPlane = new Plane(Vector3.up, new Vector3(0f, muzzlePoint.position.y + cursorPlaneHeightOffset, 0f));
            if (cursorPlane.Raycast(ray, out float planeDistance))
            {
                aimPoint = ray.GetPoint(planeDistance);
                Vector3 toPlanePoint = aimPoint - muzzlePoint.position;
                if (toPlanePoint.sqrMagnitude >= 0.0001f)
                {
                    rawAimDirection = toPlanePoint.normalized;
                    return;
                }
            }
        }

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

    private Collider[] overlapHits = new Collider[32];

    private void FindBestTarget()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(muzzlePoint.position, captureRange, overlapHits, enemyLayerMask);
        if (hitCount == 0)
        {
            currentTarget = null;
            return;
        }

        Transform best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var col = overlapHits[i];
            if (col == null)
                continue;

            if (!TryResolveEnemyTarget(col, out Transform enemyRoot, out Vector3 targetPos))
                continue;

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
                best = enemyRoot;
                lastKnownPosition = targetPos;
            }
        }

        currentTarget = best;
    }

    private static bool TryResolveEnemyTarget(Collider col, out Transform enemyRoot, out Vector3 targetPos)
    {
        enemyRoot = null;
        targetPos = Vector3.zero;

        AdvancedEnemyAI advancedEnemy = col.GetComponentInParent<AdvancedEnemyAI>();
        if (advancedEnemy != null)
        {
            enemyRoot = advancedEnemy.transform;
            targetPos = GetEnemyCenter(enemyRoot, col);
            return true;
        }

        Enemytest testEnemy = col.GetComponentInParent<Enemytest>();
        if (testEnemy != null)
        {
            enemyRoot = testEnemy.transform;
            targetPos = GetEnemyCenter(enemyRoot, col);
            return true;
        }

        return false;
    }

    private static Vector3 GetEnemyCenter(Transform enemyRoot, Collider fallbackCollider)
    {
        if (enemyRoot != null)
        {
            Renderer[] renderers = enemyRoot.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                return combined.center;
            }

            Collider rootCollider = enemyRoot.GetComponent<Collider>();
            if (rootCollider != null)
                return rootCollider.bounds.center;
        }

        return fallbackCollider != null ? fallbackCollider.bounds.center : Vector3.zero;
    }

    private void UpdateLockProgress()
    {
        if (!enableTargetAssist || currentTarget == null)
        {
            targetLockTimer = 0f;
            previousTarget = null;
            return;
        }

        if (currentTarget != previousTarget)
        {
            previousTarget = currentTarget;
            targetLockTimer = 0f;
            RestartLockMarkerTween();
            return;
        }

        targetLockTimer += Time.deltaTime;
    }

    public float GetLockAccuracy01()
    {
        if (!enableTargetAssist || currentTarget == null)
            return 0f;

        float lockTime = Mathf.Max(0.01f, timeToFullAccuracy);
        return Mathf.Clamp01(targetLockTimer / lockTime);
    }

    public Vector3 GetAimDirection()
    {
        if (!Enabled || muzzlePoint == null)
            return transform.forward;

        Vector3 baseDirection = rawAimDirection.sqrMagnitude > 0.0001f ? rawAimDirection.normalized : muzzlePoint.forward;

        if (!enableTargetAssist || currentTarget == null)
            return baseDirection;

        Vector3 toTarget = (lastKnownPosition - muzzlePoint.position).normalized;
        float t = GetLockAccuracy01();
        float assistStrength = Mathf.Lerp(
            Mathf.Clamp01(minCorrectionFactor),
            Mathf.Clamp01(correctionFactor),
            t);

        return Vector3.Slerp(baseDirection, toTarget, assistStrength).normalized;
    }

    public float GetSpread()
    {
        float minSpread = Mathf.Max(0f, aimingSpreadMin);
        float maxSpread = Mathf.Max(minSpread, aimingSpreadMax);
        return Random.Range(minSpread, maxSpread);
    }

    private void UpdateLockMarkerVisual()
    {
        if (!showLockMarker || !Enabled || !enableTargetAssist || currentTarget == null)
        {
            HideLockMarker();
            return;
        }

        EnsureLockMarkerExists();
        if (lockMarkerTransform == null)
            return;

        if (!lockMarkerTransform.gameObject.activeSelf)
            lockMarkerTransform.gameObject.SetActive(true);

        Vector3 markerPos = lastKnownPosition;
        lockMarkerTransform.position = markerPos;

        if (lockMarkerScaleTween == null || !lockMarkerScaleTween.IsActive())
        {
            float s = Mathf.Lerp(lockMarkerMinScale, lockMarkerMaxScale, GetLockAccuracy01());
            lockMarkerTransform.localScale = Vector3.one * Mathf.Max(0.01f, s);
        }
    }

    private void EnsureLockMarkerExists()
    {
        if (lockMarkerTransform != null)
            return;

        GameObject markerObj;
        if (lockMarkerPrefab != null)
        {
            markerObj = Instantiate(lockMarkerPrefab);
        }
        else
        {
            markerObj = CreateRuntimeCubeMarker();
        }

        if (markerObj == null)
            return;

        markerObj.name = "AimLockMarker";
        lockMarkerTransform = markerObj.transform;
        lockMarkerTransform.localScale = Vector3.one * Mathf.Max(0.01f, lockMarkerMinScale);
        lockMarkerTransform.gameObject.SetActive(false);
    }

    private GameObject CreateRuntimeCubeMarker()
    {
        GameObject go = new GameObject("AimLockMarker_RuntimeWireCube");

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader != null)
        {
            runtimeLockMarkerMaterial = new Material(shader);
            runtimeLockMarkerMaterial.color = lockMarkerColor;
        }

        float cubeSize = Mathf.Max(0.05f, lockMarkerCubeSize);
        float edgeThickness = Mathf.Clamp(lockMarkerEdgeThickness, 0.005f, cubeSize * 0.4f);
        float half = cubeSize * 0.5f;

        CreateWireEdge(go.transform, "Edge_X_TopFront", new Vector3(0f, half, half), new Vector3(cubeSize, edgeThickness, edgeThickness));
        CreateWireEdge(go.transform, "Edge_X_TopBack", new Vector3(0f, half, -half), new Vector3(cubeSize, edgeThickness, edgeThickness));
        CreateWireEdge(go.transform, "Edge_X_BottomFront", new Vector3(0f, -half, half), new Vector3(cubeSize, edgeThickness, edgeThickness));
        CreateWireEdge(go.transform, "Edge_X_BottomBack", new Vector3(0f, -half, -half), new Vector3(cubeSize, edgeThickness, edgeThickness));

        CreateWireEdge(go.transform, "Edge_Y_LeftFront", new Vector3(-half, 0f, half), new Vector3(edgeThickness, cubeSize, edgeThickness));
        CreateWireEdge(go.transform, "Edge_Y_RightFront", new Vector3(half, 0f, half), new Vector3(edgeThickness, cubeSize, edgeThickness));
        CreateWireEdge(go.transform, "Edge_Y_LeftBack", new Vector3(-half, 0f, -half), new Vector3(edgeThickness, cubeSize, edgeThickness));
        CreateWireEdge(go.transform, "Edge_Y_RightBack", new Vector3(half, 0f, -half), new Vector3(edgeThickness, cubeSize, edgeThickness));

        CreateWireEdge(go.transform, "Edge_Z_LeftTop", new Vector3(-half, half, 0f), new Vector3(edgeThickness, edgeThickness, cubeSize));
        CreateWireEdge(go.transform, "Edge_Z_RightTop", new Vector3(half, half, 0f), new Vector3(edgeThickness, edgeThickness, cubeSize));
        CreateWireEdge(go.transform, "Edge_Z_LeftBottom", new Vector3(-half, -half, 0f), new Vector3(edgeThickness, edgeThickness, cubeSize));
        CreateWireEdge(go.transform, "Edge_Z_RightBottom", new Vector3(half, -half, 0f), new Vector3(edgeThickness, edgeThickness, cubeSize));

        return go;
    }

    private void CreateWireEdge(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
    {
        GameObject edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        edge.name = name;
        edge.transform.SetParent(parent, false);
        edge.transform.localPosition = localPosition;
        edge.transform.localRotation = Quaternion.identity;
        edge.transform.localScale = localScale;

        Collider edgeCollider = edge.GetComponent<Collider>();
        if (edgeCollider != null)
            Destroy(edgeCollider);

        Renderer edgeRenderer = edge.GetComponent<Renderer>();
        if (edgeRenderer != null && runtimeLockMarkerMaterial != null)
            edgeRenderer.material = runtimeLockMarkerMaterial;
    }

    private void RestartLockMarkerTween()
    {
        if (!showLockMarker || currentTarget == null)
            return;

        EnsureLockMarkerExists();
        if (lockMarkerTransform == null)
            return;

        lockMarkerScaleTween?.Kill();

        lockMarkerTransform.localScale = Vector3.one * Mathf.Max(0.01f, lockMarkerMinScale);
        lockMarkerTransform.gameObject.SetActive(true);

        float duration = Mathf.Max(0.05f, timeToFullAccuracy);
        lockMarkerScaleTween = lockMarkerTransform
            .DOScale(Vector3.one * Mathf.Max(lockMarkerMinScale, lockMarkerMaxScale), duration)
            .SetEase(lockMarkerGrowthEase)
            .SetLink(lockMarkerTransform.gameObject, LinkBehaviour.KillOnDisable);
    }

    private void HideLockMarker()
    {
        lockMarkerScaleTween?.Kill();
        lockMarkerScaleTween = null;

        if (lockMarkerTransform != null)
            lockMarkerTransform.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        HideLockMarker();
    }

    private void OnDestroy()
    {
        lockMarkerScaleTween?.Kill();
        lockMarkerScaleTween = null;

        if (lockMarkerTransform != null)
            Destroy(lockMarkerTransform.gameObject);

        if (runtimeLockMarkerMaterial != null)
            Destroy(runtimeLockMarkerMaterial);
    }
}