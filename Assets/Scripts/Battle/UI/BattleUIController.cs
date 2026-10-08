using System;
using UnityEngine;

namespace ReSeer.Battle.UI
{
    /// <summary>
    /// 战斗页面协调入口：持有 HUD 两侧头像槽，把上层确认的精灵与体力数据推送到视图。
    /// 下方操作区的技能信息经由 BattleActionController 转发，本类不直接接触技能面板。
    /// 应用已确认的体力变化并同步显示，不计算伤害公式、不判断行动是否合法。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class BattleUIController : MonoBehaviour
    {
        // 两侧头像槽按场景对象名查找；改名后需要同步这里的常量。
        private const string SelfHpBarName = "SelfHpBar";
        private const string EnemyHpBarName = "EnemyHpBar";

        private HpBar selfHpBar;
        private HpBar enemyHpBar;

        // 下方操作区的入口；技能面板由它自己解析，这里不越层持有。
        private BattleActionController actionController;

        // 战斗应用层写入的确认快照；订阅后由它推送体力与技能变化。
        private BattleUiDataState dataState;
        private BattleContext context;

        private void Awake() => ResolveComponent();

        private void OnDestroy() => UnbindDataState();

        /// <summary>用一场战斗的上下文初始化两侧头像槽；没有出战精灵的一侧显示空态。</summary>
        public void Initialize(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            this.context = context;
            ResolveComponent();
            BindOrClear(selfHpBar, context.PlayerSide.ActivePet);
            BindOrClear(enemyHpBar, context.EnemySide.ActivePet);
        }

        /// <summary>接入战斗数据快照；之后由它推送已确认的体力与技能变化。</summary>
        public void BindDataState(BattleUiDataState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            UnbindDataState();
            dataState = state;
            dataState.HealthChanged += OnHealthChanged;
            dataState.SkillsChanged += OnSkillsChanged;
            ResolveComponent();
            ApplyHealthSnapshot();
            ApplySkillSnapshot();
        }

        /// <summary>解除数据快照订阅，不改变当前显示。</summary>
        public void UnbindDataState()
        {
            if (dataState == null) return;
            dataState.HealthChanged -= OnHealthChanged;
            dataState.SkillsChanged -= OnSkillsChanged;
            dataState = null;
        }

        /// <summary>只更新体力数字与血条；头像、名字和等级沿用已绑定的精灵。</summary>
        /// <remarks>maximum 为 0 表示该侧尚无确认数据，HpBar 无法表示空条，此时保持现状。</remarks>
        public void SetHealth(bool self, int current, int maximum, bool animate = true)
        {
            if (maximum <= 0) return;
            HpBar bar = self ? selfHpBar : enemyHpBar;
            if (bar != null) bar.SetHealth(current, maximum, animate);
        }

        /// <summary>清空两侧头像槽，保留框体与布局。</summary>
        public void ClearBars()
        {
            if (selfHpBar != null) selfHpBar.ClearPet();
            if (enemyHpBar != null) enemyHpBar.ClearPet();
        }

        /// <summary>集中解析本控制器依赖的视图与协作对象；已经解析到的引用会被保留。</summary>
        [ContextMenu("重新解析战斗界面引用")]
        public void ResolveComponent()
        {
            ResolveHpBars();
            ResolveNumberDisplays();
            if (actionController == null)
                actionController = FindAnyObjectByType<BattleActionController>(FindObjectsInactive.Include);
            if (actionController == null)
                Debug.LogError("战斗界面找不到 BattleActionController，技能信息无法下发。", this);
        }

        /// <summary>按场景对象名查找两侧头像槽；两边都找到后不再重复查找。</summary>
        private void ResolveHpBars()
        {
            if (selfHpBar != null && enemyHpBar != null) return;
            foreach (HpBar bar in FindObjectsByType<HpBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (selfHpBar == null && IsNamed(bar, SelfHpBarName)) selfHpBar = bar;
                else if (enemyHpBar == null && IsNamed(bar, EnemyHpBarName)) enemyHpBar = bar;
            }
            if (selfHpBar == null || enemyHpBar == null)
                Debug.LogError($"战斗 HUD 体力条查找失败：场景中需要有名为 {SelfHpBarName} 和 {EnemyHpBarName} 的 HpBar。", this);
        }

        private void OnHealthChanged(bool self, int current, int maximum) => SetHealth(self, current, maximum);

        private void OnSkillsChanged() => ApplySkillSnapshot();

        /// <summary>把已确认的技能快照交给操作区，由它刷新技能槽。</summary>
        private void ApplySkillSnapshot()
        {
            if (actionController == null || dataState == null) return;
            actionController.ShowSkills(dataState.Skills, dataState.FifthSkill);
        }

        private void ApplyHealthSnapshot()
        {
            if (dataState == null) return;
            // 用已确认的血量覆盖 Inspector 预览值，避免初始化时出现 0/1 的空态。
            SetHealth(true, dataState.SelfHp, dataState.SelfMaxHp, false);
            SetHealth(false, dataState.OpponentHp, dataState.OpponentMaxHp, false);
        }

        private static void BindOrClear(HpBar bar, BattlePetState pet)
        {
            if (bar == null) return;
            if (pet == null) bar.ClearPet();
            else bar.BindPet(pet);
        }

        private static bool IsNamed(HpBar bar, string name) =>
            bar != null && string.Equals(bar.gameObject.name, name, StringComparison.OrdinalIgnoreCase);
    }
}
