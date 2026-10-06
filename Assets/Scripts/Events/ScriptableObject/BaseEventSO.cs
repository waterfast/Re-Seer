using System;
using UnityEngine;
using UnityEngine.Events;

public class BaseEventSO<T> : ScriptableObject
{

    [TextArea]

    public string description;

    public event UnityAction<T> OnEventRaised;

    public string lastSender;

    // 返回订阅列表的快照，供 Inspector 查看；外部不能覆盖或触发事件。
    public Delegate[] GetSubscribers() => OnEventRaised?.GetInvocationList() ?? Array.Empty<Delegate>();

    public virtual void RaiseEvent(T value, object sender)
    {
        // 监听器处理本次事件时，应能读取本次发送者。
        lastSender = sender?.ToString() ?? string.Empty;
        OnEventRaised?.Invoke(value);
    }
}
