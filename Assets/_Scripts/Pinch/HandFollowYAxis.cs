using UnityEngine;

public class CameraYTracker : MonoBehaviour
{
    public Transform cameraTransform;   // 拖入主摄像头（Main Camera）

    float initialCameraY;        // 摄像头初始 Y 值（只记录一次）

    public GameObject hand;             // 要控制的物体
    public Transform trackingTarget;    // 手部跟踪参考对象

    // 限制 hand 的 Y 值范围
    public float handMinY = 0.8f;
    public float handMaxY = 1.3f;

    void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform; // 自动获取 Main Camera
        }

        initialCameraY = cameraTransform.position.y;

        //Debug.Log("[📷] 初始摄像头 Y 值: " + initialCameraY.ToString("F5"));
    }

    void Update()
    {
        Vector3 cameraPos = Camera.main.transform.position;
        float cameraY = cameraPos.y;

        //Debug.Log("📷 摄像头当前 Y 坐标：" + cameraY.ToString("F5"));

        if (hand == null || trackingTarget == null) return;

        float trackingY = trackingTarget.position.y;

        // ✅ 计算目标 hand Y 坐标（考虑摄像头偏移）
        float offsetY = trackingY - (cameraY - initialCameraY);
        float targetY = Mathf.Clamp(offsetY, handMinY, handMaxY);

        // ✅ 只有当 targetY 在 hand 有效范围内时才移动
        if (targetY >= handMinY && targetY <= handMaxY)
        {
            Vector3 handPos = hand.transform.position;
            handPos.y = targetY;
            hand.transform.position = handPos;
        }
    }
}