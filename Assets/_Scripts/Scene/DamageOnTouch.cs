using UnityEngine;

public class DamageOnTouch : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField, Min(1)] private int damageAmount = 10;
    [SerializeField, Min(0f)] private float damageInterval = 0.5f;

    [Header("Target Filter")]
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private string playerTag = "Player";

    private float nextDamageTime;

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (Time.time < nextDamageTime)
            return;

        TryApplyDamage(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyDamage(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (Time.time < nextDamageTime)
            return;

        TryApplyDamage(collision.collider);
    }

    private void TryApplyDamage(Collider other)
    {
        if (other == null)
            return;

        if (requirePlayerTag && !other.CompareTag(playerTag))
            return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            return;

        playerHealth.ApplyDamage(damageAmount);
        Debug.Log($"[DamageOnTouch] HP игрока: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}");

        nextDamageTime = Time.time + damageInterval;
    }
}
