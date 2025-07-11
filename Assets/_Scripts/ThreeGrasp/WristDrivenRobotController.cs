using UnityEngine;

public class WristDrivenRobotController : MonoBehaviour
{
    public GameObject wrist;  // 手腕对象
    public GameObject hand;   // 手对象

    void Update()
    {
        if (wrist != null && hand != null)
        {
            Vector3 handPos = hand.transform.position;
            float targetY = wrist.transform.position.y;

            // 限制 hand 的 Y 值在 0.8f 到 1.3f 之间
            targetY = Mathf.Clamp(targetY, 0.8f, 1.3f);

            hand.transform.position = new Vector3(
                handPos.x,      // 保持 X 不变
                targetY,        // 设置限制后的 Y
                handPos.z       // 保持 Z 不变
            );
        }
    }
}