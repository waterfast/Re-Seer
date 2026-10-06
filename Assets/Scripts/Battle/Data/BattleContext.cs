using System;

namespace ReSeer.Battle
{
    /// <summary>一场战斗的信息入口。控制层更新数据，玩家和敌人界面读取各自的 Side。</summary>
    public sealed class BattleContext
    {
        public BattleSideState PlayerSide { get; }//玩家方数据
        public BattleSideState EnemySide { get; }//敌人方数据
        /// <summary>初始为 0，尚未开始第一回合。</summary>
        public int Turn { get; private set; }//当前战斗回合

        /// <summary>构造函数</summary>
        public BattleContext(BattleSideState playerSide, BattleSideState enemySide)
        {
            PlayerSide = playerSide ?? throw new ArgumentNullException(nameof(playerSide));
            EnemySide = enemySide ?? throw new ArgumentNullException(nameof(enemySide));
        }

        /// <summary>设置当前战斗回合</summary>
        public void SetTurn(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Turn = value;
        }

    }
}
