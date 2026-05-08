using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AnimationDoorStuk : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip enterSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Animation")]
    [Tooltip("Animator на этом объекте или в дочерних объектах")]
    [SerializeField] private Animator animator;
    [Tooltip("Имя состояния в Animator Controller, которое нужно проиграть")]
    [SerializeField] private string playStateName = "Open";
    [Tooltip("Если включено, звук и анимация сработают только один раз")]
    [SerializeField] private bool playOnlyOnce = true;
    [Tooltip("Тег объекта игрока")]
    [SerializeField] private string playerTag = "Player";

    private bool triggered = false;
    private bool playerInTrigger = false;

    void Reset()
    {
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInTrigger = true;

        if (playOnlyOnce && triggered)
            return;

        if (enterSound != null && audioSource != null)
            audioSource.PlayOneShot(enterSound);

        if (animator != null && !string.IsNullOrWhiteSpace(playStateName))
            animator.Play(playStateName, 0, 0f);

        triggered = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInTrigger = false;
    }
}
