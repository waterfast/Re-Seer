using System;

namespace ReSeer.Pets
{
    /// <summary>不可变的六项数值。可表达种族值、学习力或面板，各调用处负责其业务约束。</summary>
    public sealed class StatValues
    {
        /// <summary>生命数值。</summary>
        public int Hp { get; }
        /// <summary>物理攻击数值。</summary>
        public int Attack { get; }
        /// <summary>物理防御数值。</summary>
        public int Defense { get; }
        /// <summary>特殊攻击数值。</summary>
        public int SpecialAttack { get; }
        /// <summary>特殊防御数值。</summary>
        public int SpecialDefense { get; }
        /// <summary>速度数值。</summary>
        public int Speed { get; }
        /// <summary>总和使用长整数，防止校验学习力时发生溢出。</summary>
        public long Total => (long)Hp + Attack + Defense + SpecialAttack + SpecialDefense + Speed;
        /// <summary>构造六项非负数值。</summary>
        public StatValues(int hp, int attack, int defense, int specialAttack, int specialDefense, int speed)
        {
            if (hp < 0 || attack < 0 || defense < 0 || specialAttack < 0 || specialDefense < 0 || speed < 0)
                throw new ArgumentOutOfRangeException(nameof(hp), "六项数值不能为负");
            Hp = hp; Attack = attack; Defense = defense; SpecialAttack = specialAttack; SpecialDefense = specialDefense; Speed = speed;
        }
    }
}
