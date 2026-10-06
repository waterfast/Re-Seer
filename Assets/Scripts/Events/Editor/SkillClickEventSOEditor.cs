using ReSeer.Skills;
using UnityEditor;

[CustomEditor(typeof(SkillClickEventSO))]
public sealed class SkillClickEventSOEditor : BaseEventSOEditor<SkillData>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var skill = ((SkillClickEventSO)target).LastSkill;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("最近点击的技能（运行时）", EditorStyles.boldLabel);
        if (skill == null)
        {
            EditorGUILayout.LabelField("尚未收到技能点击。");
            return;
        }
        EditorGUILayout.LabelField("技能 ID", skill.id);
        EditorGUILayout.LabelField("名称", skill.fallbackName);
        EditorGUILayout.LabelField("属性", skill.elementId);
        EditorGUILayout.LabelField("类别", skill.category);
        EditorGUILayout.LabelField("威力", skill.power.ToString());
        EditorGUILayout.LabelField("最大 PP", skill.maxPp.ToString());
    }
}
