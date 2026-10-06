using ReSeer.Battle.UI;
using UnityEngine;

/// <summary>挂在管理器对象上，把菜单操作请求转交给 Inspector 配置的响应方法。</summary>
[AddComponentMenu("ReSeer/Events/Battle Menu Action Event Listener")]
public sealed class BattleMenuActionEventListener : BaseEventListener<BattleMenuAction>
{
}
