using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    [Header("Настройки пули")]
    public float damage = 20f;
    public float lifetime = 6f;
    public bool destroyOnHit = true;
    public GameObject hitEffect; // опционально: вспышка при попадании

    private void OnEnable()
    {
        CancelInvoke();
        Invoke(nameof(DestroySelf), lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Урон врагу
        var enemy = collision.collider.GetComponent<AdvancedEnemyAI>();
        if (enemy != null)
        {
            // enemy.TakeDamage(damage); // ← если у тебя есть система здоровья
            Destroy(enemy.gameObject); // как у тебя было раньше
        }

        // Визуальный эффект попадания
        if (hitEffect != null)
        {
            Instantiate(hitEffect, collision.contacts[0].point, Quaternion.LookRotation(collision.contacts[0].normal));
        }

        if (destroyOnHit)
            DestroySelf();
    }

    private void DestroySelf()
    {
        CancelInvoke();
        Destroy(gameObject);
    }

    private void OnDisable() => CancelInvoke();
}