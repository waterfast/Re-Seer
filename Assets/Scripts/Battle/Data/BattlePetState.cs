using System;
using System.Collections.Generic;
using System.Linq;
using ReSeer.Skills;

namespace ReSeer.Battle
{
    /// <summary>用于控制层和界面读取的本场精灵资料，不依赖养成存档或战斗结算核心。</summary>
    public sealed class BattlePetState
    {
        /// <summary>精灵编号，同时用于查找 avatar/编号.png 和 pets/编号.png。</summary>
        public string Id { get; }
        public string Name { get; }
        public int Level { get; }
        public int MaxHp { get; }
        /// <summary>元素属性编号，与技能库的 elementId 对应；支持双属性。</summary>
        public IReadOnlyList<string> Elements { get; }
        public int CurrentHp { get; private set; }
        public BattlePetStatus State { get; private set; }

        /// <summary>当前携带的技能槽，按显示顺序保存各技能的剩余 PP。</summary>
        public IReadOnlyList<BattleSkillState> Skills { get; }
        
        /// <summary>拥有的技能资料，包括未携带的技能。</summary>
        public IReadOnlyList<SkillData> OwnedSkills { get; }

        public BattlePetState(string id, string name, IEnumerable<string> elements, int level, int currentHp, int maxHp,
            IEnumerable<SkillData> ownedSkills = null, IEnumerable<BattleSkillState> skills = null)
        {
            if (string.IsNullOrEmpty(id) || id.Any(c => c < '0' || c > '9'))
                throw new ArgumentException("精灵编号必须为数字。", nameof(id));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (maxHp < 1) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("精灵名称不能为空。", nameof(name));
            var elementList = (elements ?? throw new ArgumentNullException(nameof(elements))).ToArray();
            if (elementList.Length == 0 || elementList.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("精灵至少需要一个有效属性。", nameof(elements));
            var ownedList = (ownedSkills ?? Array.Empty<SkillData>()).ToArray();
            var slots = (skills ?? Array.Empty<BattleSkillState>()).ToArray();
            if (ownedList.Any(skill => skill == null))
                throw new ArgumentException("拥有的技能不能包含空项。", nameof(ownedSkills));
            if (slots.Any(slot => slot == null || !ownedList.Any(skill => skill.id == slot.Skill.id)))
                throw new ArgumentException("携带的技能必须属于该精灵拥有的技能。", nameof(skills));

            Id = id;
            Name = name;
            Level = level;
            MaxHp = maxHp;
            Elements = Array.AsReadOnly(elementList);
            OwnedSkills = Array.AsReadOnly(ownedList);
            Skills = Array.AsReadOnly(slots);
            SetCurrentHp(currentHp);
        }

        public void SetCurrentHp(int value)
        {
            if (value < 0 || value > MaxHp) throw new ArgumentOutOfRangeException(nameof(value));
            CurrentHp = value;
            State = value == 0 ? BattlePetStatus.Dead : BattlePetStatus.Alive;
        }

        public void SetState(BattlePetStatus value)
        {
            if (!Enum.IsDefined(typeof(BattlePetStatus), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value != BattlePetStatus.Dead && CurrentHp == 0)
                throw new InvalidOperationException("体力为零的精灵需要先恢复体力。");
            State = value;
            if (value == BattlePetStatus.Dead) CurrentHp = 0;
        }
    }
}
