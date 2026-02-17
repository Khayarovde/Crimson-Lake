using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class DiskettePickupWithInteraction : MonoBehaviour
{
    [Header("Предмет (инвентарь)")]
    [SerializeField] private InventoryItem item; // Ссылка на ScriptableObject дискеты (если используешь инвентарь)

    [Header("Настройки подбора")]
    [SerializeField] private GameObject pickupEffect;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioSource audioSource;

    [Header("UI подсказка (опционально)")]
    [SerializeField] private GameObject interactionUI;
    [SerializeField] private string interactionText = "Нажмите E, чтобы взять дискету";

    [Header("Враг")]
    [SerializeField] private AdvancedEnemyAI enemyAI;
    [SerializeField] private Transform enemySpawnPoint;

    [Header("Освещение")]
    [SerializeField] private Light[] lightsToChangeColor;

    [Header("Chase музыка (погоня)")]
    [SerializeField] private AudioClip chaseMusicClip;
    [SerializeField] [Range(0f, 1f)] private float chaseVolume = 1f;
    [SerializeField] private float chaseFadeTime = 1.5f;

    [Header("Что скрывать при подборе")]
    [Tooltip("Модель дискеты (обычно дочерний объект)")]
    [SerializeField] private GameObject visualObject;
    [Tooltip("Коллайдер триггера (для OnTriggerEnter/Exit)")]
    [SerializeField] private Collider triggerCollider;

    // Внутренние переменные
    private bool isPlayerNearby = false;
    private Transform player;
    private bool alreadyPickedUp = false;

    private AudioSource chaseAudioSource;
    private GameObject chaseAudioObject;

    private float chaseLocalVolume = 0f; // 0..chaseVolume

    private MusicZoneTrigger originalChaseZone;
    private bool isChaseMusicActive = false;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Автоматическое определение визуала и коллайдера, если не назначены
        if (visualObject == null)
            visualObject = transform.Find("Model")?.gameObject ?? gameObject;

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null && !triggerCollider.isTrigger)
            Debug.LogWarning($"[DiskettePickup] Коллайдер на {gameObject.name} не является триггером! Рекомендуется установить isTrigger = true.");

        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
            var uiText = interactionUI.GetComponent<Text>();
            if (uiText != null) uiText.text = interactionText;
        }

        // Создаём независимый источник для chase-музыки
        if (chaseMusicClip != null)
        {
            chaseAudioObject = new GameObject("GlobalChaseMusic");
            chaseAudioObject.transform.SetParent(transform);
            chaseAudioSource = chaseAudioObject.AddComponent<AudioSource>();
            chaseAudioSource.playOnAwake = false;
            chaseAudioSource.loop = true;
            chaseLocalVolume = 0f;
            ApplyChaseVolume();
            chaseAudioSource.clip = chaseMusicClip;
        }

        // Проверка на наличие предмета (если используешь инвентарь)
        if (item == null)
            Debug.LogWarning($"[DiskettePickup] Предмет (InventoryItem) не назначен на {gameObject.name}. Подбор будет без добавления в инвентарь.");
    }

    private void OnEnable()
    {
        SettingsManager.MusicVolumeChanged += HandleMusicVolumeChanged;
    }

    private void OnDisable()
    {
        SettingsManager.MusicVolumeChanged -= HandleMusicVolumeChanged;
    }

    private void Update()
    {
        if (alreadyPickedUp || player == null) return;

        // Подбор по клавише E, только если игрок рядом
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            TryPickup();
        }

        // Проверка окончания погони (враг мёртв)
        if (isChaseMusicActive && enemyAI != null)
        {
            bool enemyDead = !enemyAI.gameObject.activeInHierarchy || enemyAI.caughtPlayer;
            if (enemyDead)
            {
                StartCoroutine(EndChaseCompletely());
            }
        }

        if (isChaseMusicActive && chaseAudioSource != null && chaseAudioSource.isPlaying)
        {
            ApplyChaseVolume();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (alreadyPickedUp) return;

        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            player = other.transform;
            ShowInteractionPrompt(true);
            Debug.Log($"[DiskettePickup] Игрок вошёл в зону подбора дискеты ({gameObject.name})");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (alreadyPickedUp) return;

        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            player = null;
            ShowInteractionPrompt(false);
            Debug.Log($"[DiskettePickup] Игрок покинул зону подбора дискеты ({gameObject.name})");
        }
    }

    private void TryPickup()
    {
        if (player == null) return;

        Debug.Log($"[DiskettePickup] Дискета успешно подобрана! ({gameObject.name})");

        alreadyPickedUp = true;

        // Эффект и звук
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        if (pickupSound != null && audioSource != null)
            audioSource.PlayOneShot(pickupSound);

        // Скрываем подсказку и визуал
        ShowInteractionPrompt(false);
        if (visualObject != null) visualObject.SetActive(false);
        if (triggerCollider != null) triggerCollider.enabled = false;

        // Добавляем в инвентарь (опционально)
        if (item != null)
        {
            PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
            if (playerInventory != null)
            {
                bool added = playerInventory.AddItemToInventory(item);
                if (!added)
                    Debug.LogWarning("[DiskettePickup] Не удалось добавить дискету в инвентарь (возможно, полон)");
            }
            else
            {
                Debug.LogError("[DiskettePickup] PlayerInventory не найден на игроке!");
            }
        }

        // Запускаем погоню
        ActivateEnemyChase();
    }

    private void ActivateEnemyChase()
    {
        if (enemyAI == null)
        {
            Debug.LogError("Enemy AI не назначен!");
            return;
        }

        // Телепорт врага (если нужно)
        if (enemySpawnPoint != null)
        {
            enemyAI.TeleportToPosition(enemySpawnPoint.position);
            enemyAI.transform.rotation = enemySpawnPoint.rotation;
        }

        enemyAI.StartChasingAfterDiskette();

        // Красное освещение
        foreach (Light light in lightsToChangeColor)
        {
            if (light != null) light.color = Color.red;
        }

        // Запоминаем зону, в которой взяли дискету
        originalChaseZone = FindCurrentActiveZone();

        // Затухание всех зональных треков
        FadeOutAllZoneMusic();

        // Запуск chase-музыки
        if (chaseAudioSource != null)
        {
            isChaseMusicActive = true;
            StartCoroutine(FadeInChaseMusic());
        }

        Debug.Log("Погоня активирована: chase-музыка играет только в зоне взятия дискеты.");
    }

    private MusicZoneTrigger FindCurrentActiveZone()
    {
        var allZones = FindObjectsOfType<MusicZoneTrigger>();
        return allZones.FirstOrDefault(z =>
            z.zoneAudioSource != null &&
            z.zoneAudioSource.isPlaying &&
            z.zoneAudioSource.volume > 0.01f);
    }

    private void FadeOutAllZoneMusic()
    {
        var allZones = FindObjectsOfType<MusicZoneTrigger>();
        foreach (var zone in allZones)
        {
            if (zone.zoneAudioSource != null && zone.zoneAudioSource.isPlaying)
            {
                zone.StopAllCoroutines();
                zone.StartCoroutine(zone.FadeOut(zone.fadeOutTime));
            }
        }
    }

    private IEnumerator FadeInChaseMusic()
    {
        chaseLocalVolume = 0f;
        ApplyChaseVolume();
        chaseAudioSource.Play();

        float elapsed = 0f;
        while (elapsed < chaseFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            chaseLocalVolume = Mathf.Lerp(0f, chaseVolume, elapsed / chaseFadeTime);
            ApplyChaseVolume();
            yield return null;
        }
        chaseLocalVolume = chaseVolume;
        ApplyChaseVolume();
    }

    private IEnumerator FadeOutChaseMusic()
    {
        float elapsed = 0f;
        float startLocal = chaseLocalVolume;
        while (elapsed < chaseFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            chaseLocalVolume = Mathf.Lerp(startLocal, 0f, elapsed / chaseFadeTime);
            ApplyChaseVolume();
            yield return null;
        }
        chaseAudioSource.Stop();
        chaseLocalVolume = 0f;
        ApplyChaseVolume();
        isChaseMusicActive = false;
    }

    private void ApplyChaseVolume()
    {
        if (chaseAudioSource == null) return;

        float globalMusicVol = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetMusicVolume()
            : PlayerPrefs.GetFloat("MusicVol", 0.8f);

        chaseAudioSource.volume = Mathf.Clamp01(chaseLocalVolume) * Mathf.Clamp01(globalMusicVol);
    }

    private void HandleMusicVolumeChanged(float newVolume)
    {
        if (!isChaseMusicActive || chaseAudioSource == null) return;
        ApplyChaseVolume();
    }

    private IEnumerator EndChaseCompletely()
    {
        if (chaseAudioSource != null && chaseAudioSource.isPlaying)
        {
            yield return StartCoroutine(FadeOutChaseMusic());
        }
        originalChaseZone = null;
        Debug.Log("Погоня полностью завершена (враг мёртв).");
    }

    // Вызывается из MusicZoneTrigger при входе в новую зону
    public void OnPlayerEnteredNewZone(MusicZoneTrigger newZone)
    {
        if (isChaseMusicActive && originalChaseZone != null && newZone != originalChaseZone)
        {
            Debug.Log("Игрок вошёл в другую зону → chase-музыка затухает.");
            StartCoroutine(FadeOutChaseMusic());
            originalChaseZone = null;
        }
    }

    private void ShowInteractionPrompt(bool show)
    {
        if (interactionUI != null)
            interactionUI.SetActive(show);
    }

    private void OnDrawGizmosSelected()
    {
        if (enemySpawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(enemySpawnPoint.position, Vector3.one * 0.5f);
        }
    }

    private void OnDestroy()
    {
        if (chaseAudioObject != null)
            Destroy(chaseAudioObject);
    }
}