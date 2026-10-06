using ReSeer.Battle.UI;
using UnityEngine;

public class BattleActionController : MonoBehaviour
{
    public void OnMenuActionReceived(BattleMenuAction action)
    {
        Debug.Log($"收到菜单操作命令：{action}", this);
    }
}
