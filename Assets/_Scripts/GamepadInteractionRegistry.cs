using System.Collections.Generic;
using UnityEngine;

public static class GamepadInteractionRegistry
{
    private static readonly List<ItemPickup> itemPickups = new List<ItemPickup>();
    private static readonly List<MedkitPickup> medkitPickups = new List<MedkitPickup>();
    private static readonly List<AmmoPickup> ammoPickups = new List<AmmoPickup>();
    private static readonly List<EnemyPickupInteraction> enemyPickups = new List<EnemyPickupInteraction>();
    private static readonly List<Interact> interactables = new List<Interact>();

    public static IReadOnlyList<ItemPickup> ItemPickups => itemPickups;
    public static IReadOnlyList<MedkitPickup> MedkitPickups => medkitPickups;
    public static IReadOnlyList<AmmoPickup> AmmoPickups => ammoPickups;
    public static IReadOnlyList<EnemyPickupInteraction> EnemyPickups => enemyPickups;
    public static IReadOnlyList<Interact> Interactables => interactables;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Reset()
    {
        itemPickups.Clear();
        medkitPickups.Clear();
        ammoPickups.Clear();
        enemyPickups.Clear();
        interactables.Clear();
    }

    public static void Register(ItemPickup pickup)
    {
        AddUnique(itemPickups, pickup);
    }

    public static void Unregister(ItemPickup pickup)
    {
        itemPickups.Remove(pickup);
    }

    public static void Register(MedkitPickup pickup)
    {
        AddUnique(medkitPickups, pickup);
    }

    public static void Unregister(MedkitPickup pickup)
    {
        medkitPickups.Remove(pickup);
    }

    public static void Register(AmmoPickup pickup)
    {
        AddUnique(ammoPickups, pickup);
    }

    public static void Unregister(AmmoPickup pickup)
    {
        ammoPickups.Remove(pickup);
    }

    public static void Register(EnemyPickupInteraction pickup)
    {
        AddUnique(enemyPickups, pickup);
    }

    public static void Unregister(EnemyPickupInteraction pickup)
    {
        enemyPickups.Remove(pickup);
    }

    public static void Register(Interact interact)
    {
        AddUnique(interactables, interact);
    }

    public static void Unregister(Interact interact)
    {
        interactables.Remove(interact);
    }

    private static void AddUnique<T>(List<T> list, T value) where T : MonoBehaviour
    {
        if (value == null || list.Contains(value))
            return;

        list.Add(value);
    }
}
