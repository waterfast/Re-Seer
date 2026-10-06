using System;
using System.Collections.Generic;
using System.Linq;

namespace ReSeer.Combat
{
    /// <summary>招式分类；完整伤害公式接入后按物理或特殊类别选择攻防。</summary>
    public enum SkillCategory { Physical, Special, Status }

    /// <summary>共享的主动招式资料。出招行为全部来自 Effects，不隐式追加一次伤害。</summary>
    public sealed class Skill
    {
        /// <summary>稳定内容编号。</summary>
        public string Id { get; }
        /// <summary>招式名称。</summary>
        public string Name { get; }
        /// <summary>物理、特殊或属性招式；分类本身不会隐式产生伤害。</summary>
        public SkillCategory Category { get; }
        /// <summary>招式元素编号，与使用者物种属性分开。</summary>
        public string ElementId { get; }
        /// <summary>基础威力，预留给威力伤害效果；当前固定伤害效果不读取此值。</summary>
        public int Power { get; }
        /// <summary>初始 PP 上限。</summary>
        public int MaxPp { get; }
        /// <summary>每次允许出招后支付的 PP。</summary>
        public int PpCost { get; }
        /// <summary>先制等级；先制相同时才比较速度。</summary>
        public int Priority { get; }
        /// <summary>基础命中万分比。必中骨架可配置 10000。</summary>
        public int Accuracy { get; }
        /// <summary>依序执行的效果；当前切片只支持命中后的敌方单体效果。</summary>
        public IReadOnlyList<Effect> Effects { get; }
        /// <summary>创建并固定技能；空效果列表合法，代表没有额外状态变更。</summary>
        public Skill(string id, string name, int maxPp, IEnumerable<Effect> effects,
            int ppCost = 1, int priority = 0, int accuracy = 10000,
            SkillCategory category = SkillCategory.Physical, string elementId = "normal", int power = 0)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("技能编号和名称不能为空");
            if (maxPp < 1 || ppCost < 1 || ppCost > maxPp || accuracy < 0 || accuracy > 10000) throw new ArgumentOutOfRangeException(nameof(maxPp));
            if (power < 0 || !Enum.IsDefined(typeof(SkillCategory), category)) throw new ArgumentOutOfRangeException(nameof(power));
            if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("技能元素编号不能为空");
            var copy = (effects ?? throw new ArgumentNullException(nameof(effects))).ToArray();
            if (copy.Any(e => e == null)) throw new ArgumentException("效果不能为空");
            Id = id; Name = name; MaxPp = maxPp; PpCost = ppCost; Priority = priority; Accuracy = accuracy;
            Category = category; ElementId = elementId; Power = power;
            Effects = Array.AsReadOnly(copy);
        }
    }
}
