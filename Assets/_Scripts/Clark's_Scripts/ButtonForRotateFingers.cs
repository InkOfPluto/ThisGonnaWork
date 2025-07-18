using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ButtonForRotateFingers : MonoBehaviour
{
    [SerializeField] private float threshold = 0.1f;      // 按钮触发阈值（不改）
    [SerializeField] private float deadZone = 0.025f;     // 死区（不改）

    private bool _isPressed;
    private Vector3 _startPos;
    private ConfigurableJoint _joint;

    public UnityEvent onPressed, onReleased;

    private bool isInRotateMode = false;

    [SerializeField] private MonoBehaviour Grasp_HandTracking;
    [SerializeField] private MonoBehaviour UpDown_HandTracking;
    [SerializeField] private MonoBehaviour Follow_HandTracking;

    [SerializeField] private List<Transform> fingerCubes;

    void Start()
    {
        _startPos = transform.localPosition;
        _joint = GetComponent<ConfigurableJoint>();

        isInRotateMode = false;
        SwitchToNormalMode();  // 默认进入正常模式
    }

    void Update()
    {
        if (!_isPressed && GetValue() + threshold >= 1)
            Pressed();

        if (_isPressed && GetValue() - threshold <= 0)
            Released();

        // 限制按钮 Y 向移动范围
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
        //Debug.Log("Pressed");

        if (!isInRotateMode)
        {
            SwitchToRotateMode();
        }
        else
        {
            SwitchToNormalMode(); 
        }

        isInRotateMode = !isInRotateMode;
    }

    private void Released()
    {
        _isPressed = false;
        onReleased.Invoke();
        //Debug.Log("Released");
    }

    private void SwitchToRotateMode()
    {
        if (Grasp_HandTracking != null) Grasp_HandTracking.enabled = false;
        if (UpDown_HandTracking != null) UpDown_HandTracking.enabled = false;
        if (Follow_HandTracking != null) Follow_HandTracking.enabled = true;

        // 禁用所有 fingerCube 上的 PincherFingerController
        foreach (var cube in fingerCubes)
        {
            var pincher = cube.GetComponent<PincherFingerController>();
            if (pincher != null)
            {
                pincher.enabled = false;
            }
        }

        //Debug.Log("Switched to Rotate Mode");
    }

    private void SwitchToNormalMode()
    {
        StartCoroutine(SafeTransitionToNormalMode()); // 使用协程安全转换
    }

    private IEnumerator SafeTransitionToNormalMode()
    {
        yield return new WaitForEndOfFrame();  // 等待 Follow_HandTracking 的 Update 执行完

        // ✅ Step 1: 记录当前 Cube 位置为 openPosition
        foreach (var cube in fingerCubes)
        {
            var pincher = cube.GetComponent<PincherFingerController>();
            if (pincher != null)
            {
                pincher.enabled = true; // 确保重新启用控制器
                pincher.InitializeOpenPositionFromCurrent();
            }
        }

        // ✅ Step 2: 启用抓握控制，关闭 Follow
        if (Grasp_HandTracking != null) Grasp_HandTracking.enabled = true;
        if (UpDown_HandTracking != null) UpDown_HandTracking.enabled = true;
        if (Follow_HandTracking != null) Follow_HandTracking.enabled = false;

        //Debug.Log("Switched to Normal Mode (safe)");
    }
}
