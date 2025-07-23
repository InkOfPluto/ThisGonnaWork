using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ButtonForRotateFingers : MonoBehaviour
{
    [SerializeField] private float threshold = 0.1f;
    [SerializeField] private float deadZone = 0.025f;

    private bool _isPressed;
    private Vector3 _startPos;
    private ConfigurableJoint _joint;

    public UnityEvent onPressed, onReleased;

    [SerializeField] private MonoBehaviour Grasp_HandTracking;
    [SerializeField] private MonoBehaviour UpDown_HandTracking;
    [SerializeField] private MonoBehaviour Follow_HandTracking;

    [SerializeField] private Transform cubeCenter;
    [SerializeField] private List<Transform> fingerCubes;

    private Dictionary<Transform, float> lockedYPositions = new Dictionary<Transform, float>();

    private enum ControlState { Default, Rotate, Grasp }
    private ControlState currentState = ControlState.Default;

    void Start()
    {
        _startPos = transform.localPosition;
        _joint = GetComponent<ConfigurableJoint>();

        currentState = ControlState.Default;
        Debug.Log("[状态] 初始为默认模式");
    }

    void Update()
    {
        // 实时记录 fingerCubes 的 Y 值（仅在默认或抓取模式）
        if (currentState != ControlState.Rotate)
        {
            foreach (var cube in fingerCubes)
            {
                if (cube == null) continue;
                lockedYPositions[cube] = cube.position.y;
            }
        }

        // 按钮触发逻辑
        if (!_isPressed && GetValue() + threshold >= 1)
            Pressed();
        if (_isPressed && GetValue() - threshold <= 0)
            Released();
        if (!_isPressed && Input.GetKeyDown(KeyCode.JoystickButton0))
            Pressed();
        if (_isPressed && Input.GetKeyUp(KeyCode.JoystickButton0))
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

        if (currentState == ControlState.Default)
        {
            SwitchToRotateMode();
            currentState = ControlState.Rotate;
            Debug.Log("[状态] 默认模式 → 旋转模式");
        }
        else if (currentState == ControlState.Rotate)
        {
            SwitchToGraspMode();
            currentState = ControlState.Grasp;
            Debug.Log("[状态] 旋转模式 → 抓取模式");
        }
        else if (currentState == ControlState.Grasp)
        {
            SwitchToRotateMode();
            currentState = ControlState.Rotate;
            Debug.Log("[状态] 抓取模式 → 旋转模式");
        }
    }

    private void Released()
    {
        _isPressed = false;
        onReleased.Invoke();
    }

    private void SwitchToRotateMode()
    {
        if (Grasp_HandTracking != null) Grasp_HandTracking.enabled = false;
        if (UpDown_HandTracking != null) UpDown_HandTracking.enabled = false;
        if (Follow_HandTracking != null)
        {
            Follow_HandTracking.enabled = true;

            var follow = Follow_HandTracking as Follow_HandTracking;
            if (follow != null)
            {
                follow.lockedYPositions = new Dictionary<Transform, float>(lockedYPositions);
            }
        }

        foreach (var cube in fingerCubes)
        {
            if (cube == null) continue;

            var pincher = cube.GetComponent<PincherFingerController>();
            if (pincher != null)
            {
                pincher.enabled = false;
            }

            Vector3 newPos = cube.position;
            if (lockedYPositions.ContainsKey(cube))
                newPos.y = lockedYPositions[cube];
            cube.position = newPos;
        }
    }

    private void SwitchToGraspMode()
    {
        if (Grasp_HandTracking != null) Grasp_HandTracking.enabled = true;
        if (UpDown_HandTracking != null) UpDown_HandTracking.enabled = true;
        if (Follow_HandTracking != null) Follow_HandTracking.enabled = false;

        foreach (var cube in fingerCubes)
        {
            var pincher = cube.GetComponent<PincherFingerController>();
            if (pincher != null)
            {
                pincher.enabled = true;
            }
        }
    }
}
