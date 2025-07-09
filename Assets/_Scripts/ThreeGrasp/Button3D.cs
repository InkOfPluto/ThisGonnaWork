using UnityEngine;

public class Button3D : MonoBehaviour
{
    public string keyName = "Y"; // 自定义这个按钮的名称（显示用）
    public float rotationAngle = 10f; // 你要控制的手指旋转角度
    public int fingerIndex = 0; // 控制第几个手指
    public bool clockwise = true; // 是否顺时针

    public PincherController controller; // 拖入你的控制器脚本

    private void OnMouseDown()
    {
        float angle = clockwise ? rotationAngle : -rotationAngle;
        if (controller != null && fingerIndex < controller.fingers.Count)
        {
            controller.RotateFingerExternally(fingerIndex, angle);
            Debug.Log($"[✅] 虚拟按键 {keyName} 触发手指 {fingerIndex} 旋转 {angle} 度");
        }
        else
        {
            Debug.LogWarning("未绑定 PincherController 或手指索引非法");
        }
    }
}
