using UnityEngine;

public class MenuSaveSlotsLauncher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SaveSlotsUI saveSlotsUI;

    [Header("Fallback")]
    [SerializeField] private string defaultSceneIfNoSave = "cameratest2";

    public void BeginPlayFlow()
    {
        Time.timeScale = 1f;

        if (saveSlotsUI == null)
            saveSlotsUI = ResolveSaveSlotsUI();

        if (saveSlotsUI != null)
        {
            if (!saveSlotsUI.IsOpen)
                saveSlotsUI.ShowForLoad(defaultSceneIfNoSave);
            return;
        }

        Debug.LogWarning("[MenuSaveSlotsLauncher] SaveSlotsUI не найден. Используется прямой запуск через SaveManager.");
        SaveManager.GetOrCreate().LoadLatestSaveOrDefault(defaultSceneIfNoSave);
    }

    private SaveSlotsUI ResolveSaveSlotsUI()
    {
        if (SaveSlotsUI.Instance != null)
            return SaveSlotsUI.Instance;

#if UNITY_2020_1_OR_NEWER
        var all = FindObjectsByType<SaveSlotsUI>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
#else
        var all = Resources.FindObjectsOfTypeAll<SaveSlotsUI>();
#endif
        return all != null && all.Length > 0 ? all[0] : null;
    }
}
