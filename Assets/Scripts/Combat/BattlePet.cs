using System;
using System.Collections.Generic;
using System.Linq;
using ReSeer.Pets;
using ReSeer.Combat.Contracts;

namespace ReSeer.Combat
{
    /// <summary>一局内的精灵状态。只能由 Battle 从可信 Pet 和服务器面板创建。</summary>
    public sealed class BattlePet
    {
        private readonly int[] pp;
        /// <summary>本局场上编号，不向前端泄漏持久化个体编号。</summary>
        public string Id { get; }
        /// <summary>只读持久化个体；权威核心使用，前端只接收快照。</summary>
        public Pet Pet { get; }
        /// <summary>开战时从养成资料计算的实际面板。</summary>
        public StatValues Stats { get; }
        /// <summary>当前生命，只有核心事件能够提交变化。</summary>
        public int Hp { get; private set; }
        /// <summary>本次战斗的生命上限。</summary>
        public int MaxHp => Stats.Hp;
        /// <summary>是否仍可行动。</summary>
        public bool IsAlive => Hp > 0;
        /// <summary>携带的只读招式，PP 分别存储在本实例中。</summary>
        public IReadOnlyList<Skill> Skills { get; }

        internal BattlePet(string id, Pet pet, StatValues stats, IReadOnlyDictionary<string, Skill> catalog)
        {
            if (stats == null || stats.Hp < 1 || stats.Attack < 1 || stats.Defense < 1 ||
                stats.SpecialAttack < 1 || stats.SpecialDefense < 1 || stats.Speed < 1)
                throw new ArgumentException("面板计算器必须返回六项正数");
            var skills = pet.EquippedSkills.Select(key =>
            {
                if (!catalog.TryGetValue(key, out var skill) || skill == null || skill.Id != key)
                    throw new ArgumentException("携带招式没有对应的服务器技能配置：" + key);
                return skill;
            }).ToArray();
            Id = id; Pet = pet; Stats = stats; Hp = stats.Hp;
            Skills = Array.AsReadOnly(skills); pp = skills.Select(s => s.MaxPp).ToArray();
        }

        /// <summary>查询某栏 PP，不改变次数。</summary>
        public int GetPp(int slot)
        { if (slot < 0 || slot >= pp.Length) throw new ArgumentOutOfRangeException(nameof(slot)); return pp[slot]; }
        /// <summary>查询当前是否可支付栏位费用；不做随机检查。</summary>
        public bool CanUse(int slot) => IsAlive && slot >= 0 && slot < Skills.Count && pp[slot] >= Skills[slot].PpCost;
        /// <summary>生成公开精灵快照。</summary>
        public PetSnapshot CreateSnapshot() => new PetSnapshot(Id, Pet.Nickname, Pet.Species.Id, Hp, MaxHp);
        internal IReadOnlyList<SkillSlotSnapshot> GetSkillSlots() => Array.AsReadOnly(Skills.Select((s, i) => new SkillSlotSnapshot(i, s.Id, pp[i], s.MaxPp)).ToArray());
        internal void SpendPp(int slot)
        { if (!CanUse(slot)) throw new InvalidOperationException("无法支付技能费用"); pp[slot] -= Skills[slot].PpCost; }
        internal int LoseHp(int amount)
        { if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount)); int actual = Math.Min(Hp, amount); Hp -= actual; return actual; }
    }
}
