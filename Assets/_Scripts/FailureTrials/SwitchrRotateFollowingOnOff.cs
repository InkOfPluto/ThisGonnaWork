using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(ConfigurableJoint))]
public class SwitchrRotateFollowingOnOff : MonoBehaviour
{
    [Header("阈值设置")]
    [SerializeField] private float threshold = 0.1f;
    [SerializeField] private float deadZone = 0.025f;

    [Header("控制器组件")]
    public MonoBehaviour graspController; // Grasp Controller 脚本
    public MonoBehaviour wristController; // Wrist Controller 脚本
    public MonoBehaviour buttonRotate; // Button Rotate 脚本

    [Header("五个手指 Cube")]
    public List<Transform> fingerCubes = new List<Transform>();

    [Header("Y轴固定设置")]
    [SerializeField] private float fixedYPosition = 0.9f; // 固定的Y轴位置

    private List<Vector3> savedFinalPositions = new List<Vector3>();
    private List<Quaternion> savedFinalRotations = new List<Quaternion>();
    private List<float> originalYPositions = new List<float>(); // 保存原始Y位置
    private List<Rigidbody> cubeRigidbodies = new List<Rigidbody>();

    [Header("状态事件")]
    public UnityEvent onRotationModeEnabled; // 旋转模式启用事件
    public UnityEvent onNormalModeEnabled;   // 正常模式启用事件

    private bool _isPressed = false;
    private bool isRotationMode = false; // false = 正常模式, true = 旋转模式
    private Vector3 _startPos;
    private ConfigurableJoint _joint;

    void Start()
    {
        _joint = GetComponent<ConfigurableJoint>();
        _startPos = transform.localPosition;

        SetNormalMode();


        Debug.Log("🎮 初始状态：正常模式 - 控制器启用");
    }



    void Update()
    {
        float val = GetPressValue();

        // 检测按钮按下
        if (!_isPressed && val + threshold >= 1)
        {
            _isPressed = true;
            ToggleMode();
        }

        // 检测按钮释放
        if (_isPressed && val - threshold <= 0)
        {
            _isPressed = false;
        }

        // 限制按钮移动范围
        Vector3 pos = transform.localPosition;
        pos.y = Mathf.Clamp(pos.y, -0.01f, 0.01f);
        transform.localPosition = pos;

        // 在旋转模式下限制Y轴移动
        if (isRotationMode)
        {
            ConstrainYMovement();
        }
    }

    private void ConstrainYMovement()
    {
        for (int i = 0; i < fingerCubes.Count; i++)
        {
            if (fingerCubes[i] == null || i >= cubeRigidbodies.Count) continue;

            Transform cube = fingerCubes[i];
            Rigidbody rb = cubeRigidbodies[i];

            // 强制Y位置
            Vector3 pos = cube.position;
            if (Mathf.Abs(pos.y - fixedYPosition) > 0.01f)
            {
                pos.y = fixedYPosition;
                cube.position = pos;
            }

            // 清除Y轴速度
            Vector3 vel = rb.velocity;
            vel.y = 0;
            rb.velocity = vel;
        }
    }

    private float GetPressValue()
    {
        float value = Vector3.Distance(_startPos, transform.localPosition) / _joint.linearLimit.limit;
        if (Mathf.Abs(value) < deadZone)
            value = 0;
        return Mathf.Clamp(value, -1f, 1f);
    }

    private void ToggleMode()
    {
        isRotationMode = !isRotationMode;

        if (isRotationMode)
        {
            SetRotationMode();
        }
        else
        {
            SetNormalMode();
        }
    }

    private void SetRotationMode()
    {
        // 禁用控制器
        if (graspController != null)
            graspController.enabled = false;
        if (wristController != null)
            wristController.enabled = false;

        // 启用旋转脚本
        if (buttonRotate != null)
            buttonRotate.enabled = true;

        // 设置立方体约束 - 冻结Y轴位置
        for (int i = 0; i < fingerCubes.Count; i++)
        {
            if (fingerCubes[i] == null || i >= cubeRigidbodies.Count) continue;

            Transform cube = fingerCubes[i];
            Rigidbody rb = cubeRigidbodies[i];

            // 设置固定Y位置
            Vector3 pos = cube.position;
            pos.y = fixedYPosition;
            cube.position = pos;

            // 冻结Y轴位置，但允许XZ移动和旋转
            rb.constraints = RigidbodyConstraints.FreezePositionY;
        }

        Debug.Log("🔄 旋转模式启用 - Y轴固定，可水平移动和旋转");
        onRotationModeEnabled?.Invoke();
    }

    private void SetNormalMode()
    {
        // 启用控制器
        if (graspController != null)
            graspController.enabled = true;
        if (wristController != null)
            wristController.enabled = true;

        // 禁用旋转脚本
        if (buttonRotate != null)
            buttonRotate.enabled = false;

        // 移除立方体约束
        for (int i = 0; i < cubeRigidbodies.Count; i++)
        {
            if (cubeRigidbodies[i] != null)
            {
                // 移除所有约束，允许完全自由移动
                cubeRigidbodies[i].constraints = RigidbodyConstraints.None;
            }
        }

        // 如果从旋转模式切换回来，保存当前位置
        if (isRotationMode)
        {
            SaveFingerCubeStates();
        }

        Debug.Log("✅ 正常模式启用 - 恢复完全手部跟踪");
        onNormalModeEnabled?.Invoke();
    }

    private void SaveFingerCubeStates()
    {
        savedFinalPositions.Clear();
        savedFinalRotations.Clear();

        foreach (Transform cube in fingerCubes)
        {
            if (cube != null)
            {
                savedFinalPositions.Add(cube.position);
                savedFinalRotations.Add(cube.rotation);
                Debug.Log($"📌 已保存 {cube.name} 旋转后的位置: {cube.position:F3}，旋转: {cube.rotation.eulerAngles:F1}");
            }
        }
    }

    // 手动切换模式
    [ContextMenu("切换模式")]
    public void ManualToggleMode()
    {
        ToggleMode();
    }

    // 强制设置为正常模式
    [ContextMenu("强制正常模式")]
    public void ForceNormalMode()
    {
        isRotationMode = true; // 设为true，这样ToggleMode会切换到false
        ToggleMode();
    }

    // 强制设置为旋转模式
    [ContextMenu("强制旋转模式")]
    public void ForceRotationMode()
    {
        isRotationMode = false; // 设为false，这样ToggleMode会切换到true
        ToggleMode();
    }

    // 获取当前模式
    public bool IsRotationMode()
    {
        return isRotationMode;
    }

    // 获取保存的位置数据
    public List<Vector3> GetSavedCubePositions()
    {
        return new List<Vector3>(savedFinalPositions);
    }

    // 获取保存的旋转数据
    public List<Quaternion> GetSavedCubeRotations()
    {
        return new List<Quaternion>(savedFinalRotations);
    }

    // 设置固定Y位置
    public void SetFixedYPosition(float yPos)
    {
        fixedYPosition = yPos;
        Debug.Log($"🔧 固定Y位置设置为: {yPos}");
    }

    // 检查组件是否已正确设置
    void OnValidate()
    {
        if (graspController == null)
            Debug.LogWarning("⚠️ Grasp Controller 未设置！");
        if (wristController == null)
            Debug.LogWarning("⚠️ Wrist Controller 未设置！");
        if (buttonRotate == null)
            Debug.LogWarning("⚠️ Button Rotate 脚本未设置！");
    }
}