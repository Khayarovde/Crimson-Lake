using UnityEngine;

public class AmmoPickup : MonoBehaviour
{
    public InventoryItem.ItemType ammoType; // Выбери в инспекторе Gun или Pistol
    public int amount = 30;
    public GameObject pickupEffect;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var weaponHandler = other.GetComponent<WeaponHandler>();
            if (weaponHandler != null)
            {
                weaponHandler.AddAmmo(ammoType, amount);

                if (pickupEffect) Instantiate(pickupEffect, transform.position, Quaternion.identity);
                Destroy(gameObject);
            }
        }
    }
}