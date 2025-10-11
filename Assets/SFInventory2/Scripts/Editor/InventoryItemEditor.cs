using UnityEditor;

namespace Parity.SFInventory2.Core.Editor
{
    [CustomEditor(typeof(InventoryItem), true)]
    public class InventoryItemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
}