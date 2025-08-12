using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ButtonForRotateFingers : MonoBehaviour
{
    [Header("操作说明 | Instructions")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
        "按 手柄A(JoystickButton0) 或 E：按下/释放按钮以切换状态\n" +
        "按钮循环：默认(Default) → 旋转(Rotate) → 抓取(Grasp) → 旋转(Rotate)...\n" +
        "Inspector：threshold/deadZone 调整按钮触发灵敏度与死区\n" +
        "Inspector：绑定 Grasp_HandTracking、UpDown_HandTracking、Follow_HandTracking\n" +
        "Inspector：绑定 cubeCenter、fingerCubes、cylinder（用于锁定与修正可视化高度）\n" +
        "Inspector：绑定 visualDisplay（用于切换可视化显示）\n" +
        "进入 Rotate：启用 Follow_HandTracking，并锁定各手指当前 Y；禁用 PincherFingerController；关闭 VisualDisplay\n" +
        "进入 Grasp：启用 Grasp_HandTracking 与 UpDown_HandTracking；禁用 Follow；启用 PincherFingerController；开启 VisualDisplay\n" +
        "按钮位移：本体 local Y 被限制在 [-0.01, 0]\n";

    [Header("Button Settings | 按钮参数")]
    [SerializeField] private float threshold = 0.1f;
    [SerializeField] private float deadZone = 0.025f;
    [SerializeField] private bool logExitOnRelease = false;

    private bool _isPressed;
    private Vector3 _startPos;
    private ConfigurableJoint _joint;

    public UnityEvent onPressed, onReleased;

    [Header("Control Scripts | 控制脚本")]
    [SerializeField] private MonoBehaviour Grasp_HandTracking;
    [SerializeField] private MonoBehaviour UpDown_HandTracking;
    [SerializeField] private MonoBehaviour Follow_HandTracking;
    private ExperimentSaveData_JSON saveJSON;

    [Header("References | 引用组件")]
    [SerializeField] private Transform cubeCenter;
    [SerializeField] private List<Transform> fingerCubes;
    [SerializeField] private Transform cylinder;

    [Header("Visuals | 可视化组件")]
    [SerializeField] private MonoBehaviour visualDisplay;

    [Header("UI 提示 | 非旋转模式显示绿点")]
    [SerializeField] private GameObject uiIndicator; // 拖 Alarm/Canvas/rotate 进来

    private readonly Dictionary<Transform, float> lockedYPositions = new Dictionary<Transform, float>();

    private enum ControlState { Default, Rotate, Grasp }
    private ControlState currentState = ControlState.Default;

    void Start()
    {
        saveJSON = FindObjectOfType<ExperimentSaveData_JSON>();
        _startPos = transform.localPosition;
        _joint = GetComponent<ConfigurableJoint>();

        currentState = ControlState.Default;
        UpdateUIIndicator(); // 初始化同步
        Debug.Log("[状态] 初始为默认模式");
    }

    void Update()
    {
        if (currentState != ControlState.Rotate)
        {
            foreach (var cube in fingerCubes)
            {
                if (cube == null) continue;
                lockedYPositions[cube] = cube.position.y;
            }
        }

        if (!_isPressed && GetValue() + threshold >= 1f) Pressed();
        if (_isPressed && GetValue() - threshold <= 0f) Released();

        if ((!_isPressed && Input.GetKeyDown(KeyCode.JoystickButton0)) || Input.GetKeyDown(KeyCode.E))
            Pressed();
        if ((_isPressed && Input.GetKeyUp(KeyCode.JoystickButton0)) || Input.GetKeyUp(KeyCode.E))
            Released();

        Vector3 pos = transform.localPosition;
        pos.y = Mathf.Clamp(pos.y, -0.01f, 0f);
        transform.localPosition = pos;
    }

    private float GetValue()
    {
        var value = Vector3.Distance(_startPos, transform.localPosition) / _joint.linearLimit.limit;
        if (Mathf.Abs(value) < deadZone) value = 0f;
        return Mathf.Clamp(value, -1f, 1f);
    }

    private void Pressed()
    {
        _isPressed = true;
        onPressed?.Invoke();

        if (currentState == ControlState.Default)
        {
            SwitchToRotateMode();
            currentState = ControlState.Rotate;

            if (saveJSON != null)
            {
                saveJSON.OnRotateButtonPressed();
                saveJSON.EnterRotateMode("button");
            }

            UpdateUIIndicator();
            Debug.Log("[状态] 默认模式 → 旋转模式");
        }
        else if (currentState == ControlState.Rotate)
        {
            if (saveJSON != null)
            {
                saveJSON.ExitRotateMode("button_to_grasp");
            }

            SwitchToGraspMode();
            currentState = ControlState.Grasp;

            UpdateUIIndicator();
            Debug.Log("[状态] 旋转模式 → 抓取模式");
        }
        else if (currentState == ControlState.Grasp)
        {
            SwitchToRotateMode();
            currentState = ControlState.Rotate;

            if (saveJSON != null)
            {
                saveJSON.OnRotateButtonPressed();
                saveJSON.EnterRotateMode("button");
            }

            UpdateUIIndicator();
            Debug.Log("[状态] 抓取模式 → 旋转模式");
        }
    }

    private void Released()
    {
        _isPressed = false;
        onReleased?.Invoke();

        if (logExitOnRelease && currentState == ControlState.Rotate && saveJSON != null)
        {
            saveJSON.ExitRotateMode("button_release");
        }
    }

    private void SwitchToRotateMode()
    {
        if (Grasp_HandTracking != null) Grasp_HandTracking.enabled = false;
        if (UpDown_HandTracking != null) UpDown_HandTracking.enabled = false;

        if (Follow_HandTracking != null)
        {
            Follow_HandTracking.enabled = true;
            var followScript = Follow_HandTracking as Follow_HandTracking;
            if (followScript != null)
            {
                followScript.lockedYPositions = new Dictionary<Transform, float>(lockedYPositions);
            }
        }

        foreach (var cube in fingerCubes)
        {
            if (cube == null) continue;

            var pincher = cube.GetComponent<PincherFingerController>();
            if (pincher != null) pincher.enabled = false;

            Vector3 newPos = cube.position;
            if (lockedYPositions.TryGetValue(cube, out float y))
                newPos.y = y;
            cube.position = newPos;
        }

        if (visualDisplay != null) visualDisplay.enabled = false;
        if (uiIndicator != null) uiIndicator.SetActive(false); // 旋转模式隐藏绿点
    }

    private void SwitchToGraspMode()
    {
        if (Grasp_HandTracking != null) Grasp_HandTracking.enabled = true;
        if (UpDown_HandTracking != null) UpDown_HandTracking.enabled = true;
        if (Follow_HandTracking != null) Follow_HandTracking.enabled = false;

        foreach (var cube in fingerCubes)
        {
            if (cube == null) continue;

            var pincher = cube.GetComponent<PincherFingerController>();
            if (pincher != null) pincher.enabled = true;
        }

        if (visualDisplay != null) visualDisplay.enabled = true;
        if (uiIndicator != null) uiIndicator.SetActive(true); // 抓取模式显示绿点
    }

    public void EnterRotateModeFromCOMChange()
    {
        var oldState = currentState;
        SwitchToRotateMode();
        currentState = ControlState.Rotate;

        if (saveJSON != null) saveJSON.EnterRotateMode("com_change");

        UpdateUIIndicator();
        Debug.Log($"[状态] 由 COM 切换触发 → 进入旋转模式（{oldState} → Rotate）");
    }

    private void UpdateUIIndicator()
    {
        if (uiIndicator != null)
            uiIndicator.SetActive(currentState != ControlState.Rotate);
    }
}
