using UnityEngine;

[CreateAssetMenu(fileName = "MedkitProfile", menuName = "Scriptable Objects/Medkit Profile")]
public class MedkitProfile : ScriptableObject
{
    [Header("Healing")]
    [SerializeField, Min(1)] private int healAmount = 25;

    public int HealAmount => healAmount;

    private void OnValidate()
    {
        healAmount = Mathf.Max(1, healAmount);
    }
}
