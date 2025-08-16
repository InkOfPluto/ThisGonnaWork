using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最终版 WristController，加入 Deadzone + Snap 抑制 hand 抖动。
/// 启动时，将 5 个手指 Cube 高度与圆柱体中心对齐；
/// 提供“同步锁定模式”在运行时保持一致高度。
/// </summary>
public class UpDown_HandTracking : MonoBehaviour
{
    [Header("Scene Refs")]
    public GameObject wrist;          // 手腕对象
    public GameObject hand;           // 手对象
    public GameObject[] fingers;      // 五个手指 Cube
    public GameObject cylinder;       // 圆柱体对象（用于获取中心高度）

    [Header("Hand Damping")]
    public float handMoveSpeed = 0.5f;        // hand 平滑移动速度
    public float handDeadZone = 0.002f;       // 最小移动阈值，抖动容忍（推荐0.001~0.005）
    public float handSnapThreshold = 0.0005f; // 贴合阈值（更小）

    [Header("Move State")]
    [SerializeField] private float movementThreshold = 0.01f;  // 状态判断阈值
    private float previousY;
    private float accumulatedDeltaY = 0f;

    [Header("Hand Y Clamp")]
    public float minY = 0.8f;
    public float maxY = 1.2f;

    [Header("Lock Sync (Fingers ↔ Cylinder)")]
    public bool lockSync = false;           // 是否开启运行时同步
    [Tooltip("=0: 立刻对齐；>0: 以该速度插值对齐")]
    public float lockSyncSpeed = 0f;        // 同步平滑速度（0 为瞬间对齐）

    void Start()
    {
        if (wrist != null)
        {
            previousY = wrist.transform.position.y;
        }

        // 启动时，将手指高度与圆柱体中心对齐
        AlignFingersToCylinderInstant();
    }

    void Update()
    {
        if (wrist == null || hand == null) return;

        float currentY = wrist.transform.position.y;
        float deltaY = currentY - previousY;
        accumulatedDeltaY += deltaY;

        // 判断移动状态
        BigHandState moveState = MoveStateForDelta(accumulatedDeltaY);
        if (Mathf.Abs(accumulatedDeltaY) >= movementThreshold)
        {
            accumulatedDeltaY = 0f;
        }

        // 传状态给控制器
        GripperDemoController controller = hand.GetComponent<GripperDemoController>();
        if (controller != null)
        {
            controller.moveState = moveState;
        }

        // hand 抖动抑制与贴合
        Vector3 handPos = hand.transform.position;
        float targetY = Mathf.Clamp(currentY, minY, maxY);
        float diffY = targetY - handPos.y;

        if (Mathf.Abs(diffY) > handDeadZone)
        {
            // 这里保留你的平滑滤波逻辑
            handPos.y = Mathf.Lerp(handPos.y, targetY, handMoveSpeed * Time.deltaTime);
            hand.transform.position = handPos;
        }
        else if (Mathf.Abs(diffY) > handSnapThreshold)
        {
            // 已非常接近目标，直接贴合
            handPos.y = targetY;
            hand.transform.position = handPos;
        }
        // 否则不动，避免抖动

        // —— 同步锁定模式：让手指在运行时保持与 Cylinder 同高 ——
        if (lockSync)
        {
            SyncFingersToCylinder(lockSyncSpeed);
        }

        previousY = currentY;
    }

    BigHandState MoveStateForDelta(float deltaY)
    {
        if (deltaY > movementThreshold)
        {
            return BigHandState.MovingUp;
        }
        else if (deltaY < -movementThreshold)
        {
            return BigHandState.MovingDown;
        }
        else
        {
            return BigHandState.Fixed;
        }
    }

    /// <summary>
    /// 启动瞬间对齐（不插值）
    /// </summary>
    private void AlignFingersToCylinderInstant()
    {
        if (cylinder == null || fingers == null || fingers.Length == 0) return;

        float centerY = cylinder.transform.position.y;
        for (int i = 0; i < fingers.Length; i++)
        {
            var f = fingers[i];
            if (f == null) continue;
            Vector3 pos = f.transform.position;
            pos.y = centerY;
            f.transform.position = pos;
        }
    }

    /// <summary>
    /// 运行时同步：speed == 0 则瞬间对齐；否则用 Lerp 插值
    /// </summary>
    private void SyncFingersToCylinder(float speed)
    {
        if (cylinder == null || fingers == null || fingers.Length == 0) return;

        float centerY = cylinder.transform.position.y;

        for (int i = 0; i < fingers.Length; i++)
        {
            GameObject f = fingers[i];
            if (f == null) continue;

            Vector3 pos = f.transform.position;

            if (speed <= 0f)
            {
                pos.y = centerY; // 立刻对齐
            }
            else
            {
                pos.y = Mathf.Lerp(pos.y, centerY, speed * Time.deltaTime); // 平滑跟随
            }

            f.transform.position = pos;
        }
    }
}
