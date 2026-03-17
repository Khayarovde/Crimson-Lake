using UnityEngine;

[CreateAssetMenu(fileName = "InventoryItem", menuName = "Scriptable Objects/InventoryItem")]
public class InventoryItem : ScriptableObject
{
    public string itemName;
    public Sprite icon;  // Иконка для слота (из спрайтов)
    public ItemType type;  // Тип: Gun, Cassette, Character и т.д.

  public enum ItemType {
    Gun,
    Cassette,
    Character,
    Pistol,
    Empty
  }
}
