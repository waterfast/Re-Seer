using System;
using System.Collections.Generic;
using ReSeer.Skills;
using UnityEngine;

namespace ReSeer.Battle.UI
{
    /// <summary>
    /// 临时测试数据源：手工造一份 BattleContext 与 BattleUiDataState，用来验证 BattleUIController 的接入链路。
    /// 只用于在编辑器和 Play 模式里看效果，正式战斗流程接上后删除本组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleUiTestDataSource : MonoBehaviour
    {
        [Tooltip("留空时自动取同一物体上的 BattleUIController。")]
        [SerializeField] private BattleUIController controller;

        [Header("我方测试精灵")]
        [SerializeField] private string selfPetId = "4500";
        [SerializeField] private string selfPetName = "武心婵";
        [SerializeField] private string selfElementId = "88";
        [SerializeField, Min(1)] private int selfLevel = 100;
        [SerializeField, Min(0)] private int selfCurrentHp = 300;
        [SerializeField, Min(1)] private int selfMaxHp = 300;

        [Header("对方测试精灵")]
        [SerializeField] private string enemyPetId = "4260";
        [SerializeField] private string enemyPetName = "湮灵";
        [SerializeField] private string enemyElementId = "90";
        [SerializeField, Min(1)] private int enemyLevel = 100;
        [SerializeField, Min(0)] private int enemyCurrentHp = 360;
        [SerializeField, Min(1)] private int enemyMaxHp = 360;

        [Header("测试技能")]
        [SerializeField] private SkillDatabaseSO skillDatabase;
        [Tooltip("写进 BattleUiDataState 的技能 ID，由 MainActionPanel 的技能库解析；留空表示不下发技能。")]
        [SerializeField] private string[] testSkillIds = { "demo_thunder", "demo_break", "demo_focus", "demo_spark" };
        [Tooltip("第五技能使用现有技能资料演示 UI；留空时隐藏第五技能。")]
        [SerializeField] private string testFifthSkillId = "demo_spark";
        [SerializeField, Min(0)] private int testSkillIndex;

        [Header("测试操作")]
        [SerializeField, Min(1)] private int damageAmount = 50;
        [SerializeField] private DamageNumberStyle damageType = DamageNumberStyle.Normal;
        [SerializeField] private bool autoStartOnPlay = true;
        [Tooltip("Play 模式快捷键：1 我方受伤，2 对方受伤，3 双方回满，4 切换我方出战；5 指定技能 PP 减一，6 第五技能 PP 减一，7 全部技能 PP 回满。")]
        [SerializeField] private bool enableKeyboardShortcuts = true;

        private BattlePetState selfPet;
        private BattlePetState enemyPet;
        private BattleSideState selfSide;
        private BattleSideState enemySide;
        private BattleContext context;
        private BattleUiDataState dataState;
        private BattleSkillState fifthSkill;

        public BattleContext Context => context;
        public BattleUiDataState DataState => dataState;

        // 用 Start 而不是 Awake，确保两侧 HpBar 的 Awake 已经跑完，避免初始化结果被覆盖。
        private void Start()
        {
            if (controller == null) controller = GetComponent<BattleUIController>();
            if (controller == null) controller = FindAnyObjectByType<BattleUIController>();
            if (autoStartOnPlay && controller != null) StartSession();
        }

        private void Update()
        {
            if (!enableKeyboardShortcuts || dataState == null) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) Damage(true, damageAmount);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Damage(false, damageAmount);
            if (Input.GetKeyDown(KeyCode.Alpha3)) HealAll();
            if (Input.GetKeyDown(KeyCode.Alpha4)) ToggleSelfActivePet();
            if (Input.GetKeyDown(KeyCode.Alpha5)) DrainSkillPp(testSkillIndex);
            if (Input.GetKeyDown(KeyCode.Alpha6)) DrainFifthSkillPp();
            if (Input.GetKeyDown(KeyCode.Alpha7)) RestoreSkillPp();
        }

