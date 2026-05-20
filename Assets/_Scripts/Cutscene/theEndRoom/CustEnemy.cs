using UnityEngine;

// Вспомогательный компонент для Signal Receiver (Timeline).
// Позволяет скрыть катсценного босса и заспавнить настоящего.
[DisallowMultipleComponent]
public class CustEnemy : MonoBehaviour
{
    [Tooltip("Объект катсценного босса (пустышки), который нужно скрыть.")]
    [SerializeField] private GameObject cutsceneBoss;

    [Tooltip("Префаб настоящего босса с логикой и скриптами.")]
    [SerializeField] private GameObject realBossPrefab;

    [Tooltip("Точка спавна настоящего босса. Если пусто — заспавнится там же, где висит этот скрипт.")]
    [SerializeField] private Transform spawnPoint;

    private void Awake()
    {
        if (spawnPoint == null)
            spawnPoint = transform;
    }

    // Вызывается из Signal Receiver для скрытия объекта на сцене.
    public void HideBoss()
    {
        if (cutsceneBoss != null)
        {
            cutsceneBoss.SetActive(false);
        }
        else
        {
            Debug.LogWarning("CustEnemy: Не назначен cutsceneBoss для HideBoss.");
        }
    }

    // Вызывается из Signal Receiver для включения уже существующего босс-объекта.
    public void ShowBoss()
    {
        if (cutsceneBoss != null)
        {
            cutsceneBoss.SetActive(true);
        }
    }

    // Вызывается из Signal Receiver для спавна босса из префаба.
    public void SpawnBoss()
    {
        if (realBossPrefab != null)
        {
            Transform point = spawnPoint != null ? spawnPoint : transform;
            Instantiate(realBossPrefab, point.position, point.rotation);
        }
        else
        {
            Debug.LogWarning("CustEnemy: Не назначен realBossPrefab для SpawnBoss.");
        }
    }
}