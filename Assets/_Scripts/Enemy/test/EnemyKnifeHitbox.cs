using UnityEngine;

[DisallowMultipleComponent]
public class EnemyKnifeHitbox : MonoBehaviour
{
    [SerializeField] private Enemytest owner;
    [SerializeField] private Collider hitboxCollider;

    private void Reset()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }
    }

    private void Awake()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
    }

    public void SetOwner(Enemytest newOwner)
    {
        owner = newOwner;
    }

    public void SetActiveWindow(bool active)
    {
        if (hitboxCollider == null)
            return;

        hitboxCollider.enabled = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null || other == null)
            return;

        owner.OnKnifeHitboxTriggered(other);
    }
}
