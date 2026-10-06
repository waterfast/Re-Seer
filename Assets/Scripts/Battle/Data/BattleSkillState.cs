using System;
using ReSeer.Skills;

namespace ReSeer.Battle
{
    /// <summary>一场战斗中的技能槽。技能资料共享，剩余 PP 属于当前精灵。</summary>
    public sealed class BattleSkillState
    {
        public SkillData Skill { get; }
        public int CurrentPp { get; private set; }

        public BattleSkillState(SkillData skill, int currentPp)
        {
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            SetCurrentPp(currentPp);
        }

        public void SetCurrentPp(int value)
        {
            if (value < 0 || value > Skill.maxPp)
                throw new ArgumentOutOfRangeException(nameof(value), "剩余 PP 必须在 0 和技能最大 PP 之间。");
            CurrentPp = value;
        }
    }
}
