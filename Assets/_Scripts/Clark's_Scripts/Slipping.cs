using UnityEngine;
using System.IO.Ports;
using System;

public class Slipping : MonoBehaviour
{
    [Header("Reference to VisualDisplay")]
    public VisualDisplay visual;

    private SerialPort serial1; // COM5 → 中指 + 无名指
    private SerialPort serial2; // COM6 → 拇指 + 食指

    private bool isRunning = false;

    [Header("Cooldown Settings")]
    public float cooldownTime = 0.5f;
    private float thumbCooldown = 0f, indexCooldown = 0f, middleCooldown = 0f, ringCooldown = 0f;

    void Start()
    {
        OpenSerialPort(ref serial1, "COM5");
        OpenSerialPort(ref serial2, "COM6");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            isRunning = true;
            Debug.Log("🟢 Haptic feedback started.");
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            isRunning = false;
            Debug.Log("🔴 Haptic feedback stopped.");
        }

        if (!isRunning || visual == null) return;

        float now = Time.time;

        // 分别对每根手指调用封装函数
        SendMapped(serial2, visual.DistanceDA, ref thumbCooldown, now, "t", "Thumb");       // 拇指 → COM6
        SendMapped(serial2, visual.DistanceSHI, ref indexCooldown, now, "i", "Index");      // 食指 → COM6
        SendMapped(serial1, visual.DistanceZHONG, ref middleCooldown, now, "m", "Middle");  // 中指 → COM5
        SendMapped(serial1, visual.DistanceWU, ref ringCooldown, now, "r", "Ring");         // 无名指 → COM5
    }

    /// <summary>
    /// 将滑动值映射后输出 指令+B 和 g/n（方向）
    /// </summary>
    void SendMapped(SerialPort port, float value, ref float cooldownTimer, float now, string prefix, string fingerName)
    {
        if (now - cooldownTimer < cooldownTime) return;

        float absVal = Mathf.Abs(value);
        if (absVal <= 0.0001f) return; // 滑动太小，忽略

        // 映射范围：0.0001 ~ 0.013 → 100 ~ 255
        int B = Mathf.RoundToInt(Mathf.Lerp(100f, 255f, Mathf.InverseLerp(0.0001f, 0.013f, absVal)));
        B = Mathf.Clamp(B, 100, 255);

        try
        {
            port.Write(prefix + B.ToString());
            port.Write("\n");
            Debug.Log($"✅ [{fingerName}] Power Sent: {prefix}{B}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ [{fingerName}] Failed to write {prefix}{B}: {e.Message}");
        }

        try
        {
            string dir = value > 0 ? "g" : "n";
            port.Write(dir);
            port.Write("\n");
            Debug.Log($"➡️ [{fingerName}] Direction Sent: {dir}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"⚠️ [{fingerName}] Failed to write direction: {e.Message}");
        }

        cooldownTimer = now;
    }

    /// <summary>
    /// 串口初始化封装
    /// </summary>
    void OpenSerialPort(ref SerialPort port, string portName)
    {
        try
        {
            port = new SerialPort(portName, 115200);
            port.ReadTimeout = 50;
            port.Open();
            Debug.Log($"✅ Serial ({portName}) opened.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"❌ Failed to open {portName}: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        if (serial1 != null && serial1.IsOpen) serial1.Close();
        if (serial2 != null && serial2.IsOpen) serial2.Close();
    }
}
