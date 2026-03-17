using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FPSController : MonoBehaviour
{
    public static FPSController Instance { get; private set; }

    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private TMP_Text statusText;

    private const int MIN_FPS = 30;
    private const int MAX_FPS = 360;
    private const int DEFAULT_FPS = 120;

    private float fpsUpdateTimer;
    private int framesCounter;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Восстанавливаем значение при запуске (если было изменено ранее)
        int savedFPS = PlayerPrefs.GetInt("TargetFrameRate", DEFAULT_FPS);
        bool savedVSync = PlayerPrefs.GetInt("VSyncEnabled", 0) == 1;
        ApplySettings(savedFPS, savedVSync);

        if (inputField != null)
        {
            inputField.text = PlayerPrefs.GetInt("TargetFrameRate", DEFAULT_FPS).ToString();
            inputField.onSubmit.AddListener(OnSubmit);
            inputField.onEndEdit.AddListener(OnSubmit);
        }

        if (vSyncToggle != null)
        {
            vSyncToggle.SetIsOnWithoutNotify(savedVSync);
            vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        }

        UpdateInputAvailability(savedVSync);
    }

    private void Update()
    {
        if (statusText == null)
        {
            return;
        }

        framesCounter++;
        fpsUpdateTimer += Time.unscaledDeltaTime;

        if (fpsUpdateTimer >= 0.25f)
        {
            float currentFps = framesCounter / fpsUpdateTimer;
            statusText.text = Mathf.RoundToInt(currentFps).ToString();
            framesCounter = 0;
            fpsUpdateTimer = 0f;
        }
    }

    private void OnSubmit(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        if (int.TryParse(value, out int fps))
        {
            fps = Mathf.Clamp(fps, MIN_FPS, MAX_FPS);
            bool vSyncEnabled = vSyncToggle != null && vSyncToggle.isOn;
            ApplySettings(fps, vSyncEnabled);
            inputField.text = PlayerPrefs.GetInt("TargetFrameRate", DEFAULT_FPS).ToString();
        }
        else
        {
            inputField.text = PlayerPrefs.GetInt("TargetFrameRate", DEFAULT_FPS).ToString();
        }
    }

    private void OnVSyncChanged(bool isEnabled)
    {
        int fps = PlayerPrefs.GetInt("TargetFrameRate", DEFAULT_FPS);

        if (inputField != null && int.TryParse(inputField.text, out int parsedFps))
        {
            fps = Mathf.Clamp(parsedFps, MIN_FPS, MAX_FPS);
        }

        ApplySettings(fps, isEnabled);
        UpdateInputAvailability(isEnabled);
    }

    private void UpdateInputAvailability(bool vSyncEnabled)
    {
        if (inputField != null)
        {
            inputField.interactable = !vSyncEnabled;
        }
    }

    private void ApplySettings(int fps, bool vSyncEnabled)
    {
        int monitorRefreshRate = GetMonitorRefreshRate();
        fps = Mathf.Clamp(fps, MIN_FPS, MAX_FPS);

        if (vSyncEnabled)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = monitorRefreshRate;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps;
        }

        PlayerPrefs.SetInt("TargetFrameRate", fps);
        PlayerPrefs.SetInt("VSyncEnabled", vSyncEnabled ? 1 : 0);
        PlayerPrefs.Save();

#if UNITY_EDITOR
        Debug.Log($"FPS → {(vSyncEnabled ? $"VSync ({monitorRefreshRate} Hz)" : fps.ToString())} | Monitor = {monitorRefreshRate} Hz | VSync = {(vSyncEnabled ? "ON" : "OFF")}");
#endif
    }

    private int GetMonitorRefreshRate()
    {
        int monitorRefreshRate;
#if UNITY_2022_2_OR_NEWER
        monitorRefreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
#else
        monitorRefreshRate = Screen.currentResolution.refreshRate;
#endif

        if (monitorRefreshRate <= 0)
        {
            monitorRefreshRate = 60;
        }

        return monitorRefreshRate;
    }

}