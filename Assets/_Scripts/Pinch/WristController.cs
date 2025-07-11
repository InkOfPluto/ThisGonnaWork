using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WristController : MonoBehaviour
{
    public GameObject wrist;           // 手腕对象
    public GameObject hand;            // 手对象

    private float previousY;           // 上一帧 wrist Y 值
    private float movementThreshold = 0.01f;  // 防抖阈值

    private float minY = 0.8f;         // hand 的最低高度
    private float maxY = 1.2f;         // hand 的最高高度

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

        BigHandState moveState = MoveStateForDelta(deltaY, currentY);

        GripperDemoController controller = hand.GetComponent<GripperDemoController>();
        if (controller != null)
        {
            controller.moveState = moveState;
        }

        // 限制 hand 的 Y 值在 minY 和 maxY 之间
        Vector3 handPos = hand.transform.position;
        handPos.y = Mathf.Clamp(handPos.y, minY, maxY);
        hand.transform.position = handPos;

        previousY = currentY;
    }

    BigHandState MoveStateForDelta(float deltaY, float currentY)
    {
        if (currentY < 0.8f)
        {
            return BigHandState.MovingDown;
        }
        else if (currentY > 1.4f)
        {
            return BigHandState.MovingUp;
        }
        else if (deltaY > movementThreshold)
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
