using UnityEngine;

public class CameraYTracker : MonoBehaviour
{
    public GameObject robot;   // 要控制的机器人对象
    public Transform wrist;    // 手部参考位置

    public float handMinY = 0.8f; // Y 最小值
    public float handMaxY = 1.3f; // Y 最大值

    private float previousHeight; // 上一帧 wrist 的 Y 值

    void Start()
    {
        if (wrist != null)
        {
            previousHeight = wrist.position.y;
        }
    }

    void Update()
    {
        if (robot == null || wrist == null) return;

        // 当前手腕 Y 值
        float currentHeight = wrist.position.y;

        // 计算 Y 高度的变化量
        float changeInHeight = currentHeight - previousHeight;

        // 更新 robot 的位置：只移动 Y 值
        Vector3 robotPos = robot.transform.position;
        float newY = Mathf.Clamp(robotPos.y + changeInHeight, handMinY, handMaxY);
        robot.transform.position = new Vector3(robotPos.x, newY, robotPos.z);
    }
}
