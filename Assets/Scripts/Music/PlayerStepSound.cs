using UnityEngine;

public class PlayerStepSound : MonoBehaviour
{
    public AudioClip[] stepSounds_AR; // массив звуков текущий
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        // Step_sound_play(); // Временный вызов для теста: звук проиграется при запуске сцены
    }

    public void Step_sound_play()
    {
        if (stepSounds_AR.Length > 0 && audioSource != null)
        {
            audioSource.PlayOneShot(stepSounds_AR[Random.Range(0, stepSounds_AR.Length)]);
            Debug.Log("Событие шага сработало"); // Для отладки: смотрите в консоль Unity
            print("Звук шага"); // Ваш оригинальный print
        }
        else
        {
            Debug.LogWarning("Нет звуков в массиве или AudioSource не найден!");
        }
    }
}