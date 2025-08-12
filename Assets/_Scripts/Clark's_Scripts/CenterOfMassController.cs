// 文件名：CenterOfMassController.cs
using UnityEngine;

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

    [Header("质心序号 | COM Index（0 ~ N-1）")]
    [Range(0, 15)]
    public int selectedCOMIndex = 0;

    [Header("重心坐标列表 | Center of Mass List（可在 Inspector 自行调整长度与值）")]
    public Vector3[] centerOfMassList = new Vector3[]
    {
        new Vector3( 0.000f,  0.000f,  0.000f),  // 0
        new Vector3( 0.081f,  0.047f, -0.039f), // 1
        new Vector3(-0.093f, -0.019f,  0.088f), // 2
        new Vector3( 0.014f,  0.097f, -0.074f), // 3
        new Vector3(-0.078f,  0.065f,  0.022f), // 4
        new Vector3( 0.058f, -0.091f, -0.067f), // 5
        new Vector3(-0.006f,  0.030f,  0.096f), // 6
        new Vector3( 0.087f, -0.058f,  0.079f), // 7
        new Vector3(-0.091f, -0.031f, -0.092f), // 8
        new Vector3( 0.025f,  0.098f, -0.005f), // 9
        new Vector3( 0.050f,  0.010f, -0.030f), // 10
        new Vector3(-0.070f,  0.080f,  0.040f), // 11
        new Vector3( 0.020f, -0.040f,  0.060f), // 12
        new Vector3(-0.030f,  0.020f, -0.070f), // 13
        new Vector3( 0.060f, -0.020f,  0.090f), // 14
        new Vector3(-0.044f,  0.055f, -0.083f), // 15
    };

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

    [Header("旋转按钮 | Rotate Button（切换 COM 时强制进入旋转模式）")]
    public ButtonForRotateFingers rotateButton; // 在 Inspector 里拖入 ButtonForRotateFingers 对象

    [Header("Goal 触发器 | Goal Triggers（切换 COM 时重置以重新出现）")]
    public GoalTriggerController[] goalTriggers; // 把挂了 GoalTriggerController 的 goal 物体拖进来

    // —— 运行期状态 —— //
    private Rigidbody rb;
    private int lastAppliedIndex = -1;
    private bool _isPressed = false;
    private bool _cycleCompleted = false;

    ExperimentSaveData_JSON saveJSON;

    private void Start()
    {
        saveJSON = FindObjectOfType<ExperimentSaveData_JSON>();
        TryGetComponents();
        SetExperimentVisualState(showCounting: true, showObjects: true);
        if (changingModeText) changingModeText.SetActive(false);
        if (changingModeBackground) changingModeBackground.SetActive(false);

        ApplyCenterOfMass(force: true);   // 真实应用 → 会重置计数并回调 ResetGoalsOnCOMChange()
        ResetGoalsOnCOMChange();          // 确保初始进场时 goal 可见
    }

    private void Update()
    {
        TryGetComponents();

        if (Application.isPlaying)
        {
            if (!_cycleCompleted)
            {
                if (!_isPressed && (Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.G)))
                {
                    _isPressed = true;
                    CycleToNextCOM_NoLoop();
                }
                if (_isPressed && (Input.GetKeyUp(KeyCode.JoystickButton1) || Input.GetKeyUp(KeyCode.G)))
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

    void ApplyCenterOfMass(bool force = false)
    {
        if (targetObject == null || rb == null || centerOfMassList == null || centerOfMassList.Length == 0) return;

        if ((force || selectedCOMIndex != lastAppliedIndex) &&
            selectedCOMIndex >= 0 && selectedCOMIndex < centerOfMassList.Length)
        {
            rb.centerOfMass = centerOfMassList[selectedCOMIndex];
            lastAppliedIndex = selectedCOMIndex;

            // 日志记录
            if (saveJSON != null)
            {
                saveJSON.OnCOMChanged(selectedCOMIndex, centerOfMassList[selectedCOMIndex]);
            }

            // —— 真实“应用了新 COM”后的回调 —— //
            ResetAttemptsOnCOMChange();   // 清零抓握计数
            ResetGoalsOnCOMChange();      // 让 goal 重新出现（关键行）
        }
    }

    void CycleToNextCOM_NoLoop()
    {
        if (centerOfMassList == null || centerOfMassList.Length == 0) return;

        if (selectedCOMIndex >= centerOfMassList.Length - 1)
        {
            OnCompleteAllCOMs();
            return;
        }

        selectedCOMIndex = Mathf.Clamp(selectedCOMIndex + 1, 0, centerOfMassList.Length - 1);
        Debug.Log($"🎮 切换到 COM_{selectedCOMIndex}");

        // 切换 COM 的同时，进入旋转模式（Rotate）
        if (rotateButton != null)
        {
            rotateButton.EnterRotateModeFromCOMChange();
        }
        else
        {
            Debug.LogWarning("⚠️ rotateButton 未绑定，无法在切换 COM 时进入旋转模式。");
        }
        // 注意：不在这里重置 goal；统一在 ApplyCenterOfMass 的“实际应用时”重置
    }

    void OnCompleteAllCOMs()
    {
        _cycleCompleted = true;
        SetExperimentVisualState(showCounting: false, showObjects: false);
        if (changingModeText) changingModeText.SetActive(true);
        if (changingModeBackground) changingModeBackground.SetActive(true);
        Debug.Log("✅ 已完成所有质心；停止循环，进入 ChangingMode 提示。");
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
            if (centerOfMassList != null && centerOfMassList.Length > 0)
            {
                selectedCOMIndex = Mathf.Clamp(selectedCOMIndex, 0, centerOfMassList.Length - 1);
            }
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
