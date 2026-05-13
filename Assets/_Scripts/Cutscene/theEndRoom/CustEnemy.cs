using UnityEngine;

// Вспомогательный компонент для Signal Receiver (Timeline).
// Позволяет по сигналу Timeline просто включить босс-объект.
[DisallowMultipleComponent]
public class CustEnemy : MonoBehaviour
{
    [Tooltip("Объект на сцене, который должен быть отключен при активации.")]
    [SerializeField] private GameObject bossObject;

    [Tooltip("Если босс должен создаваться заново, сюда назначь prefab босса.")]
    [SerializeField] private GameObject bossPrefab;

    [Tooltip("Точка спавна. Если не задана, будет использован объект с этим компонентом.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Ссылка на компонент BossEnemy для авто-поиска скрытого объекта")]
    [SerializeField] private BossEnemy bossEnemy;

    private void Reset()
    {
        bossEnemy = GetComponentInParent<BossEnemy>();
        bossObject = bossEnemy != null ? bossEnemy.gameObject : null;
        spawnPoint = transform;
    }

    private void Awake()
    {
        if (bossEnemy == null)
            bossEnemy = GetComponentInParent<BossEnemy>();

        if (bossObject == null && bossEnemy != null)
            bossObject = bossEnemy.gameObject;

        if (spawnPoint == null)
            spawnPoint = transform;
    }

    // Вызывается из Signal Receiver для скрытия объекта на сцене.
    public void HideBoss()
    {
        if (bossObject == null)
        {
            Debug.LogWarning("CustEnemy: не назначен bossObject для HideBoss.");
            return;
        }

        bossObject.SetActive(false);
    }

    // Вызывается из Signal Receiver для спавна босса из префаба.
    public void SpawnBoss()
    {
        if (bossPrefab == null)
        {
            Debug.LogWarning("CustEnemy: не назначен bossPrefab для SpawnBoss.");
            return;
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;
        Instantiate(bossPrefab, point.position, point.rotation);
    }
}