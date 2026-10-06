using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MainActionPanel))]
public sealed class MainActionPanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var panel = (MainActionPanel)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Test：生成三个技能槽"))
        {
            panel.Test();
            SceneView.RepaintAll();
        }
        if (!Application.isPlaying && GUILayout.Button("清除 Test 预览"))
        {
            var skills = panel.GetComponent<SkillPanel>();
            if (skills != null) skills.ClearTestPreview();
            SceneView.RepaintAll();
        }
    }

    [InitializeOnLoadMethod]
    private static void RegisterPreviewCleanup()
    {
        AssemblyReloadEvents.beforeAssemblyReload += ClearPreviews;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) ClearPreviews();
        };
    }

    private static void ClearPreviews()
    {
        foreach (var panel in Resources.FindObjectsOfTypeAll<SkillPanel>())
            if (!EditorUtility.IsPersistent(panel)) panel.ClearTestPreview();
    }
}
