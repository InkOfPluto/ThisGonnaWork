// ThresholdReopener.cs
using UnityEngine;

public class ThresholdReopener : MonoBehaviour
{
    [Header("References")]
    [Tooltip("被关闭/打开的 Threshold（挂有 ThresholdCloser 的那个对象）")]
    public GameObject threshold;

    [Tooltip("Cylinder 的 Transform")]
    public Transform cylinder;

    [Header("Settings")]
    [Tooltip("当 Cylinder 的世界坐标 Y < reopenHeight 时，重新打开 Threshold")]
    public float reopenHeight = 0.775f;

    // 内部状态：用于识别“刚被关闭”的事件（视为 ThresholdCloser 触发）
    private bool sawCloseEvent = false;
    private bool lastThresholdActive = true;

    private void Awake()
    {
        if (threshold != null)
            lastThresholdActive = threshold.activeSelf;
    }

    private void Update()
    {
        if (threshold == null || cylinder == null) return;

        // 侦测“关闭事件”：上帧是激活，这帧是未激活 → 认为是 ThresholdCloser 刚刚触发过
        if (lastThresholdActive && !threshold.activeSelf)
        {
            sawCloseEvent = true;
        }
        lastThresholdActive = threshold.activeSelf;

        // 如果已检测到关闭事件，则在高度条件满足时重新打开
        if (sawCloseEvent)
        {
            if (cylinder.position.y < reopenHeight)
            {
                threshold.SetActive(true);   // 重新打开 Threshold
                sawCloseEvent = false;       // 清空，等待下一次 ThresholdCloser 再次触发
                lastThresholdActive = true;  // 同步状态，避免误判
                Debug.Log($"[ThresholdReopener] Reopened because Cylinder.y={cylinder.position.y:F3} < {reopenHeight}");
            }
        }
    }
}
