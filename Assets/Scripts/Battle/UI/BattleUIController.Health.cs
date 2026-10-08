using System;

namespace ReSeer.Battle.UI
{
    public sealed partial class BattleUIController
    {
        /// <summary>应用已确认的伤害，同时更新精灵状态、血条和对应类型的伤害数字。</summary>
        public int TakeDamage(bool targetSelf, int amount, DamageNumberStyle damageType = DamageNumberStyle.Normal)
        {
            if (damageType != DamageNumberStyle.Normal && damageType != DamageNumberStyle.Critical
                && damageType != DamageNumberStyle.FixedDamage && damageType != DamageNumberStyle.TrueDamage)
                throw new ArgumentOutOfRangeException(nameof(damageType), "伤害类型不能使用治疗样式。");
            BattlePetState pet = GetActivePet(targetSelf);
            int applied = pet.TakeDamage(amount);
            PresentHealthChange(targetSelf, pet, applied, damageType);
            return applied;
        }

        /// <summary>应用已确认的治疗；浮动数字显示实际恢复量，满血或零治疗不播放数字。</summary>
        public int Heal(bool targetSelf, int amount)
        {
            BattlePetState pet = GetActivePet(targetSelf);
            int applied = pet.Heal(amount);
            PresentHealthChange(targetSelf, pet, applied, DamageNumberStyle.Healing);
            return applied;
        }

        private BattlePetState GetActivePet(bool self)
        {
            if (context == null) throw new InvalidOperationException("请先 Initialize 战斗上下文。");
            BattlePetState pet = self ? context.PlayerSide.ActivePet : context.EnemySide.ActivePet;
            return pet ?? throw new InvalidOperationException("该侧没有出战精灵。");
        }

        private void PresentHealthChange(bool self, BattlePetState pet, int applied, DamageNumberStyle style)
        {
            if (applied == 0) return;
            // 快照通知负责刷新血条，避免再调用一次 SetHealth 重启动画。
            if (dataState != null) dataState.SetHealth(self, pet.CurrentHp, pet.MaxHp);
            else SetHealth(self, pet.CurrentHp, pet.MaxHp);
            ShowNumber(self, applied, style);
        }
    }
}
