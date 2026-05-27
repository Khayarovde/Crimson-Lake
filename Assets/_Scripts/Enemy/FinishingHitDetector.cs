using UnityEngine;

public class FinishingHitDetector : MonoBehaviour
{
    [HideInInspector] public Transform targetEnemy;
    [HideInInspector] public FinishingManager manager;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        CheckHit(other.transform, hitPoint);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.contactCount == 0)
        {
            return;
        }

        CheckHit(collision.transform, collision.contacts[0].point);
    }

    private void CheckHit(Transform hitTransform, Vector3 hitPoint)
    {
        if (targetEnemy == null || manager == null || !manager.IsFinishingActive)
        {
            return;
        }

        if (hitTransform == targetEnemy || hitTransform.IsChildOf(targetEnemy))
        {
            manager.OnWeaponHit(hitPoint);
            targetEnemy = null; // prevent multiple hits per finishing
        }
    }
}
