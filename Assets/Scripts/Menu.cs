using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    // Делаем метод ОБЯЗАТЕЛЬНО public (или можно оставить без модификатора — тогда он public по умолчанию в новых версиях)
    public void PlayOsnova()
    {
        // Включаем нормальную скорость игры (на случай, если до этого был паузой)
        Time.timeScale = 1f;
        
        // Загружаем сцену по имени
        SceneManager.LoadScene("cameratest2"); // ←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←
    }

    // Дополнительно: если хочешь по номеру сцены в Build Settings (иногда надёжнее)
    // public void PlayOsnovaByBuildIndex()
    // {
    //     Time.timeScale = 1f;
    //     SceneManager.LoadScene(1); // например, 1 — это индекс сцены cameratest2
    // }
}