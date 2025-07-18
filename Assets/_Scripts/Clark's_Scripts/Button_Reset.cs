using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Button_Reset : MonoBehaviour
{
    [SerializeField] private float threshold = 0.1f;  //按钮 阈值界限（不用改）
    [SerializeField] private float deadZone = 0.025f;  //按钮 死区（不用改）

    private bool _isPressed;
    private Vector3 _startPos;
    private ConfigurableJoint _joint;

    public UnityEvent onPressed, onReleased;

    public ResetCylinderPosition resetScript; // 👉 拖入带 ResetCylinderPosition 脚本的物体

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

        // 限制按钮移动范围
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

        // 👉 按钮按下时调用 Cylinder 重置方法
        if (resetScript != null)
        {
            resetScript.ResetPosition();
        }

        //Debug.Log("Pressed");
    }

    private void Released()
    {
        _isPressed = false;
        onReleased.Invoke();
        //Debug.Log("Released");
    }
}
