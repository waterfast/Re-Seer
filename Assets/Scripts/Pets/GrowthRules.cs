using System;

namespace ReSeer.Pets
{
    /// <summary>注入的养成上限，不在个体或战斗循环中写死特定版本的游戏数值。</summary>
    public sealed class GrowthRules
    {
        /// <summary>最高等级。</summary>
        public int MaxLevel { get; }
        /// <summary>个体值上限。</summary>
        public int MaxIndividualValue { get; }
        /// <summary>单项学习力上限。</summary>
        public int MaxEffortPerStat { get; }
        /// <summary>学习力总上限。</summary>
        public int MaxTotalEffort { get; }
        /// <summary>可携带技能栏位数。</summary>
        public int MaxSkillSlots { get; }
        /// <summary>刻印栏位数，允许为零。</summary>
        public int MaxInscriptionSlots { get; }
        /// <summary>由服务器选定养成规则版本后提供上限。</summary>
        public GrowthRules(int maxLevel, int maxIndividualValue, int maxEffortPerStat,
            int maxTotalEffort, int maxSkillSlots, int maxInscriptionSlots)
        {
            if (maxLevel < 1 || maxIndividualValue < 0 || maxEffortPerStat < 0 || maxTotalEffort < 0 || maxSkillSlots < 1 || maxInscriptionSlots < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLevel));
            MaxLevel = maxLevel; MaxIndividualValue = maxIndividualValue; MaxEffortPerStat = maxEffortPerStat;
            MaxTotalEffort = maxTotalEffort; MaxSkillSlots = maxSkillSlots; MaxInscriptionSlots = maxInscriptionSlots;
        }
        /// <summary>校验基础养成范围；经验曲线、性格资格与存档所有权由外层服务校验。</summary>
        public void Validate(int level, long experience, int individualValue, StatValues effort)
        {
            if (effort == null) throw new ArgumentNullException(nameof(effort));
            if (level < 1 || level > MaxLevel || experience < 0 || individualValue < 0 || individualValue > MaxIndividualValue ||
                effort.Total > MaxTotalEffort || effort.Hp > MaxEffortPerStat || effort.Attack > MaxEffortPerStat ||
                effort.Defense > MaxEffortPerStat || effort.SpecialAttack > MaxEffortPerStat ||
                effort.SpecialDefense > MaxEffortPerStat || effort.Speed > MaxEffortPerStat)
                throw new ArgumentOutOfRangeException(nameof(level), "等级、经验、个体值或学习力超出规则范围");
        }
    }
}
