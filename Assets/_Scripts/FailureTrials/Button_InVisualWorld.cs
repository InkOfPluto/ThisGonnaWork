using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Button_InVisualWorld : MonoBehaviour
{
    [SerializeField] private float threshold = 0.1f;  //��ť ��ֵ���ޣ����øģ�
    [SerializeField] private float deadZone = 0.025f;  //��ť ���������øģ�

    private bool _isPressed;
    private Vector3 _startPos;
    private ConfigurableJoint _joint;

    public UnityEvent onPressed, onReleased;

    void Start()
    {
        _startPos = transform.localPosition;
        _joint = GetComponent<ConfigurableJoint>();
    }

    void Update()
    {
        if (!_isPressed && GetValue() + threshold >= 1)
            Pressed();

        if (_isPressed && GetValue() - threshold <= 0)
            Released();

        // ���ư�ť�ƶ���Χ
        Vector3 pos = transform.localPosition;
        pos.y = Mathf.Clamp(pos.y, -0.01f, 0);
        transform.localPosition = pos;
    }

    private float GetValue()
    {
        var value = Vector3.Distance(_startPos, transform.localPosition) / _joint.linearLimit.limit;
        if (Mathf.Abs(value) < deadZone)
            value = 0;
        return Mathf.Clamp(value, -1f, 1f);
    }

    private void Pressed()
    {
        _isPressed = true;

        onPressed.Invoke();
        Debug.Log("Pressed");
    }

    private void Released()
    {
        _isPressed = false;
        onReleased.Invoke();
        Debug.Log("Released");
    }
}



