// 文件名：CenterOfMassController.cs
using System;
using UnityEngine;
using UnityEngine.Events;

[ExecuteAlways]
public class CenterOfMassController : MonoBehaviour
{
    [Header("操作说明 | Instructions")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
          "按 G 或 Xbox B 键：切换到下一个重心（COM）。当到达最后一个时，不再循环，隐藏对象与Counting文本+背景，显示ChangingMode文本+背景；模式切换后重置到0号重心。";

    [Header("目标物体 | Target Object（必须带 Rigidbody）")]
    public GameObject targetObject;

    [Header("圆柱体中心引用 | Cylinder Center Reference（可选）")]
    public Transform cylinderCenter;

    [Header("质心序号 | COM Index（0 ~ N-1）")]
    [Range(0, 15)]
    public int selectedCOMIndex = 0;

    // —— 仅保留距离+角度的定义 —— //
    [Header("距离-角度定义 | Distance-Angle Definitions（索引与数量决定最终 COM 列表）")]
    [SerializeField]
    private COMDistanceAngle[] comDistanceAngles = new COMDistanceAngle[]
    {
        new COMDistanceAngle(0.000f,   0.0f),   // 0 - Center
        new COMDistanceAngle(0.100f,   0.0f),   // 1 - Forward
        new COMDistanceAngle(0.100f,  45.0f),   // 2 - Front-Right
        new COMDistanceAngle(0.100f,  90.0f),   // 3 - Right
        new COMDistanceAngle(0.100f, 135.0f),   // 4 - Back-Right
        new COMDistanceAngle(0.100f, 180.0f),   // 5 - Back
        new COMDistanceAngle(0.100f, 225.0f),   // 6 - Back-Left
        new COMDistanceAngle(0.100f, 270.0f),   // 7 - Left
        new COMDistanceAngle(0.100f, 315.0f),   // 8 - Front-Left
        new COMDistanceAngle(0.050f,  30.0f),   // 9 - Near
        new COMDistanceAngle(0.050f,  60.0f),   // 10
        new COMDistanceAngle(0.050f, 120.0f),   // 11
        new COMDistanceAngle(0.050f, 150.0f),   // 12
        new COMDistanceAngle(0.050f, 210.0f),   // 13
        new COMDistanceAngle(0.050f, 240.0f),   // 14
        new COMDistanceAngle(0.050f, 300.0f),   // 15
    };

    [Header("同距离微扰参数 | Jitter For Same Distance")]
    public bool enableRandomJitter = true;
    [Tooltip("角度扰动（度），正负对称")]
    public float randomAngleJitterDeg = 5f;
    [Tooltip("半径扰动（米），正负对称")]
    public float randomRadiusJitter = 0.01f;
    [Tooltip("同一索引用于可复现随机的种子偏移（修改以生成新一组扰动）")]
    public int jitterSeedOffset = 12345;

    // —— 兼容其它脚本：保留相同的公开字段名与索引 —— //
    [Header("重心坐标列表（自动由距离-角度生成）| Auto-built From Distance-Angle")]
    public Vector3[] centerOfMassList = new Vector3[16];

    [Header("UI 引用 | UI References")]
    public GameObject countingText;
    public GameObject countingBackground;
    public GameObject changingModeText;
    public GameObject changingModeBackground;

    [Header("需要统一显隐的渲染器 | Renderers To Toggle（五个Cube + 圆柱体）")]
    public Renderer[] renderersToToggle;

    [Header("可选：ModeSwitch（用于模式切换时交互）")]
    public ModeSwitch modeSwitch; // 非必须

    [Header("切换重心时需要清零的抓握计数器 | Grasp Counters To Reset")]
    public Grasp_HandTracking[] graspCounters; // 在 Inspector 里把有计数的手脚本拖进来

    [Header("Goal 触发器 | Goal Triggers（切换 COM 时重置以重新出现）")]
    public GoalTriggerController[] goalTriggers; // 把挂了 GoalTriggerController 的 goal 物体拖进来

    // —— 新增：事件（可在代码/Inspector 订阅） —— //
    [Header("事件（Inspector 可配）| Events (UnityEvent)")]
    [Tooltip("当请求切到下一个 COM 并成功时触发（参数：新的 COM 索引）")]
    public UnityEvent<int> onNextCOMChanged;
    [Tooltip("当实际应用某个 COM（rb.centerOfMass 设置完成）时触发（参数：当前 COM 索引）")]
    public UnityEvent<int> onCOMApplied;
    [Tooltip("当走完最后一个 COM，进入 ChangingMode 提示时触发")]
    public UnityEvent onCycleCompleted;

    /// <summary>当请求切到下一个 COM 并成功时（参数：新的索引）。</summary>
    public event Action<int> NextCOMChanged;
    /// <summary>当实际应用某个 COM（rb.centerOfMass 设置完成）（参数：当前索引）。</summary>
    public event Action<int> COMApplied;
    /// <summary>当所有 COM 已完成时。</summary>
    public event Action CycleCompleted;

    // —— 运行期状态 —— //
    private Rigidbody rb;
    private int lastAppliedIndex = -1;
    private bool _isPressed = false;
    private bool _cycleCompleted = false;

    // —— 数据结构 —— //
    [Serializable]
    public struct COMDistanceAngle
    {
        [Tooltip("从圆柱体中心到重心的距离（米）")]
        public float distance;
        [Tooltip("XZ 平面角度（度），0 度为 Z+ 方向，顺时针为正")]
        public float angleInDegrees;

        public COMDistanceAngle(float distance, float angleInDegrees)
        {
            this.distance = distance;
            this.angleInDegrees = angleInDegrees;
        }
    }

    private void Start()
    {
        TryGetComponents();
        RebuildCenterOfMassList(); // 先生成列表，保证 Apply 生效
        SetExperimentVisualState(showCounting: true, showObjects: true);
        if (changingModeText) changingModeText.SetActive(false);
        if (changingModeBackground) changingModeBackground.SetActive(false);

        ApplyCenterOfMass(force: true);   // 真实应用 → 会重置计数并回调 ResetGoalsOnCOMChange()
        ResetGoalsOnCOMChange();          // 确保初始进场时 goal 可见
    }

    private void Update()
    {
        TryGetComponents();
        RebuildCenterOfMassList(); // 允许在编辑器/运行时修改参数后即时更新

        if (Application.isPlaying)
        {
            if (!_cycleCompleted)
            {
                // —— 将按键逻辑统一走公共入口 —— //
                if (!_isPressed && (Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.Space)))
                {
                    _isPressed = true;
                    RequestNextCOM_NoLoop(); // <—— 统一入口：外部脚本也可直接调用这个方法
                }
                if (_isPressed && (Input.GetKeyUp(KeyCode.JoystickButton1) || Input.GetKeyUp(KeyCode.Space)))
                {
                    _isPressed = false;
                }
            }
        }

        ApplyCenterOfMass(); // 在编辑器或运行时，只有当索引变化时才会真正应用
    }

