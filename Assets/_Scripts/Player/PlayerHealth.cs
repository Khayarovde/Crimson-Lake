using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0)] private int currentHealth = 100;

    [Header("Enemy Hits")]
    [SerializeField, Min(1)] private int hitsToDie = 2;

    [Header("UI (optional)")]
    [Tooltip("Если не назначено, скрипт создаст оверлей сам.")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private Image overlayImage;

    [Tooltip("Canvas поражения (Lose). Можно не назначать, если враг сам передаст через SetLoseCanvas().")]
    [SerializeField] private GameObject loseScreenCanvas;

    [Header("FX")]
    [SerializeField, Range(0f, 1f)] private float firstHitRedAlpha = 0.25f;
    [SerializeField] private float firstHitFlashDuration = 0.6f;
    [SerializeField, Tooltip("Задержка перед запуском логики смерти, чтобы проигралась анимация игрока")]
    private float gameoverAnimationDelay = 1.2f;
    [SerializeField, Tooltip("Задержка (в реальном времени) перед затемнением на смертельном ударе — чтобы анимация врага успела проиграться.")]
    private float deathDelayBeforeFade = 0.75f;
    [SerializeField] private float deathFadeDuration = 0.65f;
    [SerializeField, Tooltip("Задержка (в реальном времени) перед переходом в Menu после показа LoseScreen")]
    private float deathMenuDelay = 1.2f;
    [SerializeField, Header("Turret FX"), Tooltip("Максимальная краснота экрана от удержания игрока под огнем турели")]
    [Range(0f, 1f)]
    private float turretMaxRedAlpha = 0.55f;
    [SerializeField, Tooltip("Скорость нарастания красноты, пока турель держит игрока в прицеле")]
    private float turretRedBuildSpeed = 1.25f;
    [SerializeField, Tooltip("Скорость спада красноты после выхода из прицела турели")]
    private float turretRedFadeSpeed = 1.6f;
    [SerializeField, Tooltip("Сколько времени после последнего попадания считается, что урон все еще продолжается")]
    private float turretDamageHoldTime = 0.3f;

    private bool isDead;
    private Coroutine overlayRoutine;
    private float turretExposureLevel;
    private float lastTurretDamageTime = -999f;

    public bool IsDead => isDead;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsFullHealth => currentHealth >= maxHealth;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        EnsureOverlay();
        TryResolveLoseCanvasIfMissing();
        if (loseScreenCanvas != null) loseScreenCanvas.SetActive(false);

//        Debug.Log($"[PlayerHealth] HP: {currentHealth}/{maxHealth}");
    }

    private void Update()
    {
        UpdateTurretOverlay();
    }

    public void SetLoseCanvas(GameObject canvas)
    {
        if (canvas == null) return;
        loseScreenCanvas = canvas;
        loseScreenCanvas.SetActive(false);
    }

    public void TakeEnemyHit(AdvancedEnemyAI source = null)
    {
        if (isDead) return;

        int enemyHitDamage = Mathf.CeilToInt((float)maxHealth / Mathf.Max(1, hitsToDie));
        ApplyDamage(enemyHitDamage, source);
    }

    public void SetHealth(int amount)
    {
        currentHealth = Mathf.Clamp(amount, 0, maxHealth);
        if (currentHealth <= 0)
        {
            Die(null);
        }
    }

    public void ApplyDamage(int amount, AdvancedEnemyAI source = null)
    {
        if (isDead || amount <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"[PlayerHealth] HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die(source);
            return;
        }

        var animCon = GetComponent<PlayerAnimationCon>();
        if (animCon != null)
            animCon.PlayHit();

        StartOverlayRoutine(FlashColor(new Color(1f, 0f, 0f, 1f), firstHitRedAlpha, firstHitFlashDuration));
    }

    public void ApplyTurretDamage(int amount)
    {
        if (isDead || amount <= 0)
            return;

        lastTurretDamageTime = Time.time;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"[PlayerHealth] HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die(null);
            return;
        }

        var animCon = GetComponent<PlayerAnimationCon>();
        if (animCon != null)
            animCon.PlayHit();
    }

    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Debug.Log($"[PlayerHealth] HP: {currentHealth}/{maxHealth}");
    }

    private void Die(AdvancedEnemyAI source)
    {
        if (isDead) return;
        isDead = true;
        currentHealth = 0;
        turretExposureLevel = 0f;
        Debug.Log($"[PlayerHealth] HP: {currentHealth}/{maxHealth}");

        var animCon = GetComponent<PlayerAnimationCon>();
        if (animCon != null)
            animCon.PlayGameOver();

        // Отключаем управление/оружие, чтобы игрок "умер".
        var weapon = GetComponent<WeaponHandler>();
        if (weapon != null) weapon.enabled = false;

        var tankController = GetComponent<TankController>();
        if (tankController != null)
        {
            tankController.SetMovementLock(true);
            tankController.SetMouseRotationEnabled(false);
            tankController.enabled = false;
        }

        var movement = GetComponent<CharacterMovement>();
        if (movement != null) movement.enabled = false;

        StartOverlayRoutine(DeathSequence(source));
    }

    private void StartOverlayRoutine(IEnumerator routine)
    {
        if (overlayRoutine != null)
            StopCoroutine(overlayRoutine);
        overlayRoutine = StartCoroutine(RunOverlayRoutine(routine));
    }

    private IEnumerator RunOverlayRoutine(IEnumerator routine)
    {
        yield return StartCoroutine(routine);
        overlayRoutine = null;
    }

    private IEnumerator FlashColor(Color color, float peakAlpha, float duration)
    {
        EnsureOverlay();
        if (overlayImage == null) yield break;

        overlayImage.color = new Color(color.r, color.g, color.b, 0f);

        float half = Mathf.Max(0.01f, duration * 0.5f);
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Lerp(0f, peakAlpha, t / half));
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Lerp(peakAlpha, 0f, t / half));
            yield return null;
        }

        SetOverlayAlpha(0f);
    }

    private IEnumerator DeathSequence(AdvancedEnemyAI source)
    {
        EnsureOverlay();

        // На всякий случай попробуем найти LoseScreen, если его не передали.
        TryResolveLoseCanvasIfMissing();

        if (gameoverAnimationDelay > 0f)
            yield return new WaitForSecondsRealtime(gameoverAnimationDelay);

        // Даем время анимации врага/звуку до затемнения.
        if (deathDelayBeforeFade > 0f)
            yield return new WaitForSecondsRealtime(deathDelayBeforeFade);

        // Плавно затемняем экран
        if (overlayImage != null)
        {
            overlayImage.color = new Color(0f, 0f, 0f, 0f);

            float t = 0f;
            while (t < deathFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                SetOverlayAlpha(Mathf.Lerp(0f, 1f, t / Mathf.Max(0.01f, deathFadeDuration)));
                yield return null;
            }

            SetOverlayAlpha(1f);
        }

        // Включаем Lose-канвас
        bool hasLoseCanvas = loseScreenCanvas != null;
        if (hasLoseCanvas) loseScreenCanvas.SetActive(true);

        // Курсор для меню поражения
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Пауза после смерти (только если реально есть меню поражения)
        if (hasLoseCanvas)
            Time.timeScale = 0f;

        // Через задержку уходим в сцену Menu
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, deathMenuDelay));
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu");
    }

    private void TryResolveLoseCanvasIfMissing()
    {
        if (loseScreenCanvas != null) return;

        // Быстрые попытки по имени
        var byName = GameObject.Find("LoseScreen") ?? GameObject.Find("LoseScreenCanvas") ?? GameObject.Find("Lose Canvas");
        if (byName != null)
        {
            loseScreenCanvas = byName;
            return;
        }

        // Поиск по Canvas'ам (включая неактивные)
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c == null) continue;
            string n = c.gameObject.name.ToLowerInvariant();
            if (n.Contains("lose") || n.Contains("gameover") || n.Contains("defeat") || n.Contains("dead"))
            {
                loseScreenCanvas = c.gameObject;
                return;
            }
        }
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null && overlayImage != null) return;

        // Пытаемся найти уже существующий оверлей на сцене (если пользователь его создал)
        if (overlayCanvas == null)
            overlayCanvas = GetComponentInChildren<Canvas>(true);

        if (overlayCanvas != null && overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (overlayImage == null)
                overlayImage = overlayCanvas.GetComponentInChildren<Image>(true);
            if (overlayImage != null) return;
        }

        // Если ничего не нашли — создаём простой ScreenSpaceOverlay Canvas + Image на весь экран
        var canvasGO = new GameObject("PlayerDamageOverlay");
        canvasGO.transform.SetParent(null);

        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 10000;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imageGO = new GameObject("OverlayImage");
        imageGO.transform.SetParent(canvasGO.transform, false);

        overlayImage = imageGO.AddComponent<Image>();
        overlayImage.raycastTarget = false;

        var rect = overlayImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayImage.color = new Color(0f, 0f, 0f, 0f);
    }

    private void UpdateTurretOverlay()
    {
        if (isDead)
            return;

        EnsureOverlay();
        if (overlayImage == null)
            return;

        bool exposedByTurret = Time.time - lastTurretDamageTime <= Mathf.Max(0.01f, turretDamageHoldTime);
        float target = exposedByTurret ? 1f : 0f;
        float speed = exposedByTurret ? Mathf.Max(0.01f, turretRedBuildSpeed) : Mathf.Max(0.01f, turretRedFadeSpeed);
        turretExposureLevel = Mathf.MoveTowards(turretExposureLevel, target, speed * Time.deltaTime);

        float turretAlpha = turretExposureLevel * turretMaxRedAlpha;

        if (overlayRoutine == null)
        {
            overlayImage.color = new Color(1f, 0f, 0f, turretAlpha);
            return;
        }

        Color current = overlayImage.color;
        overlayImage.color = new Color(1f, 0f, 0f, Mathf.Max(current.a, turretAlpha));
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlayImage == null) return;
        var c = overlayImage.color;
        c.a = alpha;
        overlayImage.color = c;
    }
}