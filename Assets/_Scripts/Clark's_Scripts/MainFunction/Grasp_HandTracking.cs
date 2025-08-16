using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grasp_HandTracking : MonoBehaviour
{
    #region 手部引用
    [Header("手部引用 | Hand Refs")]
    public Transform[] fingerTips;
    public Transform thumbtip;
    public GameObject hand;
    #endregion

    #region 抓握参数
    [Header("抓握距离参数")]
    public float minGripDistance = 0.01f;
    public float maxGripDistance = 0.1f;

    [Header("抓握判定阈值（滞回）")]
    [Tooltip("normalized < 该值 判定为 Closing，建议略小于 0.5")]
    public float closingThreshold = 0.5f;

    [Tooltip("normalized > 该值 判定为 Opening 的基础阈值")]
    public float openingThresholdBase = 0.5f;

    [Tooltip("离开 Threshold 后临时抬高 Opening 阈值的附加量")]
    public float openingBoost = 0.15f;

    [Tooltip("是否启用“离开 Threshold 后提高张开阈值（防误松）”")]
    public bool enableOpeningBoost = true;
    #endregion

    #region 统计信息
    [Header("抓握次数统计")]
    [ReadOnly, SerializeField]
    private int attempt = 0;

    [Header("延迟开启时间")]
    public float Delay = 1f;

    // ★ 新增：最大尝试数（达到后阻止下一次正常抓取流程）
    [Header("尝试上限 | Attempt Limit")]
    public int attemptLimit = 5;
    #endregion

    #region 场景引用
    [Header("场景引用 | Scene Refs")]
    public GameObject uglyHand;
    public Transform cylinder;
    public Collider thresholdZone;
    public Collider cylinderCollider;
    public Collider tableCollider;
    #endregion

    #region 模式脚本
    [Header("模式脚本 | Mode Behaviours")]
    public Behaviour upDownBehaviour;
    public Behaviour followBehaviour;
    public Behaviour[] rotationBehaviours;
    #endregion

    #region 私有变量与新增引用
    // 组件
    private PincherController pincherController;

    [Header("Attempt 计数控制")]
    [SerializeField, ReadOnly]
    private bool attemptCountingEnabled = true;

    private bool hasClosed = false;
    private bool rotationEnabled = true;
    private GripState prevGripState = GripState.Fixed;

    // 计数武装态 & 桌面接触状态
    private bool closeArmed = false;
    private bool cylinderOnTable = false;

    // 防误松 boost
    private bool boostActive = false;

    // 阈值内外状态
    private bool prevCylinderInThreshold = false;

    private float normalizedCache = 0f;

    // ★ 超出尝试 UI/音频/渲染器控制
    [Header("OutOfAttempt 资源 | UI & Audio")]
    [Tooltip("超出尝试时显示的 UI 文本/图片容器")]
    public GameObject outOfAttemptUI;          // 赋值：你的“OutOfAttempt”UI 物体
    [Tooltip("播放超出尝试提示音的 AudioSource")]
    public AudioSource audioSource;            // 赋值：一个 AudioSource
    [Tooltip("超出尝试时播放的语音剪辑")]
    public AudioClip outOfAttemptVoice;        // 赋值：OutOfAttemptVoice

    [Header("需要统一显隐的渲染器（圆柱体+五个Cube）")]
    public Renderer[] renderersToToggle;       // 赋值：圆柱体+五个手指的 Renderer

    [Header("CoM 控制器（用于切换质心）")]
    public CenterOfMassController comController;  // 赋值：场景里的 CenterOfMassController

    // 防止重复触发
    private bool outOfAttemptRoutineRunning = false;

    // ★ 新增：与 ThresholdReopener 的联动
    [Header("ThresholdReopener Hook")]
    public ThresholdReopener thresholdReopener;

    // 达到上限后等待 OnCylinderLow 再触发的武装标志
    [SerializeField, ReadOnly]
    private bool outOfAttemptArmed = false;
    #endregion

    #region Unity 生命周期
    void OnEnable()
    {
        CylinderTableContact.OnCylinderTouchTable += HandleCylinderTouchTable;
        CylinderTableContact.OnCylinderLeaveTable += HandleCylinderLeaveTable;

        if (thresholdReopener != null)
            thresholdReopener.OnCylinderLow += HandleCylinderLowForOutOfAttempt;
    }

    void OnDisable()
    {
        CylinderTableContact.OnCylinderTouchTable -= HandleCylinderTouchTable;
        CylinderTableContact.OnCylinderLeaveTable -= HandleCylinderLeaveTable;

        if (thresholdReopener != null)
            thresholdReopener.OnCylinderLow -= HandleCylinderLowForOutOfAttempt;
    }

    void Start()
    {
        InitializeComponents();
        ValidateParameters();

        cylinderOnTable = EstimateCylinderOnTableByBounds();
        prevCylinderInThreshold = IsCylinderInThreshold();

        SwitchToOpenMode(initial: true);
    }

    void Update()
    {
        if (!ValidateRequiredComponents()) return;

        UpdateThresholdEdgeAndBoost();
        CalculateGripState();
        HandleGripStateTransitions();
        UpdateUglyHandVisibility();
    }
    #endregion

    #region 初始化与验证
    private void InitializeComponents()
    {
        if (hand != null)
        {
            pincherController = hand.GetComponent<PincherController>();
            if (pincherController == null)
                Debug.LogWarning("[Grasp_HandTracking] hand 上未找到 PincherController 组件。");
        }
        else
        {
            Debug.LogWarning("[Grasp_HandTracking] hand 未绑定。");
        }
    }

    private void ValidateParameters()
    {
        if (maxGripDistance <= minGripDistance)
        {
            Debug.LogWarning("[Grasp_HandTracking] maxGripDistance 应大于 minGripDistance，已自动微调。");
            maxGripDistance = minGripDistance + 0.001f;
        }
        closingThreshold = Mathf.Clamp01(closingThreshold);
        openingThresholdBase = Mathf.Clamp01(openingThresholdBase);
        openingBoost = Mathf.Clamp(openingBoost, 0f, 0.49f);
    }

    private bool ValidateRequiredComponents()
    {
        return pincherController != null &&
               thumbtip != null &&
               fingerTips != null &&
               fingerTips.Length > 0;
    }
    #endregion

    #region 抓握计算与状态转换
    private void CalculateGripState()
    {
        float fingertipDist = 0f;
        int validCount = 0;

        foreach (Transform fingertip in fingerTips)
        {
            if (fingertip == null) continue;
            fingertipDist += Vector3.Distance(thumbtip.position, fingertip.position);
            validCount++;
        }
        if (validCount == 0) return;

        float avgFingertipDist = fingertipDist / validCount;
        float normalized = Mathf.Clamp01((avgFingertipDist - minGripDistance) / (maxGripDistance - minGripDistance));
        normalizedCache = normalized;

        float openingThreshold = openingThresholdBase + (enableOpeningBoost && boostActive ? openingBoost : 0f);
        openingThreshold = Mathf.Clamp01(openingThreshold);

        GripState newState;
        if (normalized < closingThreshold) newState = GripState.Closing;
        else if (normalized > openingThreshold) newState = GripState.Opening;
        else newState = GripState.Fixed;

        pincherController.gripState = newState;
    }

    private void HandleGripStateTransitions()
    {
        var currentState = pincherController.gripState;

        // —— Closing 边沿 —— //
        if (currentState == GripState.Closing && prevGripState != GripState.Closing)
        {
            // 已达到上限：此处不触发协程，仅阻止正常抓取流程，并武装等待 OnCylinderLow
            if (attempt >= attemptLimit)
            {
                outOfAttemptArmed = true;           // 标记等待 OnCylinderLow
                prevGripState = currentState;       // 更新前态，避免重复触发
                return;                              // 不进入闭合模式
            }

            // 正常流程：进入武装态，等待离桌计数
            if (attemptCountingEnabled)
            {
                closeArmed = true;
            }
            hasClosed = true;
            SwitchToClosedMode();
        }
        // —— Opening 边沿 —— //
        else if (currentState == GripState.Opening && prevGripState != GripState.Opening)
        {
            hasClosed = false;
            boostActive = false; // 真正张开后，关闭防误松
            closeArmed = false;  // 解除武装
            SwitchToOpenMode();
        }

        prevGripState = currentState;
    }
    #endregion

    #region Threshold 边沿检测与 Boost
    private void UpdateThresholdEdgeAndBoost()
    {
        bool nowInThreshold = IsCylinderInThreshold();

        // 内 -> 外：离开 Threshold，若已抓住过物体，则开启防误松
        if (prevCylinderInThreshold && !nowInThreshold)
        {
            if (hasClosed && enableOpeningBoost)
            {
                boostActive = true;
            }
        }

        // 外 -> 内：回到 Threshold，关闭防误松
        if (!prevCylinderInThreshold && nowInThreshold)
        {
            boostActive = false;
        }

        prevCylinderInThreshold = nowInThreshold;
    }
    #endregion

    #region UglyHand & 环境状态
    private void UpdateUglyHandVisibility()
    {
        bool cylinderInThreshold = IsCylinderInThreshold();
        bool cylinderTouchingTable = IsCylinderTouchingTable();
        bool isHandOpen = IsHandOpen();
        ApplyUglyHandVisibilityRules(cylinderTouchingTable, isHandOpen, cylinderInThreshold);
    }

    private bool IsCylinderInThreshold()
    {
        if (thresholdZone == null || cylinder == null) return false;
        return thresholdZone.bounds.Contains(cylinder.position);
    }

    private bool IsCylinderTouchingTable()
    {
        if (cylinderCollider != null) return cylinderOnTable;
        return EstimateCylinderOnTableByBounds();
    }

    private bool EstimateCylinderOnTableByBounds()
    {
        if (cylinderCollider == null) return false;

        if (tableCollider != null)
        {
            return cylinderCollider.bounds.Intersects(tableCollider.bounds);
        }

        var tables = GameObject.FindGameObjectsWithTag("Table");
        foreach (var table in tables)
        {
            var collider = table.GetComponent<Collider>();
            if (collider != null && cylinderCollider.bounds.Intersects(collider.bounds))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsHandOpen()
    {
        if (pincherController == null) return false;
        return pincherController.gripState == GripState.Opening ||
               (pincherController.gripState == GripState.Fixed && pincherController.grip < 0.5f);
    }

    private void ApplyUglyHandVisibilityRules(bool cylinderTouchingTable, bool isHandOpen, bool cylinderInThreshold)
    {
        if (cylinderTouchingTable) SetUglyHand(true);
        else if (isHandOpen && !cylinderInThreshold) SetUglyHand(false);
        else SetUglyHand(true);
    }

    private void SetUglyHand(bool isActive)
    {
        if (uglyHand == null) return;
        if (uglyHand.activeSelf == isActive) return;
        uglyHand.SetActive(isActive);
    }
    #endregion

    #region 模式切换
    private void SwitchToOpenMode(bool initial = false)
    {
        SetEnabled(upDownBehaviour, false);
        SetEnabled(followBehaviour, true);
    }

    private void SwitchToClosedMode()
    {
        SetEnabled(followBehaviour, false);
        StartCoroutine(EnableUpDownAfterDelay(Delay));
    }

    private IEnumerator EnableUpDownAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetEnabled(upDownBehaviour, true);
    }

    private void SetEnabled(Behaviour behaviour, bool isEnabled)
    {
        if (behaviour == null) return;
        if (behaviour.enabled == isEnabled) return;
        behaviour.enabled = isEnabled;
    }
    #endregion

    #region 圆柱体-桌面 事件处理
    private void HandleCylinderTouchTable()
    {
        cylinderOnTable = true;
        boostActive = false; // 放回桌面 → 不需要防误松
    }

    private void HandleCylinderLeaveTable()
    {
        cylinderOnTable = false;

        // 正常计数逻辑：合拢后离桌才 +1
        if (attemptCountingEnabled && closeArmed)
        {
            attempt++;
            closeArmed = false;

            // 刚好达到上限时，武装等待 OnCylinderLow
            if (attempt == attemptLimit)
            {
                outOfAttemptArmed = true;
                // Debug.Log("[Grasp_HandTracking] Attempt reached limit; armed for OnCylinderLow.");
            }
        }
    }
    #endregion

    #region OutOfAttempt：通过 OnCylinderLow 触发（★修改后）
    // 收到 ThresholdReopener 的 OnCylinderLow 才真正触发超限流程
    private void HandleCylinderLowForOutOfAttempt()
    {
        if (!outOfAttemptArmed) return;
        if (outOfAttemptRoutineRunning) return;

        outOfAttemptArmed = false; // 解除武装
        TryTriggerOutOfAttempt();
    }

    private void TryTriggerOutOfAttempt()
    {
        if (outOfAttemptRoutineRunning) return;
        StartCoroutine(OutOfAttemptRoutine());
    }

    private IEnumerator OutOfAttemptRoutine()
    {
        outOfAttemptRoutineRunning = true;

        // 1) 暂停 attempt 计数，防止 3~5 秒内发生额外事件
        StopAttemptCounting();

        // 2) 显示 UI
        if (outOfAttemptUI) outOfAttemptUI.SetActive(true);

        // 3) 播放音频
        if (audioSource)
        {
            if (outOfAttemptVoice)
            {
                audioSource.PlayOneShot(outOfAttemptVoice);
            }
            else
            {
                audioSource.Play(); // 若已在 AudioSource 里设置好 clip
            }
        }

        // 4) 隐藏渲染器（圆柱体 + 五个 Cube）
        ToggleRenderers(false);

        // 5) 等待 3 秒
        yield return new WaitForSeconds(3f);

        // 6) 关闭 UI
        if (outOfAttemptUI) outOfAttemptUI.SetActive(false);

        // 7) 恢复渲染器
        ToggleRenderers(true);

        // 8) 切换到下一个 CoM（不循环）
        if (comController != null)
        {
            bool changed = comController.RequestNextCOM_NoLoop();
            if (!changed)
            {
                Debug.Log("[Grasp_HandTracking] 已到最后一个 COM，CenterOfMassController 会进入 ChangingMode。");
            }
        }
        else
        {
            Debug.LogWarning("[Grasp_HandTracking] 未绑定 CenterOfMassController，无法切换 COM。");
        }

        // 9) 恢复 attempt 计数（CenterOfMassController 应已重置 attempt = 0）
        ResumeAttemptCounting();

        outOfAttemptRoutineRunning = false;
    }

    private void ToggleRenderers(bool enabled)
    {
        if (renderersToToggle == null) return;
        foreach (var r in renderersToToggle)
        {
            if (r) r.enabled = enabled;
        }
    }
    #endregion

    #region 公共接口
    public void ResetAttempt()
    {
        attempt = 0;
        hasClosed = false;
        closeArmed = false;
        boostActive = false;
        outOfAttemptArmed = false;   // 清理武装标志
        SwitchToOpenMode();
    }

    public void StopAttemptCounting()
    {
        attemptCountingEnabled = false;
        closeArmed = false;
        Debug.Log("[Grasp_HandTracking] Attempt 计数已暂停。");
    }

    public void ResumeAttemptCounting()
    {
        attemptCountingEnabled = true;
        Debug.Log("[Grasp_HandTracking] Attempt 计数已恢复。");
    }

    public int AttemptCount => attempt;
    #endregion
}
