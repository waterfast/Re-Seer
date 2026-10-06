using System;
using System.Collections.Generic;
using System.Linq;

namespace ReSeer.Pets
{
    /// <summary>玩家持有的精灵个体快照。保存养成资料，不保存某场战斗的血量或 PP。</summary>
    public sealed class Pet
    {
        /// <summary>持久化个体编号；同物种的两只精灵拥有不同编号。</summary>
        public string Id { get; }
        /// <summary>物种资料，包括种族值和可学习招式。</summary>
        public PetSpecies Species { get; }
        /// <summary>昵称；不参与规则判断。</summary>
        public string Nickname { get; }
        /// <summary>等级，由养成规则校验上下限。</summary>
        public int Level { get; }
        /// <summary>累计经验；升级曲线由养成服务负责，本类不推断经验对应等级。</summary>
        public long Experience { get; }
        /// <summary>个体值；取值上限由规则注入。</summary>
        public int IndividualValue { get; }
        /// <summary>性格编号；修正面板的算法由 IPetStatCalculator 提供。</summary>
        public string NatureId { get; }
        /// <summary>六项学习力，独立于种族值和实际面板。</summary>
        public StatValues Effort { get; }
        /// <summary>已经学会的招式编号，包含尚未携带的招式。</summary>
        public IReadOnlyList<string> LearnedSkills { get; }
        /// <summary>本次保存的携带顺序；战斗按此顺序创建独立 PP 栏位。</summary>
        public IReadOnlyList<string> EquippedSkills { get; }
        /// <summary>刻印装备快照；所有权和装备资格由服务器刻印服务校验。</summary>
        public IReadOnlyList<InscriptionSlot> Inscriptions { get; }

        /// <summary>根据可信存档创建只读个体；复制集合以隔离调用者后续修改。</summary>
        public Pet(string id, PetSpecies species, string nickname, int level, long experience,
            int individualValue, string natureId, StatValues effort, IEnumerable<string> learnedSkills,
            IEnumerable<string> equippedSkills, IEnumerable<InscriptionSlot> inscriptions, GrowthRules rules)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(natureId))
                throw new ArgumentException("个体编号和性格编号不能为空");
            Species = species ?? throw new ArgumentNullException(nameof(species));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            Effort = effort ?? throw new ArgumentNullException(nameof(effort));
            rules.Validate(level, experience, individualValue, effort);
            var learned = (learnedSkills ?? throw new ArgumentNullException(nameof(learnedSkills))).ToArray();
            var equipped = (equippedSkills ?? throw new ArgumentNullException(nameof(equippedSkills))).ToArray();
            if (learned.Any(s => string.IsNullOrWhiteSpace(s) || !species.CanLearn(s)) || learned.Distinct().Count() != learned.Length)
                throw new ArgumentException("已学习招式必须唯一且属于物种学习表");
            if (equipped.Length > rules.MaxSkillSlots || equipped.Distinct().Count() != equipped.Length ||
                equipped.Any(s => !learned.Contains(s))) throw new ArgumentException("携带招式必须已学会、无重复且不超过栏位上限");
            var slots = (inscriptions ?? Array.Empty<InscriptionSlot>()).ToArray();
            if (slots.Any(s => s == null || s.Index >= rules.MaxInscriptionSlots) ||
                slots.Select(s => s.Index).Distinct().Count() != slots.Length ||
                slots.Select(s => s.InstanceId).Distinct().Count() != slots.Length)
                throw new ArgumentException("刻印栏位越界、重复，或同一刻印重复装备");
            Id = id; Nickname = string.IsNullOrWhiteSpace(nickname) ? species.Name : nickname;
            Level = level; Experience = experience; IndividualValue = individualValue; NatureId = natureId;
            LearnedSkills = Array.AsReadOnly(learned); EquippedSkills = Array.AsReadOnly(equipped);
            Inscriptions = Array.AsReadOnly(slots);
        }
    }
}
