using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MusicZoneTrigger))]
public class MusicZoneTriggerEditor : Editor
{
    private SerializedProperty _zoneMusicProp;
    private SerializedProperty _zoneVolumeProp;
    private SerializedProperty _useMultiTrackProp;
    private SerializedProperty _tracksProp;
    private SerializedProperty _loopMusicProp;
    private SerializedProperty _playOnEnterProp;
    private SerializedProperty _stopOnExitProp;
    private SerializedProperty _fadePreviousOnEnterProp;
    private SerializedProperty _fadeInTimeProp;
    private SerializedProperty _fadeOutTimeProp;
    private SerializedProperty _zoneAudioSourceProp;

    private void OnEnable()
    {
        _zoneMusicProp = serializedObject.FindProperty("zoneMusic");
        _zoneVolumeProp = serializedObject.FindProperty("zoneVolume");
        _useMultiTrackProp = serializedObject.FindProperty("useMultiTrack");
        _tracksProp = serializedObject.FindProperty("tracks");
        _loopMusicProp = serializedObject.FindProperty("loopMusic");
        _playOnEnterProp = serializedObject.FindProperty("playOnEnter");
        _stopOnExitProp = serializedObject.FindProperty("stopOnExit");
        _fadePreviousOnEnterProp = serializedObject.FindProperty("fadePreviousOnEnter");
        _fadeInTimeProp = serializedObject.FindProperty("fadeInTime");
        _fadeOutTimeProp = serializedObject.FindProperty("fadeOutTime");
        _zoneAudioSourceProp = serializedObject.FindProperty("zoneAudioSource");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_useMultiTrackProp);

        if (_useMultiTrackProp.boolValue)
        {
            DrawTrackList();
        }
        else
        {
            EditorGUILayout.PropertyField(_zoneMusicProp);
            EditorGUILayout.PropertyField(_zoneVolumeProp);
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(_loopMusicProp);
        EditorGUILayout.PropertyField(_playOnEnterProp);
        EditorGUILayout.PropertyField(_stopOnExitProp);
        EditorGUILayout.PropertyField(_fadePreviousOnEnterProp);
        EditorGUILayout.PropertyField(_fadeInTimeProp);
        EditorGUILayout.PropertyField(_fadeOutTimeProp);
        EditorGUILayout.PropertyField(_zoneAudioSourceProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTrackList()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tracks", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope(1))
        {
            for (int i = 0; i < _tracksProp.arraySize; i++)
            {
                DrawTrackRow(i);
            }

            EditorGUILayout.Space(2f);

            if (GUILayout.Button("Add track"))
            {
                AddTrack();
            }
        }
    }

    private void DrawTrackRow(int index)
    {
        SerializedProperty elementProp = _tracksProp.GetArrayElementAtIndex(index);
        SerializedProperty clipProp = elementProp.FindPropertyRelative("clip");
        SerializedProperty volumeProp = elementProp.FindPropertyRelative("volume");

        Rect rowRect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
        float spacing = 4f;
        float removeWidth = 22f;
        float valueWidth = 40f;
        float sliderWidth = 120f;

        Rect removeRect = new Rect(rowRect.xMax - removeWidth, rowRect.y, removeWidth, rowRect.height);
        Rect valueRect = new Rect(removeRect.x - spacing - valueWidth, rowRect.y, valueWidth, rowRect.height);
        Rect sliderRect = new Rect(valueRect.x - spacing - sliderWidth, rowRect.y, sliderWidth, rowRect.height);
        Rect clipRect = new Rect(rowRect.x, rowRect.y, Mathf.Max(0f, sliderRect.x - spacing - rowRect.x), rowRect.height);

        EditorGUI.PropertyField(clipRect, clipProp, GUIContent.none);

        volumeProp.floatValue = EditorGUI.Slider(sliderRect, GUIContent.none, volumeProp.floatValue, 0f, 1f);
        EditorGUI.LabelField(valueRect, volumeProp.floatValue.ToString("0.00"));

        if (GUI.Button(removeRect, "✕"))
        {
            _tracksProp.DeleteArrayElementAtIndex(index);
        }
    }

    private void AddTrack()
    {
        int newIndex = _tracksProp.arraySize;
        _tracksProp.arraySize++;

        SerializedProperty elementProp = _tracksProp.GetArrayElementAtIndex(newIndex);
        elementProp.FindPropertyRelative("clip").objectReferenceValue = null;
        elementProp.FindPropertyRelative("volume").floatValue = 1f;
    }
}