    void TryGetComponents()
    {
        if (targetObject == null) return;
        if (rb == null) rb = targetObject.GetComponent<Rigidbody>();
    }

    /// <summary>
    /// 由距离+角度生成 centerOfMassList（保持索引与长度不变）
    /// Build centerOfMassList from distance-angle while keeping indices intact.
    /// </summary>
    void RebuildCenterOfMassList()
    {
        if (comDistanceAngles == null || comDistanceAngles.Length == 0)
        {
            centerOfMassList = Array.Empty<Vector3>();
            return;
        }

        if (centerOfMassList == null || centerOfMassList.Length != comDistanceAngles.Length)
            centerOfMassList = new Vector3[comDistanceAngles.Length];

        // 参考点：以 cylinderCenter（若提供）在目标物体的局部坐标为中心；否则 (0,0,0)
        Vector3 baseLocalCenter = Vector3.zero;
        if (cylinderCenter != null && targetObject != null)
        {
            baseLocalCenter = targetObject.transform.InverseTransformPoint(cylinderCenter.position);
        }

        for (int i = 0; i < comDistanceAngles.Length; i++)
        {
            var da = comDistanceAngles[i];
            float dist = Mathf.Max(0f, da.distance);
            float angleDeg = da.angleInDegrees;

            // 可复现随机：使用索引+距离哈希作为种子
            if (enableRandomJitter && (randomAngleJitterDeg > 0f || randomRadiusJitter > 0f))
            {
                int seed = i * 73856093 ^ jitterSeedOffset ^ Mathf.RoundToInt(dist * 10000f);
                System.Random prng = new System.Random(seed);

                // 映射到 [-1,1]
                float u1 = (float)prng.NextDouble() * 2f - 1f;
                float u2 = (float)prng.NextDouble() * 2f - 1f;

                float dAngle = u1 * randomAngleJitterDeg;   // 度
                float dRad = u2 * randomRadiusJitter;       // 米

                angleDeg += dAngle;
                dist = Mathf.Max(0f, dist + dRad);
            }

            float rad = angleDeg * Mathf.Deg2Rad;
            float x = dist * Mathf.Sin(rad);
            float z = dist * Mathf.Cos(rad);

            centerOfMassList[i] = new Vector3(
                baseLocalCenter.x + x,
                baseLocalCenter.y, // 允许 cylinderCenter 具有非零 y，高度与参考点一致
                baseLocalCenter.z + z
            );
        }
    }

