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
    private PincherController pincherController;

    [Header("Attempt 计数控制")]
    [SerializeField, ReadOnly]
    private bool attemptCountingEnabled = true;

    private bool hasClosed = false;
    private bool rotationEnabled = true;
    private GripState prevGripState = GripState.Fixed;

    private bool closeArmed = false;
    private bool cylinderOnTable = false;

    private bool boostActive = false;

    private bool prevCylinderInThreshold = false;

    private float normalizedCache = 0f;

    [Header("OutOfAttempt 资源 | UI & Audio")]
    public GameObject outOfAttemptUI;
    public AudioSource audioSource;
    public AudioClip outOfAttemptVoice;

    [Header("需要统一显隐的渲染器（圆柱体+五个Cube）")]
    public Renderer[] renderersToToggle; // 仍然拖 Renderer，但我们会拿它的 gameObject

    [Header("CoM 控制器（用于切换质心）")]
    public CenterOfMassController comController;

    private bool outOfAttemptRoutineRunning = false;

    [Header("ThresholdReopener Hook")]
    public ThresholdReopener thresholdReopener;

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

        if (comController != null)
            comController.COMApplied += HandleCOMAppliedAfterOutOfAttempt;
    }

    void OnDisable()
    {
        CylinderTableContact.OnCylinderTouchTable -= HandleCylinderTouchTable;
        CylinderTableContact.OnCylinderLeaveTable -= HandleCylinderLeaveTable;

        if (thresholdReopener != null)
            thresholdReopener.OnCylinderLow -= HandleCylinderLowForOutOfAttempt;

        if (comController != null)
            comController.COMApplied -= HandleCOMAppliedAfterOutOfAttempt;
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

        if (currentState == GripState.Closing && prevGripState != GripState.Closing)
        {
            if (attempt >= attemptLimit)
            {
                outOfAttemptArmed = true;
                prevGripState = currentState;
                return;
            }

            if (attemptCountingEnabled)
            {
                closeArmed = true;
            }
            hasClosed = true;
            SwitchToClosedMode();
        }
        else if (currentState == GripState.Opening && prevGripState != GripState.Opening)
        {
            hasClosed = false;
            boostActive = false;
            closeArmed = false;
            SwitchToOpenMode();
        }

        prevGripState = currentState;
    }
    #endregion

    #region Threshold 边沿检测与 Boost
    private void UpdateThresholdEdgeAndBoost()
    {
        bool nowInThreshold = IsCylinderInThreshold();

        if (prevCylinderInThreshold && !nowInThreshold)
        {
            if (hasClosed && enableOpeningBoost)
            {
                boostActive = true;
            }
        }

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
        boostActive = false;
    }

    private void HandleCylinderLeaveTable()
    {
        cylinderOnTable = false;

        if (attemptCountingEnabled && closeArmed)
        {
            attempt++;
            closeArmed = false;

            if (attempt == attemptLimit)
            {
                outOfAttemptArmed = true;
            }
        }
    }
    #endregion

    #region OutOfAttempt：通过 OnCylinderLow 触发
    private void HandleCylinderLowForOutOfAttempt()
    {
        if (!outOfAttemptArmed) return;
        if (outOfAttemptRoutineRunning) return;

        outOfAttemptArmed = false;
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

        StopAttemptCounting();

        if (outOfAttemptUI) outOfAttemptUI.SetActive(true);

        if (audioSource)
        {
            if (outOfAttemptVoice)
                audioSource.PlayOneShot(outOfAttemptVoice);
            else
                audioSource.Play();
        }

        ToggleObjects(false); // 🚀 彻底关闭 Cube 和 Cylinder

        if (comController != null)
        {
            Debug.Log("[Grasp_HandTracking] 已达到尝试上限，请按 G 或 Space 键手动切换到下一个 COM。");
        }
        else
        {
            Debug.LogWarning("[Grasp_HandTracking] 未绑定 CenterOfMassController，无法手动切换 COM。");
        }

        outOfAttemptRoutineRunning = false;
        yield break;
    }

    private void ToggleObjects(bool enabled)
    {
        if (renderersToToggle == null) return;
        foreach (var r in renderersToToggle)
        {
            if (r != null && r.gameObject != null)
            {
                r.gameObject.SetActive(enabled);
            }
        }
    }
    #endregion

    #region 恢复逻辑（手动切换 CoM 后）
    private void HandleCOMAppliedAfterOutOfAttempt(int index)
    {
        if (outOfAttemptUI && outOfAttemptUI.activeSelf)
            outOfAttemptUI.SetActive(false);

        ToggleObjects(true);   // 🚀 切换 COM 时恢复 Cube 和 Cylinder
        ResumeAttemptCounting();

        Debug.Log($"[Grasp_HandTracking] 已切换到 COM_{index}，UI 已关闭，对象已恢复，计数已恢复。");
    }
    #endregion

    #region 公共接口
    public void ResetAttempt()
    {
        attempt = 0;
        hasClosed = false;
        closeArmed = false;
        boostActive = false;
        outOfAttemptArmed = false;
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
