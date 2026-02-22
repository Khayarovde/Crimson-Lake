using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FPSController : MonoBehaviour
{
    public static FPSController Instance { get; private set; }

    [SerializeField] private TMP_InputField inputField;

    private const int MIN_FPS = 60;
    private const int MAX_FPS = 360;
    private const int DEFAULT_FPS = 60;

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
        ApplyFPS(savedFPS);

        if (inputField != null)
        {
            inputField.text = Application.targetFrameRate.ToString();
            inputField.onSubmit.AddListener(OnSubmit);
            inputField.onEndEdit.AddListener(OnSubmit);
        }
    }

    private void OnSubmit(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        if (int.TryParse(value, out int fps))
        {
            fps = Mathf.Clamp(fps, MIN_FPS, MAX_FPS);
            ApplyFPS(fps);
            inputField.text = fps.ToString();
        }
        else
        {
            inputField.text = Application.targetFrameRate.ToString();
        }
    }

    private void ApplyFPS(int fps)
    {
        // Современный подход 2024–2026
        Application.targetFrameRate = fps;
        QualitySettings.vSyncCount = 0;           // отключаем VSync полностью

        // Сохраняем выбор игрока
        PlayerPrefs.SetInt("TargetFrameRate", fps);
        PlayerPrefs.Save();

#if UNITY_EDITOR
        Debug.Log($"FPS → {fps} | VSync = OFF");
#endif
    }
}