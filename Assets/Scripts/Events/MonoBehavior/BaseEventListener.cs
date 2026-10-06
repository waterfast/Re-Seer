

using UnityEngine;
using UnityEngine.Events;

public class BaseEventListener<T> : MonoBehaviour
{
    public BaseEventSO<T> eventSO;
    public UnityEvent<T> response;

    private void OnEnable()
    {
       if (eventSO != null )
       {
           eventSO.OnEventRaised += OnEventRaised;
       }
    }

    private void OnDisable()
    {
       if (eventSO != null)
       {
           eventSO.OnEventRaised -= OnEventRaised;
       }
    }

    public void OnEventRaised(T value)
    {
        response?.Invoke(value);
    }
}