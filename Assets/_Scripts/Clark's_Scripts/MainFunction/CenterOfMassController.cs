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
          "按 Space 或 Xbox B 键：切换到下一个重心（COM）。当到达最后一个时，不再循环，隐藏对象与Counting文本+背景，显示ChangingMode文本+背景；模式切换后重置到0号重心。";

    [Header("目标物体 | Target Object（必须带 Rigidbody）")]
    public GameObject targetObject;

    [Header("圆柱体中心引用 | Cylinder Center Reference（可选）")]
    public Transform cylinderCenter;

    [Header("质心序号 | COM Index（0 ~ N-1）")]
    [Range(0, 15)]
    public int selectedCOMIndex = 0;

    [Header("半径参数 | Distance Controls")]
    public float distanceGroup1 = 1.5f;   // 1-5
    public float distanceGroup2 = 1.25f;  // 6-10
    public float distanceGroup3 = 1.0f;   // 11-15

    // —— 仅保留角度定义，距离在 RebuildCenterOfMassList 里动态替换 —— //
    [Header("角度定义 | Angle Definitions")]
    private COMDistanceAngle[] comDistanceAngles = new COMDistanceAngle[]
    {
        // 编号 0 —— 中心（保持不动）
        new COMDistanceAngle(0.000f, 0.0f),    

        // 编号 1-5：使用 distanceGroup1
        new COMDistanceAngle(0f, 180.0f),  // 1
        new COMDistanceAngle(0f, 12.0f),   // 2
        new COMDistanceAngle(0f, 222.0f),  // 3
        new COMDistanceAngle(0f, 308.0f),  // 4
        new COMDistanceAngle(0f, 351.0f),  // 5

        // 编号 6-10：使用 distanceGroup2
        new COMDistanceAngle(0f, 8.0f),    // 6
        new COMDistanceAngle(0f, 162.0f),  // 7
        new COMDistanceAngle(0f, 204.0f),  // 8
        new COMDistanceAngle(0f, 276.0f),  // 9
        new COMDistanceAngle(0f, 333.0f),  // 10

        // 编号 11-15：使用 distanceGroup3
        new COMDistanceAngle(0f, 15.0f),   // 11
        new COMDistanceAngle(0f, 150.0f),  // 12
        new COMDistanceAngle(0f, 210.0f),  // 13
        new COMDistanceAngle(0f, 297.0f),  // 14
        new COMDistanceAngle(0f, 354.0f),  // 15
    };

    [Header("同距离微扰参数 | Jitter For Same Distance")]
    public bool enableRandomJitter = true;
    [Tooltip("角度扰动（度），正负对称")]
    public float randomAngleJitterDeg = 5f;
    [Tooltip("半径扰动（米），正负对称")]
    public float randomRadiusJitter = 0.01f;
    [Tooltip("同一索引用于可复现随机的种子偏移（修改以生成新一组扰动）")]
    public int jitterSeedOffset = 12345;

    [Header("切换方式 | Switch Mode")]
    public bool enableRandomSelection = false;   // 开启后，每次随机选择一个 COM（排除编号0）

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
    public Grasp_HandTracking[] graspCounters;

    [Header("Goal 触发器 | Goal Triggers（切换 COM 时重置以重新出现）")]
    public GoalTriggerController[] goalTriggers;

    [Header("事件（Inspector 可配）| Events (UnityEvent)")]
    public UnityEvent<int> onNextCOMChanged;
    public UnityEvent<int> onCOMApplied;
    public UnityEvent onCycleCompleted;

    public event Action<int> NextCOMChanged;
    public event Action<int> COMApplied;
    public event Action CycleCompleted;

    // —— 运行期状态 —— //
    private Rigidbody rb;
    private int lastAppliedIndex = -1;
    private bool _isPressed = false;
    private bool _cycleCompleted = false;

    [Header("累计质心进度 | COM Progress Counter")]
    [ReadOnly] public int comProgressCounter = 0;

    [Serializable]
    public struct COMDistanceAngle
    {
        public float distance;
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
        RebuildCenterOfMassList();
        SetExperimentVisualState(showCounting: true, showObjects: true);
        if (changingModeText) changingModeText.SetActive(false);
        if (changingModeBackground) changingModeBackground.SetActive(false);

        ApplyCenterOfMass(force: true);
        ResetGoalsOnCOMChange();
    }

    private void Update()
    {
        TryGetComponents();
        RebuildCenterOfMassList();

        if (Application.isPlaying)
        {
            if (!_cycleCompleted)
            {
                if (!_isPressed && (Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.Space)))
                {
                    _isPressed = true;
                    RequestNextCOM_NoLoop();
                }
                if (_isPressed && (Input.GetKeyUp(KeyCode.JoystickButton1) || Input.GetKeyUp(KeyCode.Space)))
                {
                    _isPressed = false;
                }
            }
        }

        ApplyCenterOfMass();
    }

    void TryGetComponents()
    {
        if (targetObject == null) return;
        if (rb == null) rb = targetObject.GetComponent<Rigidbody>();
    }

    void RebuildCenterOfMassList()
    {
        if (comDistanceAngles == null || comDistanceAngles.Length == 0)
        {
            centerOfMassList = Array.Empty<Vector3>();
            return;
        }

        if (centerOfMassList == null || centerOfMassList.Length != comDistanceAngles.Length)
            centerOfMassList = new Vector3[comDistanceAngles.Length];

        Vector3 baseLocalCenter = Vector3.zero;
        if (cylinderCenter != null && targetObject != null)
        {
            baseLocalCenter = targetObject.transform.InverseTransformPoint(cylinderCenter.position);
        }

        for (int i = 0; i < comDistanceAngles.Length; i++)
        {
            var da = comDistanceAngles[i];

            // ✅ 根据分组动态替换距离
            if (i >= 1 && i <= 5) da.distance = distanceGroup1;
            else if (i >= 6 && i <= 10) da.distance = distanceGroup2;
            else if (i >= 11 && i <= 15) da.distance = distanceGroup3;

            float dist = Mathf.Max(0f, da.distance);
            float angleDeg = da.angleInDegrees;

            if (enableRandomJitter && (randomAngleJitterDeg > 0f || randomRadiusJitter > 0f))
            {
                int seed = i * 73856093 ^ jitterSeedOffset ^ Mathf.RoundToInt(dist * 10000f);
                System.Random prng = new System.Random(seed);

                float u1 = (float)prng.NextDouble() * 2f - 1f;
                float u2 = (float)prng.NextDouble() * 2f - 1f;

                float dAngle = u1 * randomAngleJitterDeg;
                float dRad = u2 * randomRadiusJitter;

                angleDeg += dAngle;
                dist = Mathf.Max(0f, dist + dRad);
            }

            float rad = angleDeg * Mathf.Deg2Rad;
            float x = dist * Mathf.Cos(rad); // ✅ X+ 为 0°
            float z = dist * Mathf.Sin(rad);

            centerOfMassList[i] = new Vector3(
                baseLocalCenter.x + x,
                baseLocalCenter.y,
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

            ResetAttemptsOnCOMChange();
            ResetGoalsOnCOMChange();

            onCOMApplied?.Invoke(selectedCOMIndex);
            COMApplied?.Invoke(selectedCOMIndex);
        }
    }

    public bool RequestNextCOM_NoLoop()
    {
        if (_cycleCompleted) return false;
        if (centerOfMassList == null || centerOfMassList.Length == 0) return false;

        if (enableRandomSelection)
        {
            int newIndex = UnityEngine.Random.Range(1, centerOfMassList.Length);

            if (newIndex == selectedCOMIndex && centerOfMassList.Length > 2)
            {
                newIndex = (newIndex % (centerOfMassList.Length - 1)) + 1;
            }

            selectedCOMIndex = newIndex;
            Debug.Log($"🎲 随机切换到 COM_{selectedCOMIndex}");
        }
        else
        {
            if (selectedCOMIndex >= centerOfMassList.Length - 1)
            {
                OnCompleteAllCOMs();
                return false;
            }

            selectedCOMIndex = Mathf.Clamp(selectedCOMIndex + 1, 0, centerOfMassList.Length - 1);
            Debug.Log($"🎮 切换到 COM_{selectedCOMIndex}");
        }

        comProgressCounter++; // ✅ 累计进度 +1

        onNextCOMChanged?.Invoke(selectedCOMIndex);
        NextCOMChanged?.Invoke(selectedCOMIndex);

        ApplyCenterOfMass(force: true);
        return true;
    }

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
        comProgressCounter = 0; // ✅ 重置进度
        SetExperimentVisualState(showCounting: true, showObjects: true);
        if (changingModeText) changingModeText.SetActive(false);
        if (changingModeBackground) changingModeBackground.SetActive(false);

        ApplyCenterOfMass(force: true);
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
            if (centerOfMassList != null && centerOfMassList.Length > 0)
            {
                selectedCOMIndex = Mathf.Clamp(selectedCOMIndex, 0, centerOfMassList.Length - 1);
            }
            RebuildCenterOfMassList();
        }
    }
#endif

    private void ResetAttemptsOnCOMChange()
    {
        if (graspCounters == null) return;
        foreach (var g in graspCounters)
        {
            if (g != null) g.ResetAttempt();
        }
    }

    private void ResetGoalsOnCOMChange()
    {
        if (goalTriggers == null) return;
        foreach (var goal in goalTriggers)
        {
            if (goal != null) goal.ResetGoalOnNextCoM();
        }
    }
}
