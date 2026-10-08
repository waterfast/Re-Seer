using System;
using System.Collections.Generic;
using ReSeer.Battle.UI;
using ReSeer.Skills;

namespace ReSeer.Battle.Network
{
    /// <summary>Validates a complete server snapshot before updating the display model.</summary>
    public sealed class BattleSnapshotState
    {
        public BattleContext Context { get; private set; }
        public BattleUiDataState UiState { get; } = new BattleUiDataState();
        public BattleSnapshot Snapshot { get; private set; }

        public bool Apply(BattleSnapshot next, SkillDatabaseSO catalog)
        {
            if (next == null || string.IsNullOrEmpty(next.battleId) || next.sequence < 1 || next.round < 0)
                throw new ArgumentException("战斗快照不完整。");
            if (Snapshot != null)
            {
                if (Snapshot.battleId != next.battleId) throw new ArgumentException("收到其他战斗的数据。");
                if (next.sequence <= Snapshot.sequence) return false;
            }
            ValidatePet(next.selfPet);
            ValidatePet(next.enemyPet);
            if (next.selfPet.petId == next.enemyPet.petId) throw new ArgumentException("双方精灵实例编号重复。");
            if (next.canAct && (next.finished || next.promptId < 1)) throw new ArgumentException("行动询问不合法。");
            if (next.skills == null || next.skills.Length > 4) throw new ArgumentException("技能槽不合法。");
            var slots = new List<BattleUiSkillSlot>();
            var ids = new HashSet<string>();
            foreach (var skill in next.skills)
            {
                var slot = ValidateSkill(skill, catalog, next.canAct);
                if (!ids.Add(slot.SkillId)) throw new ArgumentException("技能槽重复。");
                slots.Add(slot);
            }
            BattleUiSkillSlot? fifth = null;
            if (next.fifthSkill != null && !string.IsNullOrEmpty(next.fifthSkill.skillId))
            {
                fifth = ValidateSkill(next.fifthSkill, catalog, next.canAct);
                if (!ids.Add(fifth.Value.SkillId)) throw new ArgumentException("第五技能与普通技能重复。");
            }
            foreach (var visual in next.events ?? Array.Empty<BattleVisualEvent>())
                if (visual == null || visual.amount < 0 ||
                    (visual.targetPetId != next.selfPet.petId && visual.targetPetId != next.enemyPet.petId))
                    throw new ArgumentException("战斗表现事件不合法。");

            if (Context == null)
            {
                var self = CreatePet(next.selfPet);
                var enemy = CreatePet(next.enemyPet);
                Context = new BattleContext(new BattleSideState("1", new[] { self }, self),
                    new BattleSideState("2", new[] { enemy }, enemy));
            }
            else
            {
                // Stage one has one active pet on each side; switching is a later feature.
                ValidateIdentity(Snapshot.selfPet, next.selfPet);
                ValidateIdentity(Snapshot.enemyPet, next.enemyPet);
                Context.PlayerSide.ActivePet.SetCurrentHp(next.selfPet.currentHp);
                Context.EnemySide.ActivePet.SetCurrentHp(next.enemyPet.currentHp);
            }
            Snapshot = next;
            Context.SetTurn(next.round);
            UiState.SetHealth(true, next.selfPet.currentHp, next.selfPet.maxHp);
            UiState.SetHealth(false, next.enemyPet.currentHp, next.enemyPet.maxHp);
            UiState.SetSkills(slots, fifth);
            return true;
        }

        public void LockInput()
        {
            var slots = new List<BattleUiSkillSlot>();
            foreach (var slot in UiState.Skills)
                slots.Add(new BattleUiSkillSlot(slot.SkillId, slot.CurrentPp, slot.MaxPp, false));
            BattleUiSkillSlot? fifth = UiState.FifthSkill;
            if (fifth.HasValue)
                fifth = new BattleUiSkillSlot(fifth.Value.SkillId, fifth.Value.CurrentPp, fifth.Value.MaxPp, false);
            UiState.SetSkills(slots, fifth);
        }

        public void RestoreInput()
        {
            if (Snapshot == null) return;
            var slots = new List<BattleUiSkillSlot>();
            foreach (var skill in Snapshot.skills)
                slots.Add(new BattleUiSkillSlot(skill.skillId, skill.currentPp, skill.maxPp,
                    Snapshot.canAct && skill.available));
            var fifth = Snapshot.fifthSkill;
            UiState.SetSkills(slots, fifth != null && !string.IsNullOrEmpty(fifth.skillId)
                ? new BattleUiSkillSlot(fifth.skillId, fifth.currentPp, fifth.maxPp,
                    Snapshot.canAct && fifth.available) : (BattleUiSkillSlot?)null);
        }

        private static BattleUiSkillSlot ValidateSkill(BattleSkillSnapshot skill, SkillDatabaseSO catalog, bool canAct)
        {
            if (skill == null || catalog == null || !catalog.TryGet(skill.skillId, out var definition) ||
                skill.maxPp != definition.maxPp || skill.currentPp < 0 || skill.currentPp > skill.maxPp)
                throw new ArgumentException("服务器技能与本地技能库不匹配。");
            return new BattleUiSkillSlot(skill.skillId, skill.currentPp, skill.maxPp, canAct && skill.available);
        }

        private static void ValidatePet(BattlePetSnapshot pet)
        {
            if (pet == null || string.IsNullOrEmpty(pet.petId) || pet.maxHp < 1 ||
                pet.currentHp < 0 || pet.currentHp > pet.maxHp)
                throw new ArgumentException("精灵快照不合法。");
            // Constructor validates display identity, element IDs and level as well.
            CreatePet(pet);
        }

        private static BattlePetState CreatePet(BattlePetSnapshot pet) =>
            new BattlePetState(pet.speciesId, pet.name, pet.elementIds, pet.level, pet.currentHp, pet.maxHp);

        private static void ValidateIdentity(BattlePetSnapshot before, BattlePetSnapshot after)
        {
            if (before.petId != after.petId || before.speciesId != after.speciesId || before.maxHp != after.maxHp)
                throw new ArgumentException("第一阶段不支持替换精灵或改变最大体力。");
        }
    }
}
