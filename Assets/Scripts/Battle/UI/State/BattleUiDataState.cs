using System;
using System.Collections.Generic;

namespace ReSeer.Battle.UI
{
    public readonly struct BattleUiSkillSlot
    {
        public readonly string SkillId;
        public readonly int CurrentPp;
        public readonly int MaxPp;
        public readonly bool Available;

        public BattleUiSkillSlot(string skillId, int currentPp, int maxPp, bool available)
        {
            if (string.IsNullOrWhiteSpace(skillId) || maxPp < 0) throw new ArgumentException("Invalid skill slot");
            SkillId = skillId;
            MaxPp = maxPp;
            CurrentPp = Math.Max(0, Math.Min(currentPp, maxPp));
            Available = available;
        }
    }

    // The battle client writes confirmed snapshots here; views subscribe to changes.
    public sealed class BattleUiDataState
    {
        private readonly List<BattleUiSkillSlot> skills = new List<BattleUiSkillSlot>();
        public IReadOnlyList<BattleUiSkillSlot> Skills => skills.AsReadOnly();
        public int SelfHp { get; private set; }
        public int SelfMaxHp { get; private set; }
        public int OpponentHp { get; private set; }
        public int OpponentMaxHp { get; private set; }
        public event Action<bool, int, int> HealthChanged;
        public event Action SkillsChanged;
        public event Action PetsChanged;
        public event Action ItemsChanged;

        public void SetHealth(bool self, int current, int maximum)
        {
            maximum = Math.Max(0, maximum);
            current = Math.Max(0, Math.Min(current, maximum));
            if (self)
            {
                if (SelfHp == current && SelfMaxHp == maximum) return;
                SelfHp = current;
                SelfMaxHp = maximum;
            }
            else
            {
                if (OpponentHp == current && OpponentMaxHp == maximum) return;
                OpponentHp = current;
                OpponentMaxHp = maximum;
            }
            HealthChanged?.Invoke(self, current, maximum);
        }

        public void SetSkills(IEnumerable<BattleUiSkillSlot> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            skills.Clear();
            skills.AddRange(snapshot);
            SkillsChanged?.Invoke();
        }

        public void NotifyPetsChanged() => PetsChanged?.Invoke();
        public void NotifyItemsChanged() => ItemsChanged?.Invoke();
    }

    // One-shot visual events are intentionally separate from the current HP snapshot.
    public readonly struct BattleDamagePresentation
    {
        public readonly string TargetId;
        public readonly int Amount;
        public readonly bool IsCritical;
        public BattleDamagePresentation(string targetId, int amount, bool isCritical)
        { TargetId = targetId; Amount = amount; IsCritical = isCritical; }
    }

    public sealed class BattlePresentationEvents
    {
        public event Action<BattleDamagePresentation> DamageRequested;
        public void ShowDamage(BattleDamagePresentation result) => DamageRequested?.Invoke(result);
    }
}
