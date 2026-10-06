using System;
using System.Collections.Generic;

namespace ReSeer.Combat
{
    /// <summary>可复用的只读行为。效果提出事件，不能直接扣血，也不保存本次施放状态。</summary>
    public abstract class Effect
    {
        /// <summary>根据当前上下文生成事件；迭代时可以读取前一个子事件的结算结果。</summary>
        public abstract IEnumerable<Event> CreateEvents(EffectContext context);
    }

    /// <summary>一次效果调用的只读上下文，未来 Skill、Trait、Status 可以共用。</summary>
    public sealed class EffectContext
    {
        /// <summary>效果拥有者；只有状态查询入口。</summary>
        public BattlePet Owner { get; }
        /// <summary>本次选中的目标。</summary>
        public BattlePet Target { get; }
        /// <summary>来源内容编号，不依赖技能显示名。</summary>
        public string SourceId { get; }
        internal EffectContext(BattlePet owner, BattlePet target, string sourceId)
        { Owner = owner; Target = target; SourceId = sourceId; }
    }

    /// <summary>本轮唯一的具体效果：固定数值伤害，用于验证组合和事件循环，不冒充完整伤害公式。</summary>
    public sealed class DamageEffect : Effect
    {
        /// <summary>请求伤害值；实际损失由 DamageEvent 夹限。</summary>
        public int Amount { get; }
        /// <summary>配置伤害，禁止负数。</summary>
        public DamageEffect(int amount)
        { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); Amount = amount; }
        /// <summary>提出一次伤害事件；技能挂两个本效果就执行两次。</summary>
        public override IEnumerable<Event> CreateEvents(EffectContext context)
        { yield return new DamageEvent(context.Owner, context.Target, Amount, context.SourceId); }
    }
}
