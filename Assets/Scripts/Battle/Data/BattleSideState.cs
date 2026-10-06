using System;
using System.Collections.Generic;
using System.Linq;

namespace ReSeer.Battle
{
    /// <summary>玩家和敌人共用的单方资料；未知的敌方队伍或背包条目可以不传入。</summary>
    public sealed class BattleSideState
    {
        public string PlayerId { get; }
        public BattlePetState ActivePet { get; private set; }
        public IReadOnlyList<BattlePetState> Pets { get; }
        public IReadOnlyList<BattleItemState> Items { get; }

        // 数量由当前队伍推导，死亡或恢复后不会留下过期的计数。
        // 敌方资料不完整时，仅统计已载入的队伍，不推断隐藏精灵数量。
        public int TotalPetCount => Pets.Count;
        public int DeadPetCount => Pets.Count(pet => pet.State == BattlePetStatus.Dead);
        public int AlivePetCount => TotalPetCount - DeadPetCount;

        public BattleSideState(string playerId, IEnumerable<BattlePetState> pets = null,
            BattlePetState activePet = null, IEnumerable<BattleItemState> items = null)
        {
            var petList = (pets ?? Array.Empty<BattlePetState>()).ToArray();
            var itemList = (items ?? Array.Empty<BattleItemState>()).ToArray();
            if (petList.Any(pet => pet == null) || petList.Distinct().Count() != petList.Length)
                throw new ArgumentException("精灵队伍不能包含空项或同一个精灵的重复引用。", nameof(pets));
            if (itemList.Any(item => item == null))
                throw new ArgumentException("背包不能包含空条目。", nameof(items));
            PlayerId = playerId ?? string.Empty;
            Pets = Array.AsReadOnly(petList);
            Items = Array.AsReadOnly(itemList);
            SetActivePet(activePet);
        }

        /// <summary>只更新当前出战引用；null 表示暂时没有出战精灵。</summary>
        public void SetActivePet(BattlePetState pet)
        {
            if (pet != null && (!Pets.Contains(pet) || pet.State != BattlePetStatus.Alive))
                throw new ArgumentException("出战精灵必须属于本方队伍，且处于存活状态。", nameof(pet));
            ActivePet = pet;
        }
    }
}
