using ReSeer.Battle.UI;
using UnityEngine;

/// <summary>菜单操作请求通道，由按钮发送，管理器监听。</summary>
[CreateAssetMenu(fileName = "BattleMenuActionEvent", menuName = "Events/BattleMenuActionEventSO")]
public sealed class BattleMenuActionEventSO : BaseEventSO<BattleMenuAction>
{
}
