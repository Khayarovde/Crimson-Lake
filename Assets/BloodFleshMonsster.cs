using UnityEngine;
using System.Collections;

public class BloodFleshMonsster : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animationTriggerName = "Attack"; // Имя триггера или параметра анимации
    [SerializeField] private float animationDuration = 2f; // Длительность анимации

    [Header("Settings")]
    [SerializeField] private bool destroyAfterAnimation = true; // Удалить объект после анимации

    private bool isActivated = false; // Флаг предотвращения повторного взаимодействия

    private void Start()
    {
        if (animator == null) {
            animator = GetComponent<Animator>();
        }
    }

    /// <summary>
    /// Вызывается после завершения диалога Yarn для активации анимации атаки
    /// </summary>
    public void PlayAttackAnimation()
    {
        // Предотвращаем повторное взаимодействие
        if (isActivated) {
            Debug.LogWarning($"[BloodFleshMonsster] {gameObject.name} уже был активирован. Повторное взаимодействие невозможно.");
            return;
        }

        isActivated = true;

        if (animator == null) {
            Debug.LogError($"[BloodFleshMonsster] Animator не найден на {gameObject.name}.");
            return;
        }

        // Запускаем коррутину для проигрывания анимации
        StartCoroutine(PlayAnimationCoroutine());
    }

    private IEnumerator PlayAnimationCoroutine()
    {
        // Активируем триггер анимации
        animator.SetTrigger(animationTriggerName);

        // Ждем окончания анимации
        yield return new WaitForSeconds(animationDuration);

        
        
    }
}
