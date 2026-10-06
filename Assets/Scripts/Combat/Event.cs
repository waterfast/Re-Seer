using System;
using System.Collections.Generic;

namespace ReSeer.Combat
{
    /// <summary>服务端结算过程状态，与客户端的事实记录分开。</summary>
    public enum EventState { Created, Running, Finished, Faulted }

    /// <summary>一段可执行的结算。Battle 的事件栈分配编号并保证子事件先完成。</summary>
    public abstract class Event
    {
        /// <summary>当前局分配的过程编号。</summary>
        public long Id { get; internal set; }
        /// <summary>父过程编号，根事件为零。</summary>
        public long ParentId { get; internal set; }
        /// <summary>生命周期状态；一个过程实例只允许执行一次。</summary>
        public EventState State { get; internal set; }
        /// <summary>过程执行仅限权威程序集；UI 不可提交任意事件。</summary>
        internal abstract IEnumerable<Event> Execute(Battle battle);
    }

    /// <summary>一次出招过程：检查资格、扣 PP、命中、依序执行挂载效果、完成。</summary>
    internal sealed class UseSkillEvent : Event
    {
        private readonly BattlePet actor;
        private readonly BattlePet target;
        private readonly int slot;
        internal UseSkillEvent(BattlePet actor, BattlePet target, int slot)
        { this.actor = actor; this.target = target; this.slot = slot; }
        internal override IEnumerable<Event> Execute(Battle battle)
        {
            if (!actor.CanUse(slot) || !target.IsAlive) yield break;
            var skill = actor.Skills[slot];
            actor.SpendPp(slot);
            battle.Record(this, "SkillStarted", actor.Id, target.Id, skill.Id);
            if (battle.Roll(skill.Accuracy))
            {
                var context = new EffectContext(actor, target, skill.Id);
                foreach (var effect in skill.Effects)
                {
                    if (!actor.IsAlive || !target.IsAlive) break;
                    var events = effect.CreateEvents(context) ?? throw new InvalidOperationException("效果返回了空事件流");
                    foreach (var child in events) yield return child;
                }
            }
            else battle.Record(this, "SkillMissed", actor.Id, target.Id, skill.Id);
            battle.Record(this, "SkillFinished", actor.Id, target.Id, skill.Id);
        }
    }

    /// <summary>固定伤害结算事件。之后可在此处加入伤害草案和事前、事后触发点。</summary>
    public sealed class DamageEvent : Event
    {
        private readonly BattlePet source;
        private readonly BattlePet target;
        private readonly int amount;
        private readonly string contentId;
        /// <summary>已经提交的实际损失，未执行时为零。</summary>
        public int ActualDamage { get; private set; }
        /// <summary>构造请求；执行时仍检查来源和目标属于本局。</summary>
        public DamageEvent(BattlePet source, BattlePet target, int amount, string contentId)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            this.amount = amount; this.contentId = contentId ?? "";
        }
        internal override IEnumerable<Event> Execute(Battle battle)
        {
            battle.ValidateMember(source); battle.ValidateMember(target);
            if (!target.IsAlive) yield break;
            ActualDamage = target.LoseHp(amount);
            battle.Record(this, "DamageApplied", source.Id, target.Id, contentId, ActualDamage, target.Hp);
            if (!target.IsAlive) battle.Record(this, "PetDefeated", source.Id, target.Id, contentId, 0, target.Hp);
            yield break;
        }
    }
}
