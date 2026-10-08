using ReSeer.Battle.UI;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DamageDisplay))]
public sealed class DamageDisplayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DamageDisplay display = (DamageDisplay)target;
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck() && !Application.IsPlaying(display.gameObject))
            display.Preview();
        if (GUILayout.Button(Application.IsPlaying(display.gameObject) ? "播放伤害预览" : "刷新伤害预览"))
            display.PlayPreview();
    }
}
