using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最终版 WristController，加入 Deadzone + Snap 抑制 hand 抖动。
/// </summary>
public class UpDown_HandTracking : MonoBehaviour
{
    public GameObject wrist;   // 手腕对象
    public GameObject hand;    // 手对象

    public float handMoveSpeed = 0.5f;        // hand 平滑移动速度
    public float handDeadZone = 0.002f;       // 最小移动阈值，抖动容忍（推荐0.001~0.005）
    public float handSnapThreshold = 0.0005f; // 贴合阈值（更小）

    private float previousY;
    private float movementThreshold = 0.01f;  // 状态判断阈值
    private float accumulatedDeltaY = 0f;

    private float minY = 0.8f;
    private float maxY = 1.2f;

    void Start()
    {
        if (wrist != null)
        {
            previousY = wrist.transform.position.y;
        }
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

        // 抖动抑制逻辑
        Vector3 handPos = hand.transform.position;
        float targetY = Mathf.Clamp(currentY, minY, maxY);
        float diffY = targetY - handPos.y;

        if (Mathf.Abs(diffY) > handDeadZone)
        {
            // 插值移动
            handPos.y = Mathf.MoveTowards(handPos.y, targetY, handMoveSpeed * Time.deltaTime);
            hand.transform.position = handPos;
        }
        else if (Mathf.Abs(diffY) > handSnapThreshold)
        {
            // 已非常接近目标，但还没完全贴上 —— 直接贴合避免残抖
            handPos.y = targetY;
            hand.transform.position = handPos;
        }
        // 否则不动，避免抖动

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
}