    void ApplyCenterOfMass(bool force = false)
    {
        if (targetObject == null || rb == null || centerOfMassList == null || centerOfMassList.Length == 0) return;

        if ((force || selectedCOMIndex != lastAppliedIndex) &&
            selectedCOMIndex >= 0 && selectedCOMIndex < centerOfMassList.Length)
        {
            rb.centerOfMass = centerOfMassList[selectedCOMIndex];
            lastAppliedIndex = selectedCOMIndex;

            ResetAttemptsOnCOMChange();   // 清零抓握计数
            ResetGoalsOnCOMChange();      // 让 goal 重新出现（关键行）

            // —— 触发“已应用 COM”的事件 —— //
            onCOMApplied?.Invoke(selectedCOMIndex);
            COMApplied?.Invoke(selectedCOMIndex);
        }
    }

    // ======= ✅ 对外公开：请求切到下一个 COM（不循环） ======= //
    /// <summary>
    /// 请求切换到下一个 COM（不循环）。可被其它脚本直接调用。
    /// 返回值：true 表示确实切到了下一个；false 表示已经到末尾（会触发完成事件）。
    /// </summary>
    public bool RequestNextCOM_NoLoop()
    {
        if (_cycleCompleted) return false;
        if (centerOfMassList == null || centerOfMassList.Length == 0) return false;

        if (selectedCOMIndex >= centerOfMassList.Length - 1)
        {
            OnCompleteAllCOMs();
            return false;
        }

        selectedCOMIndex = Mathf.Clamp(selectedCOMIndex + 1, 0, centerOfMassList.Length - 1);
        Debug.Log($"🎮 切换到 COM_{selectedCOMIndex}");

        // —— 触发“成功切到下一个 COM”的事件（此时索引已变化，稍后 ApplyCenterOfMass 会真正应用） —— //
        onNextCOMChanged?.Invoke(selectedCOMIndex);
        NextCOMChanged?.Invoke(selectedCOMIndex);

        // 立即应用，确保外部订阅者拿到已写入 rb.centerOfMass 的时机
        ApplyCenterOfMass(force: true);
        return true;
    }

    // —— 兼容旧命名（如需保留可见性） —— //
    [Obsolete("请改用 RequestNextCOM_NoLoop()")]
    public void CycleToNextCOM_NoLoop()
    {
        RequestNextCOM_NoLoop();
    }

    void OnCompleteAllCOMs()
    {
        _cycleCompleted = true;
        SetExperimentVisualState(showCounting: false, showObjects: false);
        if (changingModeText) changingModeText.SetActive(true);
        if (changingModeBackground) changingModeBackground.SetActive(true);
        Debug.Log("✅ 已完成所有质心；停止循环，进入 ChangingMode 提示。");

        onCycleCompleted?.Invoke();
        CycleCompleted?.Invoke();
    }

    public void ResetForNewMode()
    {
        _cycleCompleted = false;
        selectedCOMIndex = 0;
        lastAppliedIndex = -1;
        SetExperimentVisualState(showCounting: true, showObjects: true);
        if (changingModeText) changingModeText.SetActive(false);
        if (changingModeBackground) changingModeBackground.SetActive(false);

        ApplyCenterOfMass(force: true);   // 会触发 ResetAttemptsOnCOMChange() + ResetGoalsOnCOMChange()
        Debug.Log("🔁 模式切换：已重置到 COM_0，恢复显示与 Counting。");
    }

    void SetExperimentVisualState(bool showCounting, bool showObjects)
    {
        if (countingText) countingText.SetActive(showCounting);
        if (countingBackground) countingBackground.SetActive(showCounting);

        if (renderersToToggle != null)
        {
            foreach (var r in renderersToToggle)
            {
                if (r) r.enabled = showObjects;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            // 保持索引有效
            if (centerOfMassList != null && centerOfMassList.Length > 0)
            {
                selectedCOMIndex = Mathf.Clamp(selectedCOMIndex, 0, centerOfMassList.Length - 1);
            }
            // 实时重建，方便在 Inspector 中调参
            RebuildCenterOfMassList();
        }
    }
#endif

    // —— 当 COM 变更被实际应用时，清零所有绑定的计数器 —— //
    private void ResetAttemptsOnCOMChange()
    {
        if (graspCounters == null) return;
        foreach (var g in graspCounters)
        {
            if (g != null) g.ResetAttempt();
        }
    }

    // —— 当 COM 变更被实际应用时，让所有 goal 重新出现 —— //
    private void ResetGoalsOnCOMChange()
    {
        if (goalTriggers == null) return;
        foreach (var goal in goalTriggers)
        {
            if (goal != null) goal.ResetGoalOnNextCoM();
        }
    }
}
