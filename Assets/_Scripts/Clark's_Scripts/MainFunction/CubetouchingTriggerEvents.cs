using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class ColliderEvent : UnityEvent<Collider> { }

public class CubetouchingTriggerEvents : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("是否根据标签过滤触发器对象")]
    public bool filterByTag = true;

    [Tooltip("仅当对方拥有这个标签时才触发事件")]
    public string requiredTag = "Cylinder";

    [Header("Events")]
    public ColliderEvent onEnter;           // 进入触发器（可带 Collider 参数）
    public ColliderEvent onExit;            // 离开触发器（可带 Collider 参数）
    public UnityEvent onAllContactsLost;    // 所有接触都离开（计数归零）

    private readonly HashSet<Collider> _contacts = new HashSet<Collider>();

    private bool Pass(Collider other) => !filterByTag || other.CompareTag(requiredTag);

    private void OnTriggerEnter(Collider other)
    {
        if (!Pass(other)) return;
        _contacts.Add(other);
        onEnter?.Invoke(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!Pass(other)) return;
        _contacts.Remove(other);
        onExit?.Invoke(other);
        if (_contacts.Count == 0)
            onAllContactsLost?.Invoke();
    }

    private void OnDisable()
    {
        // 播放停止或物体被禁用时，认为“全部离开”，触发一次安全事件
        if (_contacts.Count > 0)
        {
            _contacts.Clear();
            onAllContactsLost?.Invoke();
        }
    }
}