        /// <summary>重建一份测试数据并重新接入控制器。</summary>
        public void StartSession()
        {
            if (controller == null) throw new InvalidOperationException("没有找到 BattleUIController。");
            if (skillDatabase == null) throw new InvalidOperationException("请配置测试数据源的技能库。");
            var skills = new List<BattleSkillState>();
            var ownedSkills = new List<SkillData>();
            foreach (string id in testSkillIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                BattleSkillState slot = CreateSkill(id);
                skills.Add(slot);
                ownedSkills.Add(slot.Skill);
            }
            fifthSkill = string.IsNullOrWhiteSpace(testFifthSkillId) ? null : CreateSkill(testFifthSkillId);
            selfPet = BuildPet(selfPetId, selfPetName, selfElementId, selfLevel, selfCurrentHp, selfMaxHp,
                ownedSkills, skills);
            enemyPet = BuildPet(enemyPetId, enemyPetName, enemyElementId, enemyLevel, enemyCurrentHp, enemyMaxHp);
            selfSide = new BattleSideState("test-self", new[] { selfPet }, selfPet);
            enemySide = new BattleSideState("test-enemy", new[] { enemyPet }, enemyPet);
            context = new BattleContext(selfSide, enemySide);
            dataState = new BattleUiDataState();
            PushHealth();

            controller.Initialize(context);
            controller.BindDataState(dataState);
            PushSkills();
            Debug.Log($"[BattleUiTest] 会话已建立：我方 {selfPet.Name} {selfPet.CurrentHp}/{selfPet.MaxHp}，"
                + $"对方 {enemyPet.Name} {enemyPet.CurrentHp}/{enemyPet.MaxHp}");
        }

        /// <summary>按已确认的结果扣血；伤害计算不归界面层，这里只做演示。</summary>
        public void Damage(bool self, int amount)
        {
            if (dataState == null || amount <= 0) return;
            BattlePetState pet = self ? selfSide.ActivePet : enemySide.ActivePet;
            if (pet == null) return;
            controller.TakeDamage(self, amount, damageType);
            Debug.Log($"[BattleUiTest] {(self ? "我方" : "对方")} {pet.Name} 体力 {pet.CurrentHp}/{pet.MaxHp}"
                + $"（{pet.State}）");
        }

        /// <summary>双方回满，同时验证从 0 血恢复到存活状态。</summary>
        public void HealAll()
        {
            if (dataState == null) return;
            if (selfSide.ActivePet != null) controller.Heal(true, selfPet.MaxHp);
            else selfPet.Heal(selfPet.MaxHp);
            if (enemySide.ActivePet != null) controller.Heal(false, enemyPet.MaxHp);
            else enemyPet.Heal(enemyPet.MaxHp);
            Debug.Log("[BattleUiTest] 双方恢复满体力");
        }

        /// <summary>在我方出战与空态之间切换，验证 HpBar 的绑定与清空。</summary>
        public void ToggleSelfActivePet()
        {
            if (dataState == null || selfSide == null) return;
            BattlePetState next = selfSide.ActivePet == null ? selfPet : null;
            if (next != null && next.State != BattlePetStatus.Alive)
            {
                Debug.LogWarning($"[BattleUiTest] {next.Name} 已倒下，先回满体力再切回出战。");
                return;
            }
            selfSide.SetActivePet(next);
            controller.Initialize(context);
            PushHealth();
            PushSkills();
            Debug.Log(next == null ? "[BattleUiTest] 我方切到空态" : $"[BattleUiTest] 我方切回 {next.Name}");
        }

        [ContextMenu("测试：我方受伤")]
        private void DamageSelf() => Damage(true, damageAmount);

        [ContextMenu("测试：对方受伤")]
        private void DamageEnemy() => Damage(false, damageAmount);

        [ContextMenu("测试：我方治疗")]
        private void HealSelf() => controller?.Heal(true, damageAmount);

        [ContextMenu("测试：对方治疗")]
        private void HealEnemy() => controller?.Heal(false, damageAmount);

        [ContextMenu("测试：我方倒下")]
        private void KillSelf()
        {
            if (selfSide?.ActivePet == null) return;
            controller.TakeDamage(true, selfPet.CurrentHp, damageType);
            Debug.Log($"[BattleUiTest] 我方 {selfPet.Name} 倒下（{selfPet.State}）");
        }

        [ContextMenu("测试：双方恢复满体力")]
        private void HealAllMenu() => HealAll();

        [ContextMenu("测试：切换我方出战/空态")]
        private void ToggleSelfMenu() => ToggleSelfActivePet();

        [ContextMenu("测试：重新建立会话")]
        private void RestartMenu() => StartSession();

