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
          "按 Space 或 Xbox B 键：切换到下一个重心（COM）。当到达最后一个时，不再循环，隐藏对象与Counting文本+背景，显示ChangingMode文本+背景；模式切换后重置到0号重心。\n" +
          "新增：当处于 CoM0 时按 G 键，可在 (0,0,0) 与 (0,0,1) 之间切换 CoM0 的质心。"; // ✅ 新增说明

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
        new COMDistanceAngle(0.000f, 0.0f),    // 0 —— 中心

        // 编号 1-5：使用 distanceGroup1
        new COMDistanceAngle(0f, 0.0f),  // 1
        new COMDistanceAngle(0f, 120.0f),  // 2
        new COMDistanceAngle(0f, 138.0f),  // 3
        new COMDistanceAngle(0f, 52.0f),  // 4
        new COMDistanceAngle(0f, 115.0f),  // 5

        // 编号 6-10：使用 distanceGroup2
        new COMDistanceAngle(0f, 352.0f),    // 6
        new COMDistanceAngle(0f, 20.0f),  // 7
        new COMDistanceAngle(0f, 335.0f),  // 8
        new COMDistanceAngle(0f, 84.0f),  // 9
        new COMDistanceAngle(0f, 27.0f),  // 10

        // 编号 11-15：使用 distanceGroup3
        new COMDistanceAngle(0f, 115.0f),  // 11
        new COMDistanceAngle(0f, 60.0f),  // 12
        new COMDistanceAngle(0f, 150.0f),  // 13
        new COMDistanceAngle(0f, 104.0f),  // 14
        new COMDistanceAngle(0f, 6.0f),  // 15
    };

    [Header("同距离微扰参数 | Jitter For Same Distance")]
    public bool enableRandomJitter = true;
    public float randomAngleJitterDeg = 5f;
    public float randomRadiusJitter = 0.01f;
    public int jitterSeedOffset = 12345;

    [Header("切换方式 | Switch Mode")]
    public bool enableRandomSelection = false;

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
    public ModeSwitch modeSwitch;

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

    private bool[] visited;
    private bool _allVisitedButNotCompleted = false; // ✅ 已访问完但未进入ChangingMode
    [Header("已访问状态（调试用）| Visited Status (Debug)")]
    [ReadOnly] public string[] visitedStatus;

    [Header("累计质心进度 | COM Progress Counter")]
    [ReadOnly] public int comProgressCounter = 0;

    [Header("调试 | Debug Flags")]
    [ReadOnly] public bool allVisitedButNotCompletedFlag = false; // ✅ Inspector显示标志

    [Header("CoM0 切换状态（仅调试显示）")]
    [ReadOnly] public bool com0Raised = false; // false=(0,0,0), true=(0,0,1)
    private static readonly Vector3 kCoM0_Default = new Vector3(0f, 0f, 0f);
    private static readonly Vector3 kCoM0_Raised = new Vector3(0f, 0f, 1f);


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
        InitVisited();
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

                // ==== ✅ 新增：仅当处于 CoM0 时，按 G 切换 (0,0,0) ↔ (0,0,1) ====
                if (selectedCOMIndex == 0 && Input.GetKeyDown(KeyCode.G))
                {
                    com0Raised = !com0Raised;
                    ApplyCenterOfMass(force: true); // 立即应用
                    Debug.Log(com0Raised
                        ? "⬆️ CoM0 切换为 (0,0,1)"
                        : "⬇️ CoM0 还原为 (0,0,0)");
                }
                // ============================================================
            }
        }

        ApplyCenterOfMass();
        UpdateVisitedStatus();
    }

    void InitVisited()
    {
        visited = new bool[centerOfMassList.Length];
        visitedStatus = new string[centerOfMassList.Length];
        _allVisitedButNotCompleted = false;
        allVisitedButNotCompletedFlag = false;
        UpdateVisitedStatus();
    }

    void UpdateVisitedStatus()
    {
        if (visited == null || visitedStatus == null) return;
        for (int i = 0; i < visited.Length; i++)
        {
            visitedStatus[i] = visited[i] ? $"COM_{i} ✅" : $"COM_{i} ❌";
        }
        allVisitedButNotCompletedFlag = _allVisitedButNotCompleted;
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
            float x = dist * Mathf.Cos(rad);
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

        // 当索引变化时，若离开 CoM0，自动复位切换状态
        if (selectedCOMIndex != lastAppliedIndex && selectedCOMIndex != 0)
        {
            com0Raised = false; // ✅ 离开 CoM0 时恢复默认
        }

        if ((force || selectedCOMIndex != lastAppliedIndex) &&
            selectedCOMIndex >= 0 && selectedCOMIndex < centerOfMassList.Length)
        {
            Vector3 desired = centerOfMassList[selectedCOMIndex];

            // ✅ 覆盖：CoM0 的两种固定本地坐标
            if (selectedCOMIndex == 0)
            {
                desired = com0Raised ? kCoM0_Raised : kCoM0_Default;
            }

            rb.centerOfMass = desired;
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

        // ✅ 如果已全部访问过，但还没完成 → 这次按键进入 ChangingMode
        if (_allVisitedButNotCompleted)
        {
            OnCompleteAllCOMs();
            return false;
        }

        if (enableRandomSelection)
        {
            // 随机选择 1~N-1
            int newIndex;
            do
            {
                newIndex = UnityEngine.Random.Range(1, centerOfMassList.Length);
            } while (visited[newIndex] && !AllVisitedExceptZero());

            selectedCOMIndex = newIndex;
            visited[selectedCOMIndex] = true;
            Debug.Log($"🎲 随机切换到 COM_{selectedCOMIndex}");

            // ✅ 如果已经全部访问过 → 等待下一次按键
            if (AllVisitedExceptZero())
            {
                _allVisitedButNotCompleted = true;
                Debug.Log("⚠️ 所有 COM 已访问过，等待下一次按键进入 ChangingMode...");
            }
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

        comProgressCounter++;
        onNextCOMChanged?.Invoke(selectedCOMIndex);
        NextCOMChanged?.Invoke(selectedCOMIndex);
        ApplyCenterOfMass(force: true);
        UpdateVisitedStatus();
        return true;
    }

    bool AllVisitedExceptZero()
    {
        for (int i = 1; i < visited.Length; i++)
        {
            if (!visited[i]) return false;
        }
        return true;
    }

    void OnCompleteAllCOMs()
    {
        _cycleCompleted = true;
        _allVisitedButNotCompleted = false;
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
        _allVisitedButNotCompleted = false;
        selectedCOMIndex = 0;
        lastAppliedIndex = -1;
        comProgressCounter = 0;

        // ✅ 模式切换时也重置 CoM0 切换状态
        com0Raised = false;

        InitVisited();
        UpdateVisitedStatus();
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

            if (visited == null || visited.Length != centerOfMassList.Length)
            {
                visited = new bool[centerOfMassList.Length];
            }
            if (visitedStatus == null || visitedStatus.Length != centerOfMassList.Length)
            {
                visitedStatus = new string[centerOfMassList.Length];
            }
            UpdateVisitedStatus();
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
