using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PickupIdAttribute))]
public class PickupIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        float buttonWidth = 88f;
        Rect fieldRect = new Rect(position.x, position.y, position.width - buttonWidth - 6f, position.height);
        Rect buttonRect = new Rect(fieldRect.xMax + 6f, position.y, buttonWidth, position.height);

        EditorGUI.PropertyField(fieldRect, property, label);

        using (new EditorGUI.DisabledScope(property.serializedObject.isEditingMultipleObjects))
        {
            if (GUI.Button(buttonRect, "Generate"))
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Generate Pickup ID");
                property.stringValue = GUID.Generate().ToString();
                property.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(property.serializedObject.targetObject);
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}