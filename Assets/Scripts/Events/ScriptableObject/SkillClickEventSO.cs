using System;
using ReSeer.Skills;
using UnityEngine;

/// <summary>广播实际绑定的技能资料，并保留本次运行中最近点击的技能供查看。</summary>
[CreateAssetMenu(fileName = "ClickSkill", menuName = "Events/SkillClickEventSO")]
public sealed class SkillClickEventSO : BaseEventSO<SkillData>
{
    // 技能来自数据库；点击记录仅供运行时查看，不把数据库副本写回事件资产。
    [NonSerialized] private SkillData lastSkill;
    public SkillData LastSkill => lastSkill;

    public override void RaiseEvent(SkillData skill, object sender)
    {
        if (skill == null) throw new ArgumentNullException(nameof(skill));
        lastSkill = skill;
        base.RaiseEvent(skill, sender);
    }
}
