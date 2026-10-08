using System;
using System.Collections.Generic;
using ReSeer.Battle.UI;
using ReSeer.Skills;
using UnityEngine;

[DisallowMultipleComponent]
public class MainActionPanel : MonoBehaviour
{
    [SerializeField] private SkillPanel skillPanel;
    [SerializeField] private FifthSkillButton fifthSkillButton;
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
        if (fifthSkillButton != null) fifthSkillButton.SkillSelected += OnSkillSelected;
    }

    private void OnDisable()
    {
        if (skillPanel != null) skillPanel.SkillSelected -= OnSkillSelected;
        if (fifthSkillButton != null) fifthSkillButton.SkillSelected -= OnSkillSelected;
    }

    private void Start()
    {
        // 战斗控制层若已传入真实技能与剩余 PP，演示数据不能覆盖它。
        if (loadPreviewOnStart && !hasBoundSkills) LoadDatabasePreview();
    }

    /// <summary>
    /// 用已确认的技能快照刷新技能槽；技能 ID 在这里由配置的技能库解析成技能定义。
    /// 只要有一个 ID 解析不出来就整体放弃，避免显示出一份残缺的技能表。
    /// </summary>
    public void ShowSkills(IReadOnlyList<BattleUiSkillSlot> slots, BattleUiSkillSlot? fifthSlot = null)
    {
        if (slots == null) throw new ArgumentNullException(nameof(slots));
        if (skillDatabase == null)
            throw new InvalidOperationException("请配置 MainActionPanel 的技能库。");

        var skills = new List<SkillData>(slots.Count);
        var currentPp = new List<int>(slots.Count);
        var available = new List<bool>(slots.Count);
        foreach (BattleUiSkillSlot slot in slots)
        {
            if (!skillDatabase.TryGet(slot.SkillId, out var skill))
            {
                Debug.LogWarning($"技能库中找不到技能：{slot.SkillId}，本次技能刷新已取消。", this);
                return;
            }
            skills.Add(skill);
            ValidateMaximum(slot, skill);
            currentPp.Add(slot.CurrentPp);
            available.Add(slot.Available);
        }
        SkillData fifthSkill = null;
        if (fifthSlot.HasValue)
        {
            if (fifthSkillButton == null)
                throw new InvalidOperationException("请配置 MainActionPanel 的第五技能按钮。");
            if (!skillDatabase.TryGet(fifthSlot.Value.SkillId, out fifthSkill))
            {
                Debug.LogWarning($"技能库中找不到第五技能：{fifthSlot.Value.SkillId}，本次技能刷新已取消。", this);
                return;
            }
            ValidateMaximum(fifthSlot.Value, fifthSkill);
        }
        ShowSkills(skills, currentPp);
        skillPanel.SetAvailability(available);
        if (fifthSkillButton != null)
        {
            fifthSkillButton.gameObject.SetActive(fifthSkill != null);
            if (fifthSkill != null)
            {
                if (fifthSkillButton.BoundSkill != fifthSkill)
                    fifthSkillButton.Bind(fifthSkill, fifthSlot.Value.CurrentPp);
                else fifthSkillButton.SetCurrentPP(fifthSlot.Value.CurrentPp);
                fifthSkillButton.SetInteractable(fifthSlot.Value.Available);
            }
        }
    }

    private static void ValidateMaximum(BattleUiSkillSlot slot, SkillData skill)
    {
        // 目前最大 PP 来自技能定义；提前拒绝矛盾快照，避免按钮静默截断后看似不更新。
        if (slot.MaxPp != skill.maxPp)
            throw new ArgumentException($"技能 {slot.SkillId} 的最大 PP 应为 {skill.maxPp}，收到 {slot.MaxPp}。");
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
