using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class ColliderEvent : UnityEvent<Collider> { }

public class CubetouchingTriggerEvents : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("�Ƿ���ݱ�ǩ���˴���������")]
    public bool filterByTag = true;

    [Tooltip("�����Է�ӵ�������ǩʱ�Ŵ����¼�")]
    public string requiredTag = "Cylinder";

    [Header("Events")]
    public ColliderEvent onEnter;           // ���봥�������ɴ� Collider ������
    public ColliderEvent onExit;            // �뿪���������ɴ� Collider ������
    public UnityEvent onAllContactsLost;    // ���нӴ����뿪���������㣩

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
        // ����ֹͣ�����屻����ʱ����Ϊ��ȫ���뿪��������һ�ΰ�ȫ�¼�
        if (_contacts.Count > 0)
        {
            _contacts.Clear();
            onAllContactsLost?.Invoke();
        }
    }
}