        /// <summary>按配置的技能 ID 下发一份技能快照，驱动 BattleUIController → BattleActionController 链路。</summary>
        public void PushSkills()
        {
            if (dataState == null) return;
            var slots = new List<BattleUiSkillSlot>();
            if (selfSide.ActivePet != null)
            {
                foreach (BattleSkillState skill in selfSide.ActivePet.Skills) slots.Add(ToUiSlot(skill));
            }
            dataState.SetSkills(slots, selfSide.ActivePet != null && fifthSkill != null
                ? ToUiSlot(fifthSkill) : (BattleUiSkillSlot?)null);
        }

        [ContextMenu("测试：刷新技能槽")]
        private void PushSkillsMenu() => PushSkills();

        [ContextMenu("测试：技能 PP 减一")]
        private void DrainPp() => DrainSkillPp(testSkillIndex);

        public void DrainSkillPp(int index)
        {
            if (selfPet == null || index < 0 || index >= selfPet.Skills.Count) return;
            DrainPp(selfPet.Skills[index]);
        }

        [ContextMenu("测试：第五技能 PP 减一")]
        public void DrainFifthSkillPp() => DrainPp(fifthSkill);

        private void DrainPp(BattleSkillState skill)
        {
            if (dataState == null || skill == null) return;
            if (skill.CurrentPp == 0)
            {
                Debug.LogWarning("[BattleUiTest] 技能 PP 已经是 0，无法再减。");
                return;
            }
            skill.SetCurrentPp(skill.CurrentPp - 1);
            PushSkills();
        }

        [ContextMenu("测试：第五技能 PP 耗尽")]
        public void ExhaustFifthSkillPp()
        {
            if (fifthSkill == null) return;
            fifthSkill.SetCurrentPp(0);
            PushSkills();
        }

        [ContextMenu("测试：全部技能 PP 回满")]
        public void RestoreSkillPp()
        {
            if (selfPet == null) return;
            foreach (BattleSkillState skill in selfPet.Skills) skill.SetCurrentPp(skill.Skill.maxPp);
            if (fifthSkill != null) fifthSkill.SetCurrentPp(fifthSkill.Skill.maxPp);
            PushSkills();
        }

        private BattleSkillState CreateSkill(string id)
        {
            if (!skillDatabase.TryGet(id, out SkillData skill))
                throw new InvalidOperationException($"测试技能库中找不到技能：{id}");
            return new BattleSkillState(skill, skill.maxPp);
        }

        private static BattleUiSkillSlot ToUiSlot(BattleSkillState skill) =>
            new BattleUiSkillSlot(skill.Skill.id, skill.CurrentPp, skill.Skill.maxPp, true);

        private void PushHealth()
        {
            if (dataState == null) return;
            BattlePetState activeSelf = selfSide?.ActivePet;
            BattlePetState activeEnemy = enemySide?.ActivePet;
            dataState.SetHealth(true, activeSelf?.CurrentHp ?? 0, activeSelf?.MaxHp ?? 0);
            dataState.SetHealth(false, activeEnemy?.CurrentHp ?? 0, activeEnemy?.MaxHp ?? 0);
        }

        private static BattlePetState BuildPet(string id, string name, string elementId, int level, int currentHp, int maxHp,
            IEnumerable<SkillData> ownedSkills = null, IEnumerable<BattleSkillState> skills = null)
        {
            string[] elements = string.IsNullOrWhiteSpace(elementId) ? Array.Empty<string>() : new[] { elementId };
            if (elements.Length == 0) throw new ArgumentException("测试精灵至少需要一个属性编号。");
            int safeMaxHp = Mathf.Max(1, maxHp);
            return new BattlePetState(id, name, elements, Mathf.Max(1, level),
                Mathf.Clamp(currentHp, 0, safeMaxHp), safeMaxHp, ownedSkills, skills);
        }

        private void OnValidate()
        {
            selfMaxHp = Mathf.Max(1, selfMaxHp);
            enemyMaxHp = Mathf.Max(1, enemyMaxHp);
            selfLevel = Mathf.Max(1, selfLevel);
            enemyLevel = Mathf.Max(1, enemyLevel);
            selfCurrentHp = Mathf.Clamp(selfCurrentHp, 0, selfMaxHp);
            enemyCurrentHp = Mathf.Clamp(enemyCurrentHp, 0, enemyMaxHp);
            damageAmount = Mathf.Max(1, damageAmount);
        }
    }
}
