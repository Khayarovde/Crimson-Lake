using UnityEngine;
using System.IO;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // Сохранение игры
    public void SaveGame()
    {
        // Находим все сундуки на сцене и сохраняем их
        Chest[] allChests = FindObjectsOfType<Chest>();
        foreach (Chest chest in allChests)
        {
            // Данные сохраняются автоматически в классе Chest
        }
        
        Debug.Log("[SaveManager] Игра сохранена");
    }
    
    // Загрузка игры
    public void LoadGame()
    {
        // Данные загружаются автоматически при старте каждого сундука
        Debug.Log("[SaveManager] Игра загружена");
    }
    
    // Удаление всех сохранений
    public void DeleteAllSaves()
    {
        string[] files = Directory.GetFiles(Application.persistentDataPath, "*_chest.json");
        foreach (string file in files)
        {
            File.Delete(file);
        }
        Debug.Log("[SaveManager] Все сохранения удалены");
    }
    
    // Проверка существования сохранения для конкретного сундука
    public bool ChestSaveExists(string chestId)
    {
        string path = Path.Combine(Application.persistentDataPath, $"{chestId}_chest.json");
        return File.Exists(path);
    }
}