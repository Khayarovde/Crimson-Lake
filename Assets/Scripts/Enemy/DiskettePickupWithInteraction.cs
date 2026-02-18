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
    [SerializeField] private bool spawnEnemyOnlyAfterPickup = true;

    [Header("Yarn Interaction")]
    [SerializeField] private bool useYarnPickupFlow = true;
    [SerializeField] private Interact yarnInteractSource;

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
    private PlayerInventory cachedPlayerInventory;
    private bool hadCassetteInInventoryPreviousFrame;

    private AudioSource chaseAudioSource;
    private GameObject chaseAudioObject;

    private float chaseLocalVolume = 0f; // 0..chaseVolume

    private MusicZoneTrigger originalChaseZone;
    private bool isChaseMusicActive = false;

    private void Awake()
    {
        PrepareEnemyForDiskettePickup();
    }

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

        cachedPlayerInventory = FindObjectOfType<PlayerInventory>();
        hadCassetteInInventoryPreviousFrame = IsCassetteInPlayerInventory(cachedPlayerInventory);

    }

    private void PrepareEnemyForDiskettePickup()
    {
        if (!spawnEnemyOnlyAfterPickup || enemyAI == null || alreadyPickedUp)
            return;

        enemyAI.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        SettingsManager.MusicVolumeChanged += HandleMusicVolumeChanged;
        Interact.ItemPickedUp += HandleInteractItemPickedUp;
    }

    private void OnDisable()
    {
        SettingsManager.MusicVolumeChanged -= HandleMusicVolumeChanged;
        Interact.ItemPickedUp -= HandleInteractItemPickedUp;
    }

    private void Update()
    {
        if (spawnEnemyOnlyAfterPickup && !alreadyPickedUp && enemyAI != null && enemyAI.gameObject.activeSelf)
        {
            enemyAI.gameObject.SetActive(false);
        }

        if (!alreadyPickedUp)
        {
            if (cachedPlayerInventory == null)
                cachedPlayerInventory = FindObjectOfType<PlayerInventory>();

            if (!useYarnPickupFlow)
            {
                bool hasCassetteNow = IsCassetteInPlayerInventory(cachedPlayerInventory);
                if (!hadCassetteInInventoryPreviousFrame && hasCassetteNow)
                {
                    CompletePickupAndStartChase();
                    return;
                }

                hadCassetteInInventoryPreviousFrame = hasCassetteNow;
            }
        }

        if (alreadyPickedUp || player == null) return;

        // Подбор по клавише E, только если игрок рядом
        if (!useYarnPickupFlow && isPlayerNearby && Input.GetKeyDown(KeyCode.E))
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

        if (item == null)
        {
            Debug.LogWarning("[DiskettePickup] Кассета не назначена, запуск погони отменён.");
            return;
        }

        PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogError("[DiskettePickup] PlayerInventory не найден на игроке!");
            return;
        }

        bool added = playerInventory.AddItemToInventory(item);
        if (!added)
        {
            Debug.LogWarning("[DiskettePickup] Не удалось добавить дискету в инвентарь (возможно, полон).");
            return;
        }

        if (!IsCassetteInPlayerInventory(playerInventory))
        {
            Debug.LogWarning("[DiskettePickup] После подбора кассета не найдена в инвентаре, запуск погони отменён.");
            return;
        }

        cachedPlayerInventory = playerInventory;
        hadCassetteInInventoryPreviousFrame = true;
        CompletePickupAndStartChase();
    }

    private void HandleInteractItemPickedUp(InventoryItem pickedItem, PlayerInventory playerInventory, Interact source)
    {
        if (!useYarnPickupFlow || alreadyPickedUp)
            return;

        if (source == null)
            return;

        if (yarnInteractSource != null && source != yarnInteractSource)
            return;

        if (item != null)
        {
            if (pickedItem != item)
                return;
        }
        else
        {
            if (pickedItem == null || pickedItem.type != InventoryItem.ItemType.Cassette)
                return;
        }

        cachedPlayerInventory = playerInventory;
        hadCassetteInInventoryPreviousFrame = true;
        CompletePickupAndStartChase();
    }

    private bool IsCassetteInPlayerInventory(PlayerInventory playerInventory)
    {
        if (playerInventory == null || playerInventory.inventoryData == null)
            return false;

        var slots = playerInventory.inventoryData.GetSlots();
        foreach (var slot in slots)
        {
            if (slot == null || slot.type == InventoryItem.ItemType.Empty)
                continue;

            if (item != null)
            {
                if (slot == item)
                    return true;
            }
            else if (slot.type == InventoryItem.ItemType.Cassette)
            {
                return true;
            }
        }

        return false;
    }

    private void CompletePickupAndStartChase()
    {
        if (alreadyPickedUp)
            return;

        Debug.Log($"[DiskettePickup] Кассета подтверждена в инвентаре. Запускаем погоню ({gameObject.name})");

        alreadyPickedUp = true;

        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        if (pickupSound != null && audioSource != null)
            audioSource.PlayOneShot(pickupSound);

        ShowInteractionPrompt(false);
        if (visualObject != null) visualObject.SetActive(false);
        if (triggerCollider != null) triggerCollider.enabled = false;

        ActivateEnemyChase();
    }

    private void ActivateEnemyChase()
    {
        if (enemyAI == null)
        {
            Debug.LogError("Enemy AI не назначен!");
            return;
        }

        if (spawnEnemyOnlyAfterPickup && !enemyAI.gameObject.activeSelf)
        {
            enemyAI.gameObject.SetActive(true);
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