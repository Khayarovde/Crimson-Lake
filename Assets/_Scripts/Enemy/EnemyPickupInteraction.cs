using UnityEngine;
using System.Collections;
using System.Linq;

public class EnemyPickupInteraction : MonoBehaviour
{
    [Header("Предмет (инвентарь)")]
    [SerializeField] private InventoryItem item; // Ссылка на ScriptableObject дискеты (если используешь инвентарь)

    [Header("Враг")]
    [SerializeField] private AdvancedEnemyAI enemyAI;
    [SerializeField] private Transform enemySpawnPoint;
    [SerializeField] private bool spawnEnemyOnlyAfterPickup = true;

    [Header("Yarn Interaction")]
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
    private Coroutine chaseFadeRoutine;
    private bool isEndingChase;
    private bool isChaseFadingOut;

    private void Awake()
    {
        PrepareEnemyForDiskettePickup();
    }

    private void Start()
    {
        // Автоматическое определение визуала и коллайдера, если не назначены
        if (visualObject == null)
            visualObject = transform.Find("Model")?.gameObject;

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null && !triggerCollider.isTrigger)
            Debug.LogWarning($"[DiskettePickup] Коллайдер на {gameObject.name} не является триггером! Рекомендуется установить isTrigger = true.");

        if (enemyAI == null)
            enemyAI = FindFirstObjectByType<AdvancedEnemyAI>();

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

        cachedPlayerInventory = FindFirstObjectByType<PlayerInventory>();
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
                cachedPlayerInventory = FindFirstObjectByType<PlayerInventory>();

            bool hasCassetteNow = IsCassetteInPlayerInventory(cachedPlayerInventory);
            if (!hadCassetteInInventoryPreviousFrame && hasCassetteNow)
            {
                CompletePickupAndStartChase();
                return;
            }

            hadCassetteInInventoryPreviousFrame = hasCassetteNow;
        }

        if (!alreadyPickedUp && isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            TryPickup();
        }

        // Проверка окончания погони (враг мёртв)
        if (isChaseMusicActive && enemyAI != null)
        {
            bool enemyDead = !enemyAI.gameObject.activeInHierarchy || enemyAI.caughtPlayer;
            if (enemyDead && !isEndingChase)
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
            Debug.Log($"[DiskettePickup] Игрок покинул зону подбора дискеты ({gameObject.name})");
        }
    }

    private void TryPickup()
    {
        if (player == null) return;

        PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogError("[DiskettePickup] PlayerInventory не найден на игроке!");
            return;
        }

        if (item == null)
        {
            Debug.LogWarning("[DiskettePickup] Кассета не назначена, запуск погони отменён.");
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
        if (alreadyPickedUp)
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
        HidePickupVisual();
        if (triggerCollider != null) triggerCollider.enabled = false;
        isPlayerNearby = false;
        player = null;

        ActivateEnemyChase();
    }

    private void HidePickupVisual()
    {
        if (visualObject == null)
            return;

        // Нельзя выключать объект со скриптом: на нём запускаются корутины погони.
        if (visualObject == gameObject)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
                renderer.enabled = false;

            Debug.LogWarning($"[DiskettePickup] visualObject указывает на корневой объект {gameObject.name}. Отключены только рендеры, объект оставлен активным.");
            return;
        }

        visualObject.SetActive(false);
    }

    private void ActivateEnemyChase()
    {
        if (enemyAI == null)
            enemyAI = FindFirstObjectByType<AdvancedEnemyAI>();

        if (enemyAI == null)
        {
            Debug.LogError("Enemy AI не назначен!");
            return;
        }

        if (spawnEnemyOnlyAfterPickup && !enemyAI.gameObject.activeSelf)
        {
            enemyAI.gameObject.SetActive(true);
        }

        enemyAI.enabled = true;

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
            if (chaseFadeRoutine != null)
                StopCoroutine(chaseFadeRoutine);
            chaseFadeRoutine = StartCoroutine(FadeInChaseMusic());
        }

        Debug.Log("Погоня активирована: chase-музыка играет только в зоне взятия дискеты.");
    }

    private MusicZoneTrigger FindCurrentActiveZone()
    {
        var allZones = FindObjectsByType<MusicZoneTrigger>(FindObjectsSortMode.InstanceID);
        return allZones.FirstOrDefault(z =>
            z.zoneAudioSource != null &&
            z.zoneAudioSource.isPlaying &&
            z.zoneAudioSource.volume > 0.01f);
    }

    private void FadeOutAllZoneMusic()
    {
        var allZones = FindObjectsByType<MusicZoneTrigger>(FindObjectsSortMode.InstanceID);
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
        isChaseFadingOut = false;
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
        isChaseFadingOut = true;
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
        isChaseFadingOut = false;
        chaseFadeRoutine = null;
    }

    private void StartFadeOutChaseMusicIfNeeded()
    {
        if (!isChaseMusicActive || chaseAudioSource == null)
            return;

        if (isChaseFadingOut)
            return;

        if (chaseFadeRoutine != null)
            StopCoroutine(chaseFadeRoutine);

        chaseFadeRoutine = StartCoroutine(FadeOutChaseMusic());
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
        isEndingChase = true;
        if (chaseAudioSource != null && chaseAudioSource.isPlaying)
        {
            StartFadeOutChaseMusicIfNeeded();
            yield return chaseFadeRoutine;
        }
        originalChaseZone = null;
        isEndingChase = false;
        Debug.Log("Погоня полностью завершена (враг мёртв).");
    }

    private bool IsPlayableMusicZone(MusicZoneTrigger zone)
    {
        return zone != null &&
               zone.playOnEnter &&
               zone.zoneMusic != null;
    }

    // Вызывается из MusicZoneTrigger при входе в новую зону
    public void OnPlayerEnteredNewZone(MusicZoneTrigger newZone)
    {
        if (!isChaseMusicActive)
            return;

        if (!IsPlayableMusicZone(newZone))
            return;

        // В зоне, где была поднята дискета, chase-музыка продолжает играть.
        if (originalChaseZone != null && newZone == originalChaseZone)
            return;

        Debug.Log("Игрок вошёл в другую музыкальную зону -> chase-музыка затухает.");
        StartFadeOutChaseMusicIfNeeded();
        originalChaseZone = null;
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