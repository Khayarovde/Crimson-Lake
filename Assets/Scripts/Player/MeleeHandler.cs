using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class MeleeHandler : MonoBehaviour
{
    [Header("Настройки melee")]
    [SerializeField] public float minShootDistance = 2f;
    [SerializeField] private float pushForce = 10f;
    [SerializeField] private float pushDuration = 0.5f;
    [SerializeField] private float meleeCooldown = 0.5f;
    [SerializeField] private AudioClip meleeSound;

    private WeaponHandler weaponHandler;
    private float nextMeleeTime = 0f;

    private void Awake()
    {
        weaponHandler = GetComponent<WeaponHandler>();
        if (weaponHandler == null)
        {
            Debug.LogError("[MeleeHandler] WeaponHandler не найден!");
            enabled = false;
        }
    }

    // Теперь принимает врага напрямую
    public bool TryMeleeAttack(AdvancedEnemyAI enemy)
    {
        if (enemy == null || Time.time < nextMeleeTime) return false;

        // Анимация
        if (weaponHandler.playerAnimator != null && !string.IsNullOrEmpty(weaponHandler.meleeTrigger))
        {
            weaponHandler.playerAnimator.SetTrigger(weaponHandler.meleeTrigger);
            Debug.Log("Толчок! Анимация запущена!");
        }

        // Звук
        if (weaponHandler.audioSource && meleeSound)
            weaponHandler.audioSource.PlayOneShot(meleeSound);

        // Толчок (отталкивание без убийства)
        var agent = enemy.GetComponent<NavMeshAgent>();
        var rb = enemy.GetComponent<Rigidbody>();
        if (agent != null && rb != null)
        {
            agent.enabled = false;
            rb.isKinematic = false;

            Vector3 direction = (enemy.transform.position - transform.position).normalized;
            rb.AddForce(direction * pushForce, ForceMode.Impulse);

            StartCoroutine(ResetAgentAfterPush(enemy, pushDuration));
        }

        nextMeleeTime = Time.time + meleeCooldown;
        return true;
    }

    private IEnumerator ResetAgentAfterPush(AdvancedEnemyAI enemy, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (enemy == null) yield break;

        var agent = enemy.GetComponent<NavMeshAgent>();
        var rb = enemy.GetComponent<Rigidbody>();
        if (agent != null && rb != null)
        {
            rb.isKinematic = true;
            agent.enabled = true;
        }
    }
}