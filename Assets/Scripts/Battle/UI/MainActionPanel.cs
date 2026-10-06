using System;
using System.Collections.Generic;
using ReSeer.Skills;
using UnityEngine;

[DisallowMultipleComponent]
public class MainActionPanel : MonoBehaviour
{
    [SerializeField] private SkillPanel skillPanel;
    [SerializeField] private SkillDatabaseSO skillDatabase;

    [Header("临时技能库预览（接入精灵数据后关闭）")]
    [SerializeField] private bool loadPreviewOnStart;
    [SerializeField] private string[] previewSkillIds = { "demo_thunder", "demo_break", "demo_focus" };

    [Header("技能槽排列（相对于 MainActionPanel，世界单位）")]
    public float x = -6.4f;
    public float y = -0.7f;
    [Tooltip("相邻技能槽的水平步距，包含按钮宽度。")]
    [Min(0f)] public float spacing = 4.8f;

    private bool hasBoundSkills;
    public event Action<SkillData> SkillSelected;

    private void OnEnable()
    {
        if (skillPanel != null) skillPanel.SkillSelected += OnSkillSelected;
    }

    private void OnDisable()
    {
        if (skillPanel != null) skillPanel.SkillSelected -= OnSkillSelected;
    }

    private void Start()
    {
        // 战斗控制层若已传入真实技能与剩余 PP，演示数据不能覆盖它。
        if (loadPreviewOnStart && !hasBoundSkills) LoadDatabasePreview();
    }

    public void ShowSkills(IReadOnlyList<SkillData> skills, IReadOnlyList<int> currentPp)
    {
        if (skillPanel == null) throw new InvalidOperationException("请配置 MainActionPanel 的技能面板。");
        ApplySkillLayout();
        skillPanel.ShowSkills(skills, currentPp);
        hasBoundSkills = true;
    }

    [ContextMenu("Test / 生成技能槽")]
    public void Test() => LoadDatabasePreview();

    public void ApplySkillLayout()
    {
        if (skillPanel != null) skillPanel.SetLayout(x, y, spacing);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 延后修改 Transform，避免在 Unity 校验序列化字段期间触发布局重建。
        UnityEditor.EditorApplication.delayCall -= RefreshLayoutAfterValidation;
        UnityEditor.EditorApplication.delayCall += RefreshLayoutAfterValidation;
    }

    private void RefreshLayoutAfterValidation()
    {
        if (this != null && !UnityEditor.EditorUtility.IsPersistent(this)) ApplySkillLayout();
    }
#endif

    [ContextMenu("载入技能库预览")]
    public void LoadDatabasePreview()
    {
        if (skillDatabase == null || previewSkillIds == null)
        {
            Debug.LogWarning("请配置技能数据库和预览技能 ID。", this);
            return;
        }

        var skills = new List<SkillData>(previewSkillIds.Length);
        var currentPp = new List<int>(previewSkillIds.Length);
        foreach (string id in previewSkillIds)
        {
            if (!skillDatabase.TryGet(id, out var skill))
            {
                Debug.LogWarning($"技能库中找不到预览技能：{id}", this);
                return;
            }
            skills.Add(skill);
            // 仅演示用满 PP；真实战斗使用 ShowSkills 传入服务器确认的剩余值。
            currentPp.Add(skill.maxPp);
        }
        ShowSkills(skills, currentPp);
    }

    private void OnSkillSelected(SkillData skill) => SkillSelected?.Invoke(skill);
}
