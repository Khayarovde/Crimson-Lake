using UnityEngine;

[CreateAssetMenu(fileName = "InventoryItem", menuName = "Scriptable Objects/InventoryItem")]
public class InventoryItem : ScriptableObject
{
    public string itemName;
    public Sprite icon;  // Иконка для слота (из спрайтов)
    public ItemType type;  // Тип: Gun, Disketa/Cassette, Character, Ammo и т.д.
    public MedkitProfile medkitProfile; // Профиль аптечки (используется, если type == Medkit)

    [Header("Ограничения")]
    [Tooltip("Если включено, предмет нельзя уничтожить из инвентаря или сундука")]
    public bool cannotBeDestroyed;

    [Header("Описание")]
    [Tooltip("Если включено, в UI будет показываться текст из поля customDescription")]
    public bool useCustomDescription;
    [TextArea(2, 6)]
    public string customDescription;

    public enum ItemType
    {
        Gun = 0,
        Disketa = 1,
        Character = 2,
        Pistol = 3,
        Empty = 4,
        Cassette = 5,
        PistolAmmo = 6,
        ShotgunAmmo = 7,
        Medkit = 8
    }
}
