// 文件名：GoalTriggerController.cs
using UnityEngine;
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

    [Tooltip("音效播放器（可选，如果为空会自动创建）")]
    public AudioSource audioSource;

    [Header("视觉效果 | Visual Effects")]
    [Tooltip("Goal 对象的渲染器组件")]
    public Renderer[] goalRenderers;

    [Tooltip("Goal 对象的碰撞器组件")]
    public Collider[] goalColliders;

    [Header("调试信息 | Debug Info")]
    [ReadOnly] public bool isGoalVisible = true;
    [ReadOnly] public bool isCylinderInside = false;
    [ReadOnly] public bool hasCylinderPassedThrough = false;
    [ReadOnly] public bool isCylinderAboveAndClear = false;
    [ReadOnly] public float currentHoldTime = 0f;

    // 私有变量
    private bool hasTriggered = false;
    private Coroutine holdCoroutine;
    private Vector3 goalCenter;
    private float goalTopY;
    private float goalBottomY;

    // 事件委托（可选）
    public System.Action OnGoalCompleted;

    private void Start()
    {
        InitializeComponents();
        CalculateGoalBounds();
        SetGoalVisibility(true);
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
            goalColliders = GetComponentsInChildren<Collider>();
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

            // ★ 轻量化处理：不调用 ResetCylinderState()（不会影响圆柱体物理/阈值），
            //   如果回到 Goal 下方，仅清理 Goal 的逻辑标志与计时，避免状态卡住。
            if (cylinderObject.transform.position.y < goalBottomY)
            {
                ResetGoalFlags(); // ★ 仅清理逻辑标志
                Debug.Log($"🎯 圆柱体回到 Goal 下方，保持物理状态，仅重置 Goal 逻辑标志: {gameObject.name}");
            }
        }
    }

    private void CheckCylinderStatus()
    {
        if (cylinderObject == null) return;

        // 当圆柱体被约束（例如在 Threshold 中）时暂停 Goal 检测
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

    // ★ 新增：仅重置 Goal 的逻辑标志与计时，不触碰圆柱体物理/阈值状态
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
    }

    // 保留原有的重置函数：先轻量化清理标志，再执行“阈值保护 + 物理检查”
    private void ResetCylinderState()
    {
        ResetGoalFlags(); // ★ 先清理标志，确保状态一致

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
                Debug.Log($"   - Rigidbody约束: {(cylinderRb != null ? cylinderRb.constraints.ToString() : "无")}");
                Debug.Log($"   - 位置: {pos}");
                Debug.Log($"   - ThresholdScript: {(thresholdScript != null ? "存在" : "不存在")}");
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
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
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
            TriggerGoalComplete();
        }

        holdCoroutine = null;
    }

    private void TriggerGoalComplete()
    {
        if (hasTriggered) return;

        hasTriggered = true;
        Debug.Log($"✅ Goal 完成！{gameObject.name} - 圆柱体保持 {holdDuration} 秒");

        PlaySuccessAudio();
        SetGoalVisibility(false);
        OnGoalCompleted?.Invoke();

        // 完成后是否需要“重置到初始态”，取决于你的流程：
        // 如果不想动圆柱体物理状态，可以只清理标志：
        ResetGoalFlags(); // ★ 替代原来的 ResetCylinderState()
        // 如需彻底重置，可改回 ResetCylinderState();
    }

    private void PlaySuccessAudio()
    {
        if (audioSource != null && successAudioClip != null)
        {
            audioSource.PlayOneShot(successAudioClip);
        }
        else
        {
            Debug.LogWarning($"⚠️ 无法播放音效 - AudioSource: {audioSource != null}, AudioClip: {successAudioClip != null}");
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

        if (goalColliders != null)
        {
            foreach (var collider in goalColliders)
            {
                if (collider != null && collider != GetComponent<Collider>())
                {
                    collider.enabled = visible;
                }
            }
        }
    }

    /// <summary>
    /// 由 CenterOfMassController 调用，用于在切换 CoM 时重置 Goal
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
            // 这里看需求选择轻量或完全重置；为了安全，仍采用轻量：
            ResetGoalFlags(); // ★ 只清理标志
        }
        else
        {
            // 保持原有保护逻辑
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

        SetGoalVisibility(true);
        Debug.Log($"🔁 Goal 已重置并重新出现: {gameObject.name}");
    }

    public void ManualReset()
    {
        // 手动重置也采用轻量逻辑，避免影响圆柱体物理状态
        ResetGoalFlags(); // ★
        SetGoalVisibility(true);
        hasTriggered = false;
        Debug.Log("🧹 手动重置：仅清理 Goal 逻辑标志并保持可见");
    }

    public bool IsCompleted()
    {
        return hasTriggered;
    }

    public float GetHoldProgress()
    {
        return Mathf.Clamp01(currentHoldTime / holdDuration);
    }
}
