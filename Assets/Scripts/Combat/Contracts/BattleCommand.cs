namespace ReSeer.Combat.Contracts
{
    /// <summary>前端提交的选招意图。没有攻击力、伤害、随机种子或可伪造的行动者属性。</summary>
    public sealed class BattleCommand
    {
        /// <summary>目标对局编号，拒绝跨局的迟到请求。</summary>
        public string BattleId { get; }
        /// <summary>期望执行的回合，从一开始，用于拒绝过期和重放命令。</summary>
        public int Round { get; }
        /// <summary>从零开始的技能栏位；null 表示主动等待。</summary>
        public int? SkillSlot { get; }
        /// <summary>构造传输意图；权威战斗收到后仍会完整验证。</summary>
        public BattleCommand(string battleId, int round, int? skillSlot)
        { BattleId = battleId; Round = round; SkillSlot = skillSlot; }
    }
}
