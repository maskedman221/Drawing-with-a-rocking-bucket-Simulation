using UnityEditor;

[InitializeOnLoad]
static class ClearSelectionOnPlay
{
    static ClearSelectionOnPlay()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredPlayMode)
        {
            ClearSelection();
            EditorApplication.delayCall -= ClearSelection;
            EditorApplication.delayCall += ClearSelection;
        }
    }

    static void ClearSelection()
    {
        Selection.objects = System.Array.Empty<UnityEngine.Object>();
        Selection.activeObject = null;
    }
}
