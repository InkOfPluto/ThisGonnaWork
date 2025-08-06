using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ButtonSwitchKeyboard : MonoBehaviour
{
    [SerializeField] private float threshold = 0.1f;
    [SerializeField] private float deadZone = 0.025f;

    private bool _isPressed;
    private Vector3 _startPos;
    private ConfigurableJoint _joint;

    public UnityEvent onPressed, onReleased;

    // 👇 这部分是你要控制的 4 个组件
    [Header("Script Components To Toggle")]
    public MonoBehaviour Grasp_HandTracking;
    public MonoBehaviour UpDown_HandTracking;
    public MonoBehaviour UpDown_Keyboard;
    public MonoBehaviour Grasp_Keyboard;
    public MonoBehaviour Rotate_Keyboard;

    private bool toggled = false;

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
        if (!_isPressed && Input.GetKeyDown(KeyCode.M))
            Pressed();
        if (_isPressed && Input.GetKeyDown(KeyCode.M))
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
        ToggleComponents();
        onPressed.Invoke();
        //Debug.Log("Pressed");
    }

    private void Released()
    {
        _isPressed = false;
        onReleased.Invoke();
        //Debug.Log("Released");
    }

    // 👇 组件启用/禁用切换逻辑
    private void ToggleComponents()
    {
        toggled = !toggled;

        // 开关组件
        Grasp_HandTracking.enabled = !toggled;
        UpDown_HandTracking.enabled = !toggled;

        UpDown_Keyboard.enabled = toggled;
        Grasp_Keyboard.enabled = toggled;
        Rotate_Keyboard.enabled = toggled;

    }
}
