using System;

namespace ReSeer.Battle
{
    /// <summary>战斗背包条目；暂时只保存名称。</summary>
    public sealed class BattleItemState
    {
        public string Name { get; }

        public BattleItemState(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("道具名称不能为空。", nameof(name));
            Name = name;
        }
    }
}
