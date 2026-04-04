using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DestroyedSelectionGuard
{
    static DestroyedSelectionGuard()
    {
        EditorApplication.update += ClearDestroyedSelection;
        Selection.selectionChanged += ClearDestroyedSelection;
        EditorApplication.playModeStateChanged += _ => ClearDestroyedSelection();
    }

    private static void ClearDestroyedSelection()
    {
        // Unity destroyed objects become "fake null"; the inspector can keep stale editors for them.
        if (Selection.objects == null || Selection.objects.Length == 0)
            return;

        if (!HasDestroyedObjectInSelection())
            return;

        Selection.objects = Array.Empty<UnityEngine.Object>();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }

    private static bool HasDestroyedObjectInSelection()
    {
        UnityEngine.Object[] selected = Selection.objects;
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] == null)
                return true;
        }

        return false;
    }
}
