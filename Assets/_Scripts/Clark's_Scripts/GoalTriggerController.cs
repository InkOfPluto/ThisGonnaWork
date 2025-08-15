using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class GoalTriggerController : MonoBehaviour
{
    [Header("Goal 设置 | Goal Settings")]
    [Tooltip("圆柱体对象，用于检测其是否经过并保持在 goal 之上")]
    public GameObject cylinderObject;

    [Tooltip("保持时间要求（秒）")]
    public float holdDuration = 3f;

    [Header("音效设置 | Audio Settings")]
    [Tooltip("成功音效")]
    public AudioClip successAudioClip;

    [Tooltip("失败音效（当倾角过大导致不算成功时播放）")]
    public AudioClip failureAudioClip;

    [Tooltip("音效播放器（可选，如果为空会自动创建）")]
    public AudioSource audioSource;

    [Header("视觉效果 | Visual Effects")]
    [Tooltip("Goal 对象的渲染器组件")]
    public Renderer[] goalRenderers;

    [Tooltip("Goal 对象的碰撞器组件（包含自身触发器在内）")]
    public Collider[] goalColliders;

    [Header("成功后需要隐藏的渲染器 | Renderers To Hide After Success")]
    [Tooltip("顺序：先播放成功音效 → 隐藏 Goal（渲染器+触发器）→ 再隐藏这些渲染器（如：五个手指Cube与圆柱体）。切换 CoM 时会自动恢复这些渲染器。")]
    public Renderer[] renderersToHideAfterSuccess;

    [Header("Alarm 设置 | Alarm Settings")]
    [Tooltip("当圆柱体倾角过大时显示的 Alarm UI（例如一个包含 Image/Text 的容器 GameObject）")]
    public GameObject alarmUI;

    [Tooltip("倾角阈值（度）。当圆柱体的自身 Up 与世界 Up 的夹角 > 此值时，判定为过大")]
    public float tiltThresholdDegrees = 10f;

    [Tooltip("CylinderTableContact 事件触发后多少秒自动关闭 alarm")]
    public float alarmAutoCloseDelay = 1f;

    [Header("祝贺 UI | Congratulations UI")]
    [Tooltip("成功后显示的恭喜 UI（文本/图片容器），切换到下一个 CoM 时自动关闭")]
    public GameObject congratulationsUI;

    [Header("外部引用 | External Refs")]
    [Tooltip("抓握脚本（用于停止/恢复 attempt 计数）")]
    public Grasp_HandTracking graspHandTracking;

    [Header("调试信息 | Debug Info")]
    [ReadOnly] public bool isGoalVisible = true;
    [ReadOnly] public bool isCylinderInside = false;
    [ReadOnly] public bool hasCylinderPassedThrough = false;
    [ReadOnly] public bool isCylinderAboveAndClear = false;
    [ReadOnly] public float currentHoldTime = 0f;
    [ReadOnly] public bool alarmActive = false;

    // 私有变量
    private bool hasTriggered = false;
    private Coroutine holdCoroutine;
    private Vector3 goalCenter;
    private float goalTopY;
    private float goalBottomY;

    // 兼容旧回调（可选）
    public System.Action OnGoalCompleted;

    // —— 事件接口 ——
    public event Action<float> OnHoldSucceeded;           // 成功（参数：保持时长）
    public event Action<float> OnHoldInterrupted;         // 未满时长中断（参数：已保持时长）
    public event Action<float, float> OnHoldFailedTilt;   // 满时长但倾角失败（参数：保持时长、倾角°）

    private void OnEnable()
    {
        CylinderTableContact.OnCylinderTouchTable += OnCylinderTouchTableHandler;
    }

    private void OnDisable()
    {
        CylinderTableContact.OnCylinderTouchTable -= OnCylinderTouchTableHandler;
    }

    private void Start()
    {
        InitializeComponents();
        CalculateGoalBounds();
        SetGoalVisibility(true);
        HideAlarm();

        // 初始隐藏 Congratulations UI
        HideCongratulations();

        // 默认事件处理器
        OnHoldSucceeded += Default_OnHoldSucceeded;
        OnHoldFailedTilt += Default_OnHoldFailedTilt;
        OnHoldInterrupted += Default_OnHoldInterrupted;
    }

    private void CalculateGoalBounds()
    {
        goalCenter = transform.position;

        Collider goalCollider = GetComponent<Collider>();
        if (goalCollider != null)
        {
            Bounds bounds = goalCollider.bounds;
            goalTopY = bounds.max.y;
            goalBottomY = bounds.min.y;
        }
        else
        {
            goalTopY = goalCenter.y + 0.5f;
            goalBottomY = goalCenter.y - 0.5f;
        }

        Debug.Log($"🎯 Goal 边界计算完成 - Top: {goalTopY}, Bottom: {goalBottomY}, Center: {goalCenter.y}");
    }

    private void InitializeComponents()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;

        if (goalRenderers == null || goalRenderers.Length == 0)
            goalRenderers = GetComponentsInChildren<Renderer>();

        if (goalColliders == null || goalColliders.Length == 0)
            goalColliders = GetComponentsInChildren<Collider>(includeInactive: true);
    }

    private void Update()
    {
        if (cylinderObject != null && isGoalVisible && !hasTriggered)
        {
            CheckCylinderStatus();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isGoalVisible || hasTriggered) return;

        if (cylinderObject != null && other.gameObject == cylinderObject)
        {
            isCylinderInside = true;
            Debug.Log($"🎯 圆柱体进入 Goal trigger: {gameObject.name}");
        }
        else if (cylinderObject == null && other.CompareTag("Cylinder"))
        {
            cylinderObject = other.gameObject;
            isCylinderInside = true;
            Debug.Log($"🎯 检测到圆柱体进入 Goal trigger: {gameObject.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (cylinderObject != null && other.gameObject == cylinderObject)
        {
            if (isCylinderInside)
            {
                hasCylinderPassedThrough = true;
                Debug.Log($"🎯 圆柱体已穿过 Goal: {gameObject.name}");
            }

            isCylinderInside = false;

            // 轻量化处理：回到 Goal 下方时仅清理逻辑
            if (cylinderObject.transform.position.y < goalBottomY)
            {
                ResetGoalFlags();
                Debug.Log($"🎯 圆柱体回到 Goal 下方，仅重置 Goal 逻辑标志: {gameObject.name}");
            }
        }
    }

    private void CheckCylinderStatus()
    {
        if (cylinderObject == null) return;

        // 在 Threshold 中则暂停检测
        Rigidbody cylinderRb = cylinderObject.GetComponent<Rigidbody>();
        if (cylinderRb != null && cylinderRb.constraints != RigidbodyConstraints.None)
        {
            return;
        }

        Vector3 cylinderPos = cylinderObject.transform.position;

        bool wasAboveAndClear = isCylinderAboveAndClear;
        isCylinderAboveAndClear = hasCylinderPassedThrough &&
                                  cylinderPos.y > goalTopY &&
                                  !isCylinderInside;

        if (isCylinderAboveAndClear && !wasAboveAndClear)
        {
            StartHoldTimer();
        }
        else if (!isCylinderAboveAndClear && wasAboveAndClear)
        {
            StopHoldTimer();
        }
    }

    // —— 仅重置逻辑标志 —— 
    private void ResetGoalFlags()
    {
        hasCylinderPassedThrough = false;
        isCylinderAboveAndClear = false;
        isCylinderInside = false;
        currentHoldTime = 0f;

        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }

        HideAlarm();
    }

    // —— 带 Threshold 保护的重置（保留，未直接使用） ——
    private void ResetCylinderState()
    {
        ResetGoalFlags();

        if (cylinderObject != null)
        {
            bool isInThreshold = false;

            Rigidbody cylinderRb = cylinderObject.GetComponent<Rigidbody>();
            if (cylinderRb != null && (cylinderRb.constraints & (RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ)) != 0)
            {
                isInThreshold = true;
            }

            Vector3 pos = cylinderObject.transform.position;
            if (Mathf.Approximately(pos.x, 0f) && Mathf.Approximately(pos.z, 0.3f))
            {
                isInThreshold = true;
            }

            ThresholdLockAndReset thresholdScript = cylinderObject.GetComponent<ThresholdLockAndReset>();

            if (isInThreshold)
            {
                Debug.Log($"🚫 圆柱体在 Threshold 状态中，Goal 重置保护启动 - Goal: {gameObject.name}");
                Debug.Log($"   - 位置: {pos}");
                return;
            }
        }

        Debug.Log($"🔄 圆柱体状态已重置 - Goal: {gameObject.name}");
    }

    private void StartHoldTimer()
    {
        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        holdCoroutine = StartCoroutine(HoldTimerCoroutine());
        Debug.Log($"⏱️ 开始计时 - Goal: {gameObject.name}");
    }

    private void StopHoldTimer()
    {
        bool hadTimer = (holdCoroutine != null);

        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }

        // 未满时长被打断 → 触发中断事件
        if (hadTimer && currentHoldTime > 0f && currentHoldTime < holdDuration)
        {
            OnHoldInterrupted?.Invoke(currentHoldTime);
        }

        currentHoldTime = 0f;
        Debug.Log($"⏱️ 停止计时 - Goal: {gameObject.name}");
    }

    private IEnumerator HoldTimerCoroutine()
    {
        currentHoldTime = 0f;

        while (currentHoldTime < holdDuration && isCylinderAboveAndClear)
        {
            currentHoldTime += Time.deltaTime;
            yield return null;
        }

        if (currentHoldTime >= holdDuration && isCylinderAboveAndClear)
        {
            // 达时检查倾角
            float angle = 0f;
            bool tiltTooLarge = false;
            if (cylinderObject != null)
            {
                angle = Vector3.Angle(cylinderObject.transform.up, Vector3.up);
                tiltTooLarge = angle > tiltThresholdDegrees;
            }

            if (tiltTooLarge)
            {
                OnHoldFailedTilt?.Invoke(currentHoldTime, angle);
            }
            else
            {
                FireHoldSucceeded(currentHoldTime);
            }
        }

        holdCoroutine = null;
    }

    // 统一控制成功只触发一次
    private void FireHoldSucceeded(float duration)
    {
        if (hasTriggered) return;
        hasTriggered = true;
        OnHoldSucceeded?.Invoke(duration);
    }

    // 兼容旧方法：内部走事件
    private void TriggerGoalComplete()
    {
        FireHoldSucceeded(holdDuration);
    }

    private void PlaySuccessAudio()
    {
        if (audioSource != null && successAudioClip != null)
        {
            audioSource.PlayOneShot(successAudioClip);
        }
        else
        {
            Debug.LogWarning($"⚠️ 无法播放成功音效 - AudioSource: {audioSource != null}, AudioClip: {successAudioClip != null}");
        }
    }

    private void PlayFailureAudio()
    {
        if (audioSource != null && failureAudioClip != null)
        {
            audioSource.PlayOneShot(failureAudioClip);
        }
        else
        {
            Debug.LogWarning($"⚠️ 无法播放失败音效 - AudioSource: {audioSource != null}, AudioClip: {failureAudioClip != null}");
        }
    }

    private void SetGoalVisibility(bool visible)
    {
        isGoalVisible = visible;

        if (goalRenderers != null)
        {
            foreach (var renderer in goalRenderers)
            {
                if (renderer != null) renderer.enabled = visible;
            }
        }

        // 包含自身触发器在内的所有 Collider 一起显隐
        if (goalColliders != null && goalColliders.Length > 0)
        {
            foreach (var c in goalColliders)
            {
                if (c != null) c.enabled = visible;
            }
        }
        else
        {
            var selfCol = GetComponent<Collider>();
            if (selfCol) selfCol.enabled = visible;
        }
    }

    /// <summary>
    /// 由 CenterOfMassController 调用：切换 CoM 时重置 Goal
    /// - 重新显示 Goal（渲染器+触发器）
    /// - 恢复五指/圆柱体等渲染器
    /// - 关闭 Congratulations UI
    /// - 恢复 attempt 计数
    /// </summary>
    public void ResetGoalOnNextCoM()
    {
        hasTriggered = false;

        bool cylinderInThreshold = false;
        if (cylinderObject != null)
        {
            Rigidbody cylinderRb = cylinderObject.GetComponent<Rigidbody>();
            if (cylinderRb != null && cylinderRb.constraints != RigidbodyConstraints.None)
            {
                cylinderInThreshold = true;
                Debug.Log($"🚫 圆柱体在 Threshold 中，COM 切换时跳过圆柱体物理重置 - Goal: {gameObject.name}");
            }
        }

        if (!cylinderInThreshold)
        {
            ResetGoalFlags();
        }
        else
        {
            hasTriggered = false;
            hasCylinderPassedThrough = false;
            isCylinderAboveAndClear = false;
            currentHoldTime = 0f;
            isCylinderInside = false;

            if (holdCoroutine != null)
            {
                StopCoroutine(holdCoroutine);
                holdCoroutine = null;
            }

            Debug.Log($"🔄 Goal 状态已重置（保护 Threshold 状态）- Goal: {gameObject.name}");
        }

        // 恢复 Goal 与相关渲染器
        SetGoalVisibility(true);
        HideAlarm();
        SetRenderersEnabled(renderersToHideAfterSuccess, true);

        // 关闭 Congratulations UI
        HideCongratulations();

        // —— 恢复 attempt 计数 —— 
        if (graspHandTracking != null)
        {
            graspHandTracking.ResumeAttemptCounting();
        }

        Debug.Log($"🔁 Goal 已重置并重新出现、已恢复手指渲染器且关闭 Congratulations: {gameObject.name}");
    }

    public void ManualReset()
    {
        ResetGoalFlags();
        SetGoalVisibility(true);
        hasTriggered = false;
        HideAlarm();
        HideCongratulations();
    }

    public bool IsCompleted() => hasTriggered;

    public float GetHoldProgress() => Mathf.Clamp01(currentHoldTime / holdDuration);

    // ======== 倾角判定 / Alarm / UI / 渲染器工具 ========

    private bool IsCylinderTiltTooLarge()
    {
        if (cylinderObject == null) return false;
        float angle = Vector3.Angle(cylinderObject.transform.up, Vector3.up);
        return angle > tiltThresholdDegrees;
    }

    private void ShowAlarm()
    {
        if (alarmUI != null)
        {
            alarmUI.SetActive(true);
            alarmActive = true;
            Debug.Log("⚠️ 倾角过大，显示 Alarm。等待 CylinderTableContact 事件后延时关闭。");
        }
        else
        {
            Debug.LogWarning("⚠️ AlarmUI 未绑定，无法显示 Alarm。请在 Inspector 指定 alarmUI。");
        }
    }

    private void HideAlarm()
    {
        if (alarmUI != null) alarmUI.SetActive(false);
        alarmActive = false;
    }

    private void ShowCongratulations()
    {
        if (congratulationsUI != null) congratulationsUI.SetActive(true);
    }

    private void HideCongratulations()
    {
        if (congratulationsUI != null) congratulationsUI.SetActive(false);
    }

    private void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = enabled;
        }
    }

    // —— 默认事件处理器 —— 

    private void Default_OnHoldSucceeded(float duration)
    {
        Debug.Log($"✅ [Event] HoldSucceeded: 保持 {duration:F2}s，执行默认成功流程。");

        // 祝贺 UI
        ShowCongratulations();

        // —— 暂停 attempt 计数 —— 
        if (graspHandTracking != null)
        {
            graspHandTracking.StopAttemptCounting();
        }

        // 成功音效 + 隐藏
        PlaySuccessAudio();
        SetGoalVisibility(false);
        SetRenderersEnabled(renderersToHideAfterSuccess, false);

        // 兼容旧回调
        OnGoalCompleted?.Invoke();

        // 不打扰物理，只清理逻辑
        ResetGoalFlags();
    }

    private void Default_OnHoldFailedTilt(float duration, float angleDeg)
    {
        Debug.Log($"⚠️ [Event] HoldFailedTilt: 保持 {duration:F2}s 但倾角 {angleDeg:F1}° 超阈值 {tiltThresholdDegrees}°。");
        ShowAlarm();
        PlayFailureAudio();
    }

    private void Default_OnHoldInterrupted(float elapsed)
    {
        Debug.Log($"ℹ️ [Event] HoldInterrupted: 已保持 {elapsed:F2}s（未达 {holdDuration:F2}s），检测到离开上方区域。");
    }

    // 圆柱体与桌面接触事件：用于延时关闭 Alarm
    private void OnCylinderTouchTableHandler()
    {
        if (alarmActive && alarmUI != null)
        {
            StartCoroutine(CloseAlarmAfterDelay(alarmAutoCloseDelay));
        }
    }

    private IEnumerator CloseAlarmAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideAlarm();
        Debug.Log("✅ Alarm 已关闭。");
    }
}
