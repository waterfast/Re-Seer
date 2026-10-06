using UnityEngine;

namespace ReSeer.Battle.UI
{
    /// <summary>用户请求的菜单操作；收到请求不代表操作已执行成功。</summary>
    public enum BattleMenuAction
    {
        [InspectorName("战斗")] Fight = 0,
        [InspectorName("道具")] Item = 1,
        [InspectorName("精灵")] Pet = 2,
        [InspectorName("捕捉")] Capture = 3,
        [InspectorName("撤退")] Retreat = 4,
        [InspectorName("认输")] Surrender = 5
    }
}
