using UnityEngine;

public class AimAssist : MonoBehaviour
{
    // Переменные AimAssist
    [SerializeField] private float captureRange = 10f;
    [SerializeField] private float correctionFactor = 0.1f;
    private Transform muzzlePoint;
    private Transform targetEnemy;
    private Vector3 lastKnownTargetPosition;
    public bool Enabled { get; set; }

    // Слой для распознавания врагов
    private LayerMask enemyLayerMask;

    // Поле для хранения ссылки на родителя
    private WeaponHandler parent;

    // Метод для связи с родителем и передачей слоя
    public void Initialize(WeaponHandler handler, LayerMask layerMask)
    {
        parent = handler;
        enemyLayerMask = layerMask;
    }

    // Метод для задания точки мушки
    public void SetMuzzlePoint(Transform point)
    {
        muzzlePoint = point;
    }

    // Метод для включения/выключения Aim Assist
    public void SetAiming(bool enabled, Transform muzzlePoint)
    {
        Enabled = enabled;
        SetMuzzlePoint(muzzlePoint);
    }

    // Основной цикл обновления
    private void Update()
    {
        if (!Enabled || muzzlePoint == null) return;

        // Ищем ближайших врагов
        Collider[] nearByColliders = Physics.OverlapSphere(muzzlePoint.position, captureRange, enemyLayerMask);

        if (nearByColliders.Length > 0)
        {
            // Находим ближайшего врага
            targetEnemy = FindClosestEnemy(nearByColliders);

            if (targetEnemy != null)
            {
                // Легонько подтягиваем мушку к врагу
                CorrectAim(targetEnemy.position);
            }
        }
    }

    // Находим ближайшего врага
    private Transform FindClosestEnemy(Collider[] colliders)
    {
        Transform closest = null;
        float minDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            float distance = Vector3.Distance(muzzlePoint.position, collider.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = collider.transform;
            }
        }

        return closest;
    }

    // Корректировка направления взгляда
    private void CorrectAim(Vector3 targetPosition)
    {
        // Вектор направления взгляда игрока
        Vector3 currentLookVector = muzzlePoint.forward;

        // Вектор от текущей позиции к цели
        Vector3 directionToTarget = (targetPosition - muzzlePoint.position).normalized;

        // Интерполируем изменение направления
        muzzlePoint.rotation = Quaternion.Slerp(muzzlePoint.rotation, Quaternion.LookRotation(directionToTarget), correctionFactor);
    }

    // Возвращает текущее направление взгляда
    public Vector3 GetAimDirection()
    {
        return muzzlePoint.forward;
    }

    // Возвращает величину разброса при стрельбе
    public float GetSpread()
    {
        return Random.Range(0.5f, 1.5f); // Генерация случайного разброса
    }

    // Метод для ручного включения/выключения Aim Assist
    public void ToggleAimAssist(bool active)
    {
        Enabled = active;
    }
}