using UnityEngine;

namespace ReSeer.Battle.UI
{
    // 数字入口独立存放，避免和技能、PP、状态数据的修改混在一起。
    public sealed partial class BattleUIController
    {
        [Header("浮动数字（可手动绑定，也可按场景对象名解析）")]
        [SerializeField] private DamageDisplay enemyNumberDisplay;
        [SerializeField] private DamageDisplay selfNumberDisplay;

        private void ResolveNumberDisplays()
        {
            foreach (DamageDisplay display in FindObjectsByType<DamageDisplay>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (display.gameObject.scene != gameObject.scene) continue;
                // DamageDisplay 是已经摆放好的敌方数字实例，沿用其坐标和缩放。
                if (enemyNumberDisplay == null &&
                    (display.name == "DamageDisplay" || display.name == "EnemyDamageDisplay"))
                    enemyNumberDisplay = display;
                else if (selfNumberDisplay == null && display.name == "SelfDamageDisplay")
                    selfNumberDisplay = display;
            }
        }

        /// <summary>在已经摆好的敌方位置播放普通或暴击伤害。</summary>
        public void ShowDamage(int amount, bool critical = false) =>
            ShowNumber(false, amount, critical ? DamageNumberStyle.Critical : DamageNumberStyle.Normal);

        /// <summary>targetSelf 表示数字出现在哪一侧；amount 是已确认的数值，不在 UI 中计算。</summary>
        public void ShowNumber(bool targetSelf, long amount, DamageNumberStyle style,
            NumberSign sign = NumberSign.Auto)
        {
            DamageDisplay display = targetSelf ? selfNumberDisplay : enemyNumberDisplay;
            if (display == null)
            {
                ResolveNumberDisplays();
                display = targetSelf ? selfNumberDisplay : enemyNumberDisplay;
            }
            if (display == null)
            {
                Debug.LogWarning(targetSelf
                    ? "请绑定我方数字显示器，或放置名为 SelfDamageDisplay 的实例。"
                    : "请绑定敌方数字显示器，或放置名为 DamageDisplay / EnemyDamageDisplay 的实例。", this);
                return;
            }
            display.Show(amount, style, sign);
        }
    }
}
