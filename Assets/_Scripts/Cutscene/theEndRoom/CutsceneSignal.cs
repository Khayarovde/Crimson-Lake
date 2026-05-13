using UnityEngine;

// Вспомогательный компонент для Signal Receiver (Timeline). 
// Предоставляет параметрические-нейтральные методы, которые удобно вызывать из инспектора.
public class CutsceneSignal : MonoBehaviour
{
    [Tooltip("Ключ катсцены из CutsceneManager")]
    public string cutsceneKey;

    // Вызывается из Signal Receiver для запуска катсцены
    public void Play()
    {
        if (CutsceneManager.Instance == null)
        {
            Debug.LogWarning("CutsceneManager.Instance == null при попытке Play().");
            return;
        }

        CutsceneManager.Instance.StartCutscene(cutsceneKey);
    }

    // Вызывается из Signal Receiver для завершения текущей катсцены
    public void End()
    {
        if (CutsceneManager.Instance == null)
        {
            Debug.LogWarning("CutsceneManager.Instance == null при попытке End().");
            return;
        }

        CutsceneManager.Instance.EndCutscene();
    }
}
