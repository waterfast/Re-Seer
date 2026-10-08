using System.Collections.Generic;
using ReSeer.Battle.UI;
using UnityEngine;

[DisallowMultipleComponent]
public class BattleActionController : MonoBehaviour
{
    private MainActionPanel actionPanel;

    private void Awake() => ResolveComponent();

    /// <summary>集中解析操作区依赖的界面对象；已经解析到的引用会被保留。</summary>
    [ContextMenu("重新解析操作区引用")]
    public void ResolveComponent()
    {
        if (actionPanel == null)
            actionPanel = FindAnyObjectByType<MainActionPanel>(FindObjectsInactive.Include);
        if (actionPanel == null)
            Debug.LogError("战斗操作区找不到 MainActionPanel，技能信息无法显示。", this);
    }

    /// <summary>
    /// 用已确认的技能快照刷新技能槽。技能 ID 到技能定义的解析在 MainActionPanel 里完成，
    /// 这里只负责把上层的数据转交给操作区视图。
    /// </summary>
    public void ShowSkills(IReadOnlyList<BattleUiSkillSlot> skills, BattleUiSkillSlot? fifthSkill = null)
    {
        ResolveComponent();
        if (actionPanel != null) actionPanel.ShowSkills(skills, fifthSkill);
    }

    public void OnMenuActionReceived(BattleMenuAction action)
    {
        Debug.Log($"收到菜单操作命令：{action}", this);
    }
}
