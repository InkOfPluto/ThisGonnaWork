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
    public AudioClip successAudioClip;
    public AudioClip failureAudioClip;
    public AudioSource audioSource;

    [Header("视觉效果 | Visual Effects")]
    [Tooltip("Goal 对象的渲染器组件")]
    public Renderer[] goalRenderers;

    [Tooltip("Goal 对象的碰撞器组件（包含自身触发器在内）")]
    public Collider[] goalColliders;

    [Header("成功后处理目标 | Targets After Success")]
    [Tooltip("成功后关闭这些对象（如：五个手指Cube与圆柱体）；切换 CoM 时重新开启。")]
    public GameObject[] objectsToAffectAfterSuccess;

    [Header("CoM 切换时复位 | Reset on CoM Switch")]
    public Vector3 cylinderResetPosition = new Vector3(0f, 0.75f, 0.3f);
    public float cubesResetHeight = 0.75f;
    public bool zeroCylinderVelocityOnReset = true;
    public bool zeroCubesVelocityOnReset = true;
    public GameObject[] fingerCubes;

    [Header("Alarm 设置 | Alarm Settings")]
    public GameObject alarmUI;
    public float tiltThresholdDegrees = 10f;
    public float alarmAutoCloseDelay = 1f;

    [Header("祝贺 UI | Congratulations UI")]
    public GameObject congratulationsUI;

    [Header("外部引用 | External Refs")]
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

    // —— 事件接口 ——
    public event Action<float> OnHoldSucceeded;
    public event Action<float> OnHoldInterrupted;
    public event Action<float, float> OnHoldFailedTilt;

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
        HideCongratulations();

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
        }
        else if (cylinderObject == null && other.CompareTag("Cylinder"))
        {
            cylinderObject = other.gameObject;
            isCylinderInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (cylinderObject != null && other.gameObject == cylinderObject)
        {
            if (isCylinderInside) hasCylinderPassedThrough = true;
            isCylinderInside = false;
            if (cylinderObject.transform.position.y < goalBottomY)
            {
                ResetGoalFlags();
            }
        }
    }

    private void CheckCylinderStatus()
    {
        if (cylinderObject == null) return;
        Vector3 cylinderPos = cylinderObject.transform.position;
        bool wasAboveAndClear = isCylinderAboveAndClear;
        isCylinderAboveAndClear = hasCylinderPassedThrough &&
                                  cylinderPos.y > goalTopY &&
                                  !isCylinderInside;
        if (isCylinderAboveAndClear && !wasAboveAndClear) StartHoldTimer();
        else if (!isCylinderAboveAndClear && wasAboveAndClear) StopHoldTimer();
    }

    private void ResetGoalFlags()
    {
        hasCylinderPassedThrough = false;
        isCylinderAboveAndClear = false;
        isCylinderInside = false;
        currentHoldTime = 0f;
        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        holdCoroutine = null;
        HideAlarm();
    }

    private void StartHoldTimer()
    {
        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        holdCoroutine = StartCoroutine(HoldTimerCoroutine());
    }

    private void StopHoldTimer()
    {
        bool hadTimer = (holdCoroutine != null);
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
        if (hadTimer && currentHoldTime > 0f && currentHoldTime < holdDuration)
        {
            OnHoldInterrupted?.Invoke(currentHoldTime);
        }
        currentHoldTime = 0f;
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
            float angle = 0f;
            bool tiltTooLarge = false;
            if (cylinderObject != null)
            {
                angle = Vector3.Angle(cylinderObject.transform.up, Vector3.up);
                tiltTooLarge = angle > tiltThresholdDegrees;
            }
            if (tiltTooLarge) OnHoldFailedTilt?.Invoke(currentHoldTime, angle);
            else FireHoldSucceeded(currentHoldTime);
        }
        holdCoroutine = null;
    }

    private void FireHoldSucceeded(float duration)
    {
        if (hasTriggered) return;
        hasTriggered = true;
        OnHoldSucceeded?.Invoke(duration);
    }

    private void PlaySuccessAudio()
    {
        if (audioSource != null && successAudioClip != null)
            audioSource.PlayOneShot(successAudioClip);
    }

    private void PlayFailureAudio()
    {
        if (audioSource != null && failureAudioClip != null)
            audioSource.PlayOneShot(failureAudioClip);
    }

    private void SetGoalVisibility(bool visible)
    {
        isGoalVisible = visible;
        if (goalRenderers != null)
        {
            foreach (var renderer in goalRenderers)
                if (renderer != null) renderer.enabled = visible;
        }
        if (goalColliders != null && goalColliders.Length > 0)
        {
            foreach (var c in goalColliders)
                if (c != null) c.enabled = visible;
        }
        else
        {
            var selfCol = GetComponent<Collider>();
            if (selfCol) selfCol.enabled = visible;
        }
    }

    public void ResetGoalOnNextCoM()
    {
        hasTriggered = false;
        ResetGoalFlags();
        SetGoalVisibility(true);
        HideAlarm();
        HideCongratulations();
        SetObjectsActive(objectsToAffectAfterSuccess, true);
        ResetTransformsOnCoMSwitch();
        if (graspHandTracking != null) graspHandTracking.ResumeAttemptCounting();
    }

    public void ManualReset()
    {
        ResetGoalFlags();
        SetGoalVisibility(true);
        hasTriggered = false;
        HideAlarm();
        HideCongratulations();
        SetObjectsActive(objectsToAffectAfterSuccess, true);
    }

    private void ShowAlarm()
    {
        if (alarmUI != null) alarmUI.SetActive(true);
        alarmActive = true;
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

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var obj in objects)
            if (obj != null) obj.SetActive(active);
    }

    private void ResetTransformsOnCoMSwitch()
    {
        if (cylinderObject != null)
        {
            var rb = cylinderObject.GetComponent<Rigidbody>();
            cylinderObject.transform.position = cylinderResetPosition;
            if (rb != null && zeroCylinderVelocityOnReset)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }
        }
        if (fingerCubes != null)
        {
            foreach (var cube in fingerCubes)
            {
                if (cube == null) continue;
                var p = cube.transform.position;
                var targetPos = new Vector3(p.x, cubesResetHeight, p.z);
                var ab = cube.GetComponent<ArticulationBody>();
                if (ab != null)
                {
                    if (ab.isRoot)
                    {
                        ab.TeleportRoot(targetPos, cube.transform.rotation);
                    }
                    else
                    {
                        cube.transform.position = targetPos;
                    }

                    if (zeroCubesVelocityOnReset)
                    {
                        ab.velocity = Vector3.zero;
                        ab.angularVelocity = Vector3.zero;
                    }
                }
                else
                {
                    cube.transform.position = targetPos;

                    var rb = cube.GetComponent<Rigidbody>();
                    if (rb != null && zeroCubesVelocityOnReset)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.Sleep();
                    }
                }
            }
        }
    }

    private void Default_OnHoldSucceeded(float duration)
    {
        ShowCongratulations();
        if (graspHandTracking != null) graspHandTracking.StopAttemptCounting();
        PlaySuccessAudio();
        SetGoalVisibility(false);
        SetObjectsActive(objectsToAffectAfterSuccess, false);
        ResetGoalFlags();
    }

    private void Default_OnHoldFailedTilt(float duration, float angleDeg)
    {
        ShowAlarm();
        PlayFailureAudio();
    }

    private void Default_OnHoldInterrupted(float elapsed)
    {
        Debug.Log($"HoldInterrupted: {elapsed:F2}s / {holdDuration:F2}s");
    }

    private void OnCylinderTouchTableHandler()
    {
        if (alarmActive && alarmUI != null)
            StartCoroutine(CloseAlarmAfterDelay(alarmAutoCloseDelay));
    }

    private IEnumerator CloseAlarmAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideAlarm();
    }
}
