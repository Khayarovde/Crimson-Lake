using UnityEngine;

public class MeleeHandler : MonoBehaviour
{
    [Header("Настройки melee")]
    [SerializeField] public float minShootDistance = 2f;
    [SerializeField] private float meleeDamage = 10f;
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

        // Урон
        Destroy(enemy.gameObject);

        nextMeleeTime = Time.time + meleeCooldown;
        return true;
    }
}