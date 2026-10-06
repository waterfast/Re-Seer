using System.Collections.Generic;
using System.Linq;

namespace ReSeer.Combat.Contracts
{
    /// <summary>对局公开阶段；正在结算时不接受新输入。</summary>
    public enum BattlePhase { WaitingForCommands, Resolving, Finished, Faulted }
    /// <summary>公开结果；胜方另以参与者编号表达。</summary>
    public enum BattleOutcome { Ongoing, Victory, Draw, Faulted }

    /// <summary>可传给前端的不可变公共快照。隐藏对方 PP、未使用招式、养成存档和随机状态。</summary>
    public sealed class BattleSnapshot
    {
        /// <summary>对局编号。</summary>
        public string BattleId { get; }
        /// <summary>已经开始结算的回合数。</summary>
        public int Round { get; }
        /// <summary>下一次可提交命令的回合数。</summary>
        public int ExpectedRound => Round + 1;
        /// <summary>当前阶段。</summary>
        public BattlePhase Phase { get; }
        /// <summary>战斗结果。</summary>
        public BattleOutcome Outcome { get; }
        /// <summary>胜方参与者编号；尚未结束或平局时为空。</summary>
        public string WinnerId { get; }
        /// <summary>双方可公开的精灵状态。</summary>
        public IReadOnlyList<PetSnapshot> Pets { get; }
        /// <summary>构造独立列表，不共享服务端可写状态。</summary>
        public BattleSnapshot(string battleId, int round, BattlePhase phase, BattleOutcome outcome,
            string winnerId, IEnumerable<PetSnapshot> pets)
        { BattleId = battleId; Round = round; Phase = phase; Outcome = outcome; WinnerId = winnerId; Pets = System.Array.AsReadOnly(pets.ToArray()); }
    }

    /// <summary>场上精灵公共数据，不暴露 Pet 存档或计算器。</summary>
    public sealed class PetSnapshot
    {
        /// <summary>场上编号，与存档编号分离。</summary>
        public string Id { get; }
        /// <summary>显示名。</summary>
        public string Name { get; }
        /// <summary>物种编号。</summary>
        public string SpeciesId { get; }
        /// <summary>当前生命。</summary>
        public int Hp { get; }
        /// <summary>生命上限。</summary>
        public int MaxHp { get; }
        /// <summary>复制公开状态。</summary>
        public PetSnapshot(string id, string name, string speciesId, int hp, int maxHp)
        { Id = id; Name = name; SpeciesId = speciesId; Hp = hp; MaxHp = maxHp; }
    }

    /// <summary>只发给本人会话的技能栏位信息。</summary>
    public sealed class SkillSlotSnapshot
    {
        /// <summary>技能栏位。</summary>
        public int Slot { get; }
        /// <summary>技能编号。</summary>
        public string SkillId { get; }
        /// <summary>当前 PP。</summary>
        public int Pp { get; }
        /// <summary>PP 上限。</summary>
        public int MaxPp { get; }
        /// <summary>复制自己的技能栏位。</summary>
        public SkillSlotSnapshot(int slot, string skillId, int pp, int maxPp)
        { Slot = slot; SkillId = skillId; Pp = pp; MaxPp = maxPp; }
    }
}
