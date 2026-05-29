using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;
    private readonly HashSet<string> seenEventIds = new HashSet<string>();
    private readonly HashSet<string> solvedPuzzles = new HashSet<string>();
    private readonly HashSet<string> unlockedDoors = new HashSet<string>();
    private readonly HashSet<string> deadEnemies = new HashSet<string>();
    private readonly HashSet<string> pickedUpItems = new HashSet<string>();
    private static readonly HashSet<Chest> registeredChests = new HashSet<Chest>();

    private const string SaveSlotFilePrefix = "save_slot_";
    private const string PendingWarningKey = "PendingSaveWarning";
    private const int CurrentSaveVersion = 1;

    private bool hasUnsavedChanges;
    private GameSaveData pendingLoadData;
    private bool hasPendingLoad;
    private string pendingLoadSceneName;
    private int loadedPlaySeconds;
    private float sessionPlaySeconds;
    private Coroutine pendingApplyRoutine;
    private Coroutine postLoadStabilizeRoutine;

    private const float PendingLoadMaxWaitSeconds = 15f;
    private const int PostLoadStabilizeFrames = 8;
    private int pendingApplyRequestId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapBeforeFirstScene()
    {
        GetOrCreate();
    }

    public static SaveManager GetOrCreate()
    {
        if (Instance != null) return Instance;

        var existing = FindFirstObjectByType<SaveManager>();
        if (existing != null) return existing;

        var go = new GameObject("SaveManager");
        return go.AddComponent<SaveManager>();
    }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            GameObject persistentRoot = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(persistentRoot);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        sessionPlaySeconds += Time.unscaledDeltaTime;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (pendingApplyRoutine != null)
            {
                StopCoroutine(pendingApplyRoutine);
                pendingApplyRoutine = null;
            }
            if (postLoadStabilizeRoutine != null)
            {
                StopCoroutine(postLoadStabilizeRoutine);
                postLoadStabilizeRoutine = null;
            }
            Instance = null;
        }
    }
    
    [System.Obsolete("Use SaveSlot(slotIndex) instead.")]
    public void SaveGame()
    {
        SaveSlot(0);
    }
    
    [System.Obsolete("Use LoadSlot(slotIndex) or LoadLatestSaveOrDefault instead.")]
    public void LoadGame()
    {
        LoadSlot(0);
    }

    public bool HasSave(int slotIndex)
    {
        return File.Exists(GetSlotPath(slotIndex));
    }

    public string GetSaveSummary(int slotIndex)
    {
        if (!HasSave(slotIndex)) return "Пусто";

        if (!TryReadSaveData(slotIndex, out GameSaveData data)) return "Пусто";

        string timeText = FormatPlayTime(data.playSeconds);
        if (string.IsNullOrEmpty(data.savedAt))
            return "Сохранение | " + timeText;

        return data.savedAt + " \n " + timeText;
    }

    public void SaveSlot(int slotIndex)
    {
        GameSaveData data = BuildSaveData();
        if (data == null)
        {
            Debug.LogWarning("[SaveManager] Нечего сохранять: игрок не найден");
            return;
        }

        try
        {
            string json = JsonUtility.ToJson(data);
            string path = GetSlotPath(slotIndex);
            File.WriteAllText(path, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Ошибка записи сохранения: {ex.Message}");
            return;
        }

        hasUnsavedChanges = false;
        // Debug output: show solved puzzles contained in saved data for diagnosis
        if (data.solvedPuzzles != null)
            Debug.Log($"[SaveManager] ФАЙЛ {slotIndex + 1} сохранен. solvedPuzzles count={data.solvedPuzzles.Count} sample=[{(data.solvedPuzzles.Count>0?data.solvedPuzzles[0]:string.Empty)}]");
        else
            Debug.Log($"[SaveManager] ФАЙЛ {slotIndex + 1} сохранен. solvedPuzzles=null");
    }

    public bool LoadLatestSaveOrDefault(string defaultScene)
    {
        int latestSlot;
        if (TryGetLatestSlotIndex(out latestSlot))
            return LoadSlot(latestSlot);

        if (!string.IsNullOrEmpty(defaultScene))
        {
            StartNewGameSession();
            SceneManager.LoadScene(defaultScene);
            return true;
        }

        return false;
    }

    public bool LoadSlot(int slotIndex)
    {
        if (!TryReadSaveData(slotIndex, out GameSaveData data)) return false;

        if (!string.IsNullOrEmpty(data.sceneName) && SceneManager.GetActiveScene().name != data.sceneName)
        {
            QueuePendingApply(data);
            SceneManager.LoadScene(data.sceneName);
            return true;
        }

        if (!TryApplyNow(data))
            QueuePendingApply(data);

        return true;
    }

    public void StartNewGameSession()
    {
        loadedPlaySeconds = 0;
        sessionPlaySeconds = 0f;
        hasUnsavedChanges = false;
        seenEventIds.Clear();
        solvedPuzzles.Clear();
        unlockedDoors.Clear();
        deadEnemies.Clear();
        pickedUpItems.Clear();

        hasPendingLoad = false;
        pendingLoadData = null;
        pendingLoadSceneName = string.Empty;

        if (pendingApplyRoutine != null)
        {
            StopCoroutine(pendingApplyRoutine);
            pendingApplyRoutine = null;
        }

        if (postLoadStabilizeRoutine != null)
        {
            StopCoroutine(postLoadStabilizeRoutine);
            postLoadStabilizeRoutine = null;
        }
    }

    public void MarkUnsaved()
    {
        hasUnsavedChanges = true;
    }

    public bool HasUnsavedChanges => hasUnsavedChanges;

    public static bool HasSeenEvent(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return false;

        return Instance != null && Instance.seenEventIds.Contains(eventId);
    }

    public static void MarkEventSeen(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return;

        if (Instance == null)
            return;

        if (Instance.seenEventIds.Add(eventId))
            Instance.MarkUnsaved();
    }

    #region New Tracking Methods
    public static bool HasPuzzleSolved(string puzzleId) => Instance != null && !string.IsNullOrEmpty(puzzleId) && Instance.solvedPuzzles.Contains(puzzleId);
    public static void MarkPuzzleSolved(string puzzleId)
    {
        if (string.IsNullOrEmpty(puzzleId) || Instance == null) return;
        bool added = Instance.solvedPuzzles.Add(puzzleId);
        if (added)
        {
            Instance.MarkUnsaved();
            Debug.Log($"[SaveManager] MarkPuzzleSolved: {puzzleId}");
        }
        else
        {
            Debug.Log($"[SaveManager] MarkPuzzleSolved (already present): {puzzleId}");
        }
    }

    public static bool HasDoorUnlocked(string doorId) => Instance != null && !string.IsNullOrEmpty(doorId) && Instance.unlockedDoors.Contains(doorId);
    public static void MarkDoorUnlocked(string doorId) { if (Instance != null && !string.IsNullOrEmpty(doorId) && Instance.unlockedDoors.Add(doorId)) Instance.MarkUnsaved(); }

    public static bool IsEnemyDead(string enemyId) => Instance != null && !string.IsNullOrEmpty(enemyId) && Instance.deadEnemies.Contains(enemyId);
    public static void MarkEnemyDead(string enemyId) { if (Instance != null && !string.IsNullOrEmpty(enemyId) && Instance.deadEnemies.Add(enemyId)) Instance.MarkUnsaved(); }

    public static bool HasPickedUpItem(string itemId) => Instance != null && !string.IsNullOrEmpty(itemId) && Instance.pickedUpItems.Contains(itemId);
    public static void MarkItemPickedUp(string itemId) { if (Instance != null && !string.IsNullOrEmpty(itemId) && Instance.pickedUpItems.Add(itemId)) Instance.MarkUnsaved(); }
    #endregion

    public static void RegisterChest(Chest chest)
    {
        if (chest != null)
            registeredChests.Add(chest);
    }

    public static void UnregisterChest(Chest chest)
    {
        if (chest != null)
            registeredChests.Remove(chest);
    }

    public void RequestWarningOnNextScene(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        PlayerPrefs.SetString(PendingWarningKey, message);
        PlayerPrefs.Save();
    }
    
    // Удаление всех сохранений
    [ContextMenu("Delete All Saves")]
    public void DeleteAllSaves()
    {
        string[] files = Directory.GetFiles(Application.persistentDataPath, "*_chest.json");
        foreach (string file in files)
        {
            File.Delete(file);
        }
        string[] slotFiles = Directory.GetFiles(Application.persistentDataPath, SaveSlotFilePrefix + "*.json");
        foreach (string file in slotFiles)
        {
            File.Delete(file);
        }
        Debug.Log("[SaveManager] Все сохранения удалены");
    }

    public void DeleteAllSavesFromUI()
    {
        DeleteAllSaves();
    }
    
    // Проверка существования сохранения для конкретного сундука
    public bool ChestSaveExists(string chestId)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{chestId}_chest.json");
        return File.Exists(path);
    }

    private GameSaveData BuildSaveData()
    {
        Transform playerTransform = ResolvePlayerTransform();
        if (playerTransform == null) return null;

        var data = new GameSaveData
        {
            saveVersion = CurrentSaveVersion,
            sceneName = SceneManager.GetActiveScene().name,
            playerPosition = SerializableVector3.FromVector3(playerTransform.position),
            playerRotationEuler = SerializableVector3.FromVector3(playerTransform.rotation.eulerAngles),
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            playSeconds = Mathf.Max(0, loadedPlaySeconds) + Mathf.FloorToInt(sessionPlaySeconds),
            seenEventIds = new List<string>(seenEventIds),
            solvedPuzzles = new List<string>(solvedPuzzles),
            unlockedDoors = new List<string>(unlockedDoors),
            deadEnemies = new List<string>(deadEnemies),
            pickedUpItems = new List<string>(pickedUpItems)
        };

        TutorialManager tutorialManager = FindFirstObjectByType<TutorialManager>();
        if (tutorialManager != null)
            data.isHintIconVisible = tutorialManager.IsHintIconVisible();

        PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
        if (playerHealth != null)
            data.playerHealth = playerHealth.CurrentHealth;

        data.weapons = new List<WeaponSaveData>
        {
            new WeaponSaveData { weaponId = "gun", currentAmmoCount = PlayerAmmoData.gunInMag, reserveAmmoCount = PlayerAmmoData.gunReserve, isUnlocked = true },
            new WeaponSaveData { weaponId = "pistol", currentAmmoCount = PlayerAmmoData.pistolInMag, reserveAmmoCount = PlayerAmmoData.pistolReserve, isUnlocked = true }
        };

        data.chests = new List<ChestSlotData>();
        foreach (var chest in registeredChests)
        {
            if (chest == null || string.IsNullOrEmpty(chest.chestId))
                continue;

            data.chests.Add(new ChestSlotData
            {
                chestId = chest.chestId,
                itemNames = chest.GetChestItemNamesSnapshot()
            });
        }

        PlayerInventory inventory = playerTransform.GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();

        if (inventory != null && inventory.inventoryData != null)
        {
            data.activeItemIndex = inventory.activeItemIndex;
            data.inventoryItemNames = new List<string>();
            foreach (var item in inventory.inventoryData.items)
            {
                if (item != null)
                    data.inventoryItemNames.Add(item.itemName);
            }
        }

        return data;
    }

    private void ApplySaveData(GameSaveData data)
    {
        loadedPlaySeconds = Mathf.Max(0, data.playSeconds);
        sessionPlaySeconds = 0f;

        seenEventIds.Clear();
        if (data.seenEventIds != null)
        {
            for (int i = 0; i < data.seenEventIds.Count; i++)
            {
                string id = data.seenEventIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                    seenEventIds.Add(id);
            }
        }

        solvedPuzzles.Clear();
        if (data.solvedPuzzles != null)
        {
            foreach (var id in data.solvedPuzzles)
                if (!string.IsNullOrWhiteSpace(id)) solvedPuzzles.Add(id);
        }

        unlockedDoors.Clear();
        if (data.unlockedDoors != null)
        {
            foreach (var id in data.unlockedDoors)
                if (!string.IsNullOrWhiteSpace(id)) unlockedDoors.Add(id);
        }

        deadEnemies.Clear();
        if (data.deadEnemies != null)
        {
            foreach (var id in data.deadEnemies)
                if (!string.IsNullOrWhiteSpace(id)) deadEnemies.Add(id);
        }

        pickedUpItems.Clear();
        if (data.pickedUpItems != null)
        {
            foreach (var id in data.pickedUpItems)
                if (!string.IsNullOrWhiteSpace(id)) pickedUpItems.Add(id);
        }

        Transform playerTransform = ResolvePlayerTransform();
        if (playerTransform == null)
        {
            Debug.LogWarning("[SaveManager] Игрок не найден при применении сохранения");
            return;
        }

        Vector3 savedPosition = data.playerPosition.ToVector3();
        Quaternion savedRotation = Quaternion.Euler(data.playerRotationEuler.ToVector3());

        ApplyPlayerTransform(playerTransform, savedPosition, savedRotation);
        StartPostLoadStabilization(playerTransform, savedPosition, savedRotation);

        PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
        if (playerHealth != null && data.playerHealth >= 0f)
        {
            playerHealth.SetHealth(Mathf.FloorToInt(data.playerHealth));
        }

        if (data.weapons != null)
        {
            foreach (var w in data.weapons)
            {
                if (w.weaponId == "gun")
                {
                    PlayerAmmoData.gunInMag = w.currentAmmoCount;
                    PlayerAmmoData.gunReserve = w.reserveAmmoCount;
                }
                else if (w.weaponId == "pistol")
                {
                    PlayerAmmoData.pistolInMag = w.currentAmmoCount;
                    PlayerAmmoData.pistolReserve = w.reserveAmmoCount;
                }
            }
            PlayerAmmoData.initialized = true; // чтобы при старте сцены другие скрипты не перезаписали значения патронов
        }

        PlayerInventory inventory = playerTransform != null
            ? playerTransform.GetComponent<PlayerInventory>()
            : FindFirstObjectByType<PlayerInventory>();

        if (inventory != null && inventory.inventoryData != null)
        {
            inventory.inventoryData.Clear();
            if (data.inventoryItemNames != null)
            {
                for (int i = 0; i < data.inventoryItemNames.Count; i++)
                {
                    string itemName = data.inventoryItemNames[i];
                    if (string.IsNullOrEmpty(itemName) || itemName == "Empty")
                        continue;

                    InventoryItem item = LoadItemFromResources(itemName);
                    if (item != null && i < inventory.inventoryData.items.Count)
                        inventory.inventoryData.items[i] = item;
                }
            }
            inventory.activeItemIndex = data.activeItemIndex;

            if (inventory.inventoryUI != null)
                inventory.inventoryUI.UpdateInventoryUI();
        }

        if (data.chests != null && data.chests.Count > 0)
        {
            Dictionary<string, Chest> chestById = new Dictionary<string, Chest>();
            foreach (var chest in registeredChests)
            {
                if (chest == null || string.IsNullOrEmpty(chest.chestId))
                    continue;
                if (!chestById.ContainsKey(chest.chestId))
                    chestById.Add(chest.chestId, chest);
            }

            foreach (var savedChest in data.chests)
            {
                if (savedChest == null || string.IsNullOrEmpty(savedChest.chestId))
                    continue;

                if (chestById.TryGetValue(savedChest.chestId, out Chest chest))
                    chest.ApplyChestItemNamesSnapshot(savedChest.itemNames);
            }
        }

        TutorialManager tutorialManager = FindFirstObjectByType<TutorialManager>();
        if (tutorialManager != null)
            tutorialManager.SetHintIconVisibleFromSave(data.isHintIconVisible);

        hasUnsavedChanges = false;
        Debug.Log("[SaveManager] Сохранение применено");
    }

    private void ApplyPlayerTransform(Transform playerTransform, Vector3 savedPosition, Quaternion savedRotation)
    {
        if (playerTransform == null)
            return;

        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        bool controllerWasEnabled = false;
        if (controller != null)
        {
            controllerWasEnabled = controller.enabled;
            if (controllerWasEnabled)
                controller.enabled = false;
        }

        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.position = savedPosition;
            playerRb.rotation = savedRotation;
#if UNITY_6000_0_OR_NEWER
            playerRb.linearVelocity = Vector3.zero;
#else
            playerRb.velocity = Vector3.zero;
#endif
            playerRb.angularVelocity = Vector3.zero;
        }
        else
        {
            playerTransform.SetPositionAndRotation(savedPosition, savedRotation);
        }

        if (controller != null && controllerWasEnabled)
            controller.enabled = true;
    }

    private void StartPostLoadStabilization(Transform playerTransform, Vector3 savedPosition, Quaternion savedRotation)
    {
        if (postLoadStabilizeRoutine != null)
            StopCoroutine(postLoadStabilizeRoutine);

        postLoadStabilizeRoutine = StartCoroutine(StabilizeLoadedPlayerPose(playerTransform, savedPosition, savedRotation));
    }

    private System.Collections.IEnumerator StabilizeLoadedPlayerPose(Transform playerTransform, Vector3 savedPosition, Quaternion savedRotation)
    {
        for (int i = 0; i < PostLoadStabilizeFrames; i++)
        {
            if (playerTransform == null)
                break;

            yield return new WaitForEndOfFrame();
            ApplyPlayerTransform(playerTransform, savedPosition, savedRotation);
        }

        postLoadStabilizeRoutine = null;
    }

    private Transform ResolvePlayerTransform()
    {
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory != null) return inventory.transform;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        return playerObj != null ? playerObj.transform : null;
    }

    private InventoryItem LoadItemFromResources(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;

        InventoryItem item = Resources.Load<InventoryItem>($"Items/{itemName}");
        if (item == null)
        {
            InventoryItem[] allItems = Resources.FindObjectsOfTypeAll<InventoryItem>();
            foreach (var candidate in allItems)
            {
                if (candidate != null && candidate.itemName == itemName)
                    return candidate;
            }
        }

        if (item == null)
            Debug.LogWarning($"[SaveManager] Не удалось загрузить предмет: {itemName}");

        return item;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasPendingLoad || pendingLoadData == null)
            return;

        if (pendingApplyRoutine != null)
            StopCoroutine(pendingApplyRoutine);

        pendingApplyRequestId++;
        int requestId = pendingApplyRequestId;
        pendingApplyRoutine = StartCoroutine(ApplyPendingWhenReady(requestId));
    }

    private bool TryApplyNow(GameSaveData data)
    {
        if (data == null)
            return false;

        Transform playerTransform = ResolvePlayerTransform();
        if (playerTransform == null)
            return false;

        ApplySaveData(data);
        return true;
    }

    private void QueuePendingApply(GameSaveData data)
    {
        pendingLoadData = data;
        hasPendingLoad = data != null;
        pendingLoadSceneName = hasPendingLoad ? data.sceneName : string.Empty;

        if (!hasPendingLoad)
            return;

        if (pendingApplyRoutine != null)
            StopCoroutine(pendingApplyRoutine);

        pendingApplyRequestId++;
        int requestId = pendingApplyRequestId;
        pendingApplyRoutine = StartCoroutine(ApplyPendingWhenReady(requestId));
    }

    private System.Collections.IEnumerator ApplyPendingWhenReady(int requestId)
    {
        float startedAt = Time.realtimeSinceStartup;
        bool targetSceneReached = string.IsNullOrEmpty(pendingLoadSceneName)
            || SceneManager.GetActiveScene().name == pendingLoadSceneName;

        while (hasPendingLoad && pendingLoadData != null)
        {
            if (requestId != pendingApplyRequestId)
            {
                pendingApplyRoutine = null;
                yield break;
            }

            if (!targetSceneReached)
            {
                targetSceneReached = SceneManager.GetActiveScene().name == pendingLoadSceneName;
                if (!targetSceneReached)
                {
                    yield return null;
                    continue;
                }

                // Даем целевой сцене создать игрока (Awake/OnEnable/Start).
                startedAt = Time.realtimeSinceStartup;
                yield return null;
            }

            if (ResolvePlayerTransform() != null)
            {
                // Даем сцене инициализировать объекты (Start/OnEnable), затем применяем сохранение.
                yield return null;

                if (TryApplyNow(pendingLoadData))
                {
                    hasPendingLoad = false;
                    pendingLoadData = null;
                    pendingLoadSceneName = string.Empty;
                    pendingApplyRoutine = null;
                    yield break;
                }
            }

            if (Time.realtimeSinceStartup - startedAt >= PendingLoadMaxWaitSeconds)
            {
                Debug.LogWarning("[SaveManager] Игрок не найден вовремя, сохранение не применено полностью");
                hasPendingLoad = false;
                pendingLoadData = null;
                pendingLoadSceneName = string.Empty;
                pendingApplyRoutine = null;
                yield break;
            }

            yield return null;
        }

        pendingApplyRoutine = null;
    }

    private bool TryReadSaveData(int slotIndex, out GameSaveData data)
    {
        data = null;
        if (!HasSave(slotIndex)) return false;

        string path = GetSlotPath(slotIndex);
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Ошибка чтения сохранения: {ex.Message}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(json)) return false;
        if (!LooksLikeSaveJson(json))
        {
            Debug.LogWarning("[SaveManager] Поврежденное сохранение: отсутствуют обязательные поля");
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<GameSaveData>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Ошибка парсинга сохранения: {ex.Message}");
            data = null;
            return false;
        }

        if (!ValidateSaveData(data))
        {
            Debug.LogWarning("[SaveManager] Поврежденное сохранение: валидация не пройдена");
            data = null;
            return false;
        }

        return true;
    }

    private static bool LooksLikeSaveJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return false;
        return json.Contains("\"sceneName\"")
            && json.Contains("\"playerPosition\"")
            && json.Contains("\"playerRotationEuler\"");
    }

    private static bool ValidateSaveData(GameSaveData data)
    {
        if (data == null) return false;
        if (string.IsNullOrWhiteSpace(data.sceneName)) return false;

        Vector3 position = data.playerPosition.ToVector3();
        Vector3 rotation = data.playerRotationEuler.ToVector3();
        return IsFinite(position) && IsFinite(rotation);
    }

    private static bool IsFinite(Vector3 v)
    {
        return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string GetSlotPath(int slotIndex)
    {
        string fileName = SaveSlotFilePrefix + slotIndex + ".json";
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    private static string FormatPlayTime(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        return string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
    }

    public static string GetScreenshotPath(int slotIndex)
    {
        string fileName = SaveSlotFilePrefix + slotIndex + ".png";
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public static string PeekPendingWarningMessage()
    {
        return PlayerPrefs.GetString(PendingWarningKey, string.Empty);
    }

    public static void ClearPendingWarningMessage()
    {
        if (PlayerPrefs.HasKey(PendingWarningKey))
            PlayerPrefs.DeleteKey(PendingWarningKey);
    }

    private static bool TryGetLatestSlotIndex(out int slotIndex)
    {
        slotIndex = -1;
        string[] slotFiles = Directory.GetFiles(Application.persistentDataPath, SaveSlotFilePrefix + "*.json");
        if (slotFiles == null || slotFiles.Length == 0) return false;

        System.DateTime latestTime = System.DateTime.MinValue;
        string latestFile = null;

        foreach (string file in slotFiles)
        {
            var time = File.GetLastWriteTimeUtc(file);
            if (time > latestTime)
            {
                latestTime = time;
                latestFile = file;
            }
        }

        if (string.IsNullOrEmpty(latestFile)) return false;

        string name = Path.GetFileNameWithoutExtension(latestFile);
        string indexPart = name.Replace(SaveSlotFilePrefix, string.Empty);
        int parsed;
        if (!int.TryParse(indexPart, out parsed)) return false;

        slotIndex = parsed;
        return true;
    }
}

[System.Serializable]
public class GameSaveData
{
    public int saveVersion = 0;
    public string sceneName;
    public SerializableVector3 playerPosition;
    public SerializableVector3 playerRotationEuler;

    [Header("Player Data")]
    public float playerHealth = -1f; // -1 означает что значение не сохранялось
    public List<WeaponSaveData> weapons = new List<WeaponSaveData>();

    public List<string> inventoryItemNames;
    public List<ChestSlotData> chests;
    public int activeItemIndex = -1;
    public string savedAt;
    public int playSeconds;
    public List<string> seenEventIds;

    [Header("World State")]
    public List<string> solvedPuzzles = new List<string>();
    public List<string> unlockedDoors = new List<string>();
    public List<string> deadEnemies = new List<string>();
    public List<string> pickedUpItems = new List<string>();

    [Header("UI State")]
    public bool isHintIconVisible = true;
}

[System.Serializable]
public class WeaponSaveData
{
    public string weaponId;
    public int currentAmmoCount;
    public int reserveAmmoCount;
    public bool isUnlocked;
}

[System.Serializable]
public class ChestSlotData
{
    public string chestId;
    public List<string> itemNames;
}

[System.Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public static SerializableVector3 FromVector3(Vector3 value)
    {
        return new SerializableVector3 { x = value.x, y = value.y, z = value.z };
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}