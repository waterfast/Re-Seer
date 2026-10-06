using System;
using System.Collections.Generic;
using ReSeer.Pets;
using ReSeer.Skills;
using UnityEngine;

public class SkillPanel : MonoBehaviour
{
    [SerializeField] private Transform skillContainer;
    [SerializeField] private SkillButton skillButtonPrefab;

    private readonly List<SkillButton> skillButtons = new();
    public event Action<SkillData> SkillSelected;

    public void SetLayout(float x, float y, float spacing)
    {
        if (skillContainer == null) return;
        var position = skillContainer.localPosition;
        skillContainer.localPosition = new Vector3(x, y, position.z);
        var layout = skillContainer.GetComponent<SkillSlotLayout>();
        if (layout != null) layout.SetHorizontalSpacing(spacing);
    }


    //渲染技能
    public void ShowSkills(IReadOnlyList<SkillData> skills, IReadOnlyList<int> currentPp)
    {
        if (skills == null) throw new ArgumentNullException(nameof(skills));
        if (currentPp == null || currentPp.Count != skills.Count)
            throw new ArgumentException("每个技能必须有一项已确认的当前 PP。", nameof(currentPp));
        if (skillContainer == null || skillButtonPrefab == null)
            throw new InvalidOperationException("请配置技能容器和 SkillButton 预制体。");
        for (int i = 0; i < skills.Count; i++)
            if (skills[i] == null) throw new ArgumentException("技能列表含空项。", nameof(skills));
        ClearSkills();

        for (int i = 0; i < skills.Count; i++)
        {
            SkillButton button = Instantiate(
                skillButtonPrefab,
                skillContainer
            );

            // 编辑模式的 Test 槽只用于看布局，不写入场景或预制体。
            if (!Application.isPlaying)
                button.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            button.Bind(skills[i], currentPp[i]);
            button.SkillSelected += OnSkillClicked;

            skillButtons.Add(button);
        }

        var layout = skillContainer.GetComponent<SkillSlotLayout>();
        if (layout != null) layout.Arrange();
    }

    private void ClearSkills()
    {
        foreach (var button in skillButtons)
        {
            if (button == null) continue;
            button.SkillSelected -= OnSkillClicked;
        }
        skillButtons.Clear();
        if (skillContainer == null) return;
        // 容器专属普通技能槽；按实际子物体清理，防止脚本重载后临时列表丢失留下旧槽。
        for (int i = skillContainer.childCount - 1; i >= 0; i--)
        {
            var button = skillContainer.GetChild(i).GetComponent<SkillButton>();
            if (button == null) continue;
            button.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(button.gameObject);
            else DestroyImmediate(button.gameObject);
        }
    }

    public void ClearTestPreview()
    {
        if (Application.isPlaying || skillContainer == null) return;
        for (int i = skillContainer.childCount - 1; i >= 0; i--)
        {
            var slot = skillContainer.GetChild(i).gameObject;
            if ((slot.hideFlags & HideFlags.DontSaveInEditor) != 0)
                DestroyImmediate(slot);
        }
    }

    private void OnSkillClicked(SkillData skill)
    {
        SkillSelected?.Invoke(skill);

        // 后面不要直接算伤害
        // 而是通知 BattleUIController / BattleActionController
    }

    private void OnDestroy()
    {
        foreach (var button in skillButtons)
            if (button != null) button.SkillSelected -= OnSkillClicked;
    }
}
