using UnityEngine;
using System.IO.Ports;
using System;
using System.Collections.Generic;

public class SlippingByBand: MonoBehaviour
{
    [Header("Reference to VisualDisplay")]
    public VisualDisplay visual;

    [Header("Serial Port (Single)")]
    public string portName = "COM11";
    public int baudRate = 115200;
    private SerialPort serial;

    [Header("Control Settings")]
    [Tooltip("同一手指两次发送之间的冷却秒数，防止刷屏")]
    public float cooldownTime = 0.04f;

    [Tooltip("全局映射阈值：|value| < inputMin 不发；|value| ∈ [inputMin, inputMax] → PWM ∈ [pwmMin,pwmMax]")]
    public float inputMin = 0.0005f;   // 死区 & 映射起点
    public float inputMax = 0.13f;     // 默认上限 0.11；但 >0.09 直接 ±255

    [Tooltip("低端顺滑指数（>1 更柔和）")]
    public float gamma = 0.8f;

    [Tooltip("最小变化量（PWM 级），小于该差值则不重发")]
    public int minDeltaPwm = 8;

    [Header("PWM Range (Inspector 可调)")]
    [Range(0, 255)]
    public int pwmMin = 60;
    [Range(0, 255)]
    public int pwmMax = 255;

    [Header("Band Mapping Settings | 区间映射设置")]
    [Tooltip("PWM 区间数量，将 pwmMin 到 pwmMax 分成多少个离散级别")]
    [Range(3, 12)]
    public int numberOfBands = 3;

    [Header("Height Limit")]
    [Tooltip("cubetouching 高度低于此值时不向马达输入数据")]
    public float minHeight = 0.8f;

    [Header("Cubetouching Reference")]
    [Tooltip("用于检测高度的 cubetouching 物体")]
    public Transform cubetouching;

    // 各手指独立冷却计时
    private float thumbCooldown = 0f, indexCooldown = 0f, middleCooldown = 0f, ringCooldown = 0f, pinkyCooldown = 0f;

    // 上次发送的带符号速度（用于防抖）
    private readonly Dictionary<string, int> lastSent = new Dictionary<string, int> {
        {"t", 0}, {"i", 0}, {"m", 0}, {"r", 0}, {"p", 0}
    };

    private float lastSlipEventTime = 0f;   // 最近一次有"有效输出"的时间戳（秒）
    private bool anyFingerMoving = false;   // 是否有任何手指在运动

    // 固定硬阈值：超过即直接饱和到 ±255
    private const float HARD_SAT_CUTOFF = 0.09f;

    // 🔧 新增：预计算的PWM区间值
    private int[] pwmBands;

    public float[] GetMotorSpeeds5()
    {
        return new float[5] {
            lastSent["t"], lastSent["i"], lastSent["m"], lastSent["r"], lastSent["p"]
        };
    }

    public float GetSlipProcessTime()
    {
        return Time.time - lastSlipEventTime;
    }

    void OnValidate()
    {
        pwmMin = Mathf.Clamp(pwmMin, 0, 255);
        pwmMax = Mathf.Clamp(pwmMax, 0, 255);
        if (pwmMin > pwmMax)
        {
            int tmp = pwmMin;
            pwmMin = pwmMax;
            pwmMax = tmp;
        }
        int span = Mathf.Max(1, pwmMax - pwmMin);
        minDeltaPwm = Mathf.Clamp(minDeltaPwm, 1, span);

        if (inputMax < inputMin)
        {
            // 防止写反
            float t = inputMin;
            inputMin = inputMax;
            inputMax = t;
        }

        // 确保区间数量合理
        numberOfBands = Mathf.Clamp(numberOfBands, 3, 12);
        
        // 重新计算PWM区间
        CalculatePWMBands();
    }

    void Start()
    {
        // 计算PWM区间值
        CalculatePWMBands();
        
        OpenSerialPort(ref serial, portName, baudRate);
        Debug.Log($"🟢 SlippingByBand ready. Band-based mapping with {numberOfBands} discrete PWM levels. Single-port protocol: t/i/m/r/p±NNN; 's' for stop. Saturates to ±255 when |value| > 0.09.");
    }

    // 🔧 新增：计算PWM区间值
    private void CalculatePWMBands()
    {
        pwmBands = new int[numberOfBands];
        
        for (int i = 0; i < numberOfBands; i++)
        {
            // 计算每个区间对应的标准化位置
            float t = (float)i / (numberOfBands - 1);
            
            // 应用 gamma 曲线
            t = Mathf.Pow(t, gamma);
            
            // 映射到 PWM 范围
            int pwmValue = Mathf.RoundToInt(Mathf.Lerp(pwmMin, pwmMax, t));
            pwmBands[i] = Mathf.Clamp(pwmValue, pwmMin, pwmMax);
        }
        
        Debug.Log($"🎛️ PWM Bands calculated: {string.Join(", ", pwmBands)}");
    }

    void Update()
    {
        if (visual == null) return;

        float now = Time.time;

        // === 高度检测 ===
        if (cubetouching != null && cubetouching.position.y < minHeight)
        {
            // 如果低于阈值且之前有手指在动，发送停止指令
            if (anyFingerMoving)
            {
                StopMotors();
                anyFingerMoving = false;
            }
            return;
        }

        var tokens = new List<string>(5);
        bool needStop = false;

        // 检查每个手指
        needStop |= CheckFinger(tokens, visual.DistanceDA, ref thumbCooldown, now, "t", "Thumb");
        needStop |= CheckFinger(tokens, visual.DistanceSHI, ref indexCooldown, now, "i", "Index");
        needStop |= CheckFinger(tokens, visual.DistanceZHONG, ref middleCooldown, now, "m", "Middle");
        needStop |= CheckFinger(tokens, visual.DistanceWU, ref ringCooldown, now, "r", "Ring");
        needStop |= CheckFinger(tokens, visual.DistanceXIAO, ref pinkyCooldown, now, "p", "Pinky");

        // 如果有手指需要停止，且所有手指都已停止，发送全局停止指令
        if (needStop && AllFingersStoppedOrInDeadzone())
        {
            StopMotors();
            anyFingerMoving = false;
        }
        else if (tokens.Count > 0)
        {
            // 有新的运动指令
            anyFingerMoving = true;
            lastSlipEventTime = Time.time;
            SendLine(string.Join(",", tokens) + ",\n");
        }

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            StopMotors();
            anyFingerMoving = false;
        }
    }

    // 🔧 新增：基于区间的速度映射方法
    private int MapValueToBandedSpeed(float absValue)
    {
        if (pwmBands == null || pwmBands.Length == 0)
        {
            CalculatePWMBands();
        }
        
        // 将输入值映射到区间索引
        float range = inputMax - inputMin;
        float normalizedValue = (absValue - inputMin) / range;
        normalizedValue = Mathf.Clamp01(normalizedValue);
        
        // 计算区间索引
        int bandIndex = Mathf.FloorToInt(normalizedValue * numberOfBands);
        bandIndex = Mathf.Clamp(bandIndex, 0, numberOfBands - 1);
        
        // 返回对应区间的PWM值
        return pwmBands[bandIndex];
    }

    // 检查手指状态，返回是否需要停止
    bool CheckFinger(List<string> tokens, float value, ref float cooldownTimer, float now, string prefix, string fingerName)
    {
        if (now - cooldownTimer < cooldownTime) return false;

        float a = Mathf.Abs(value);
        int previousValue = lastSent[prefix];  // 在方法开始时获取之前的值

        // 当值小于死区时
        if (a < inputMin)
        {
            if (previousValue != 0)  // 如果之前有速度，标记需要停止
            {
                lastSent[prefix] = 0;
                cooldownTimer = now;
                Debug.Log($"🔻 {fingerName} entered deadzone (was {previousValue})");
                return true;  // 返回需要停止
            }
            return false;
        }

        int signed;

        // === 超过硬阈值(0.09)时直接饱和到 ±255 ===
        if (a > HARD_SAT_CUTOFF)
        {
            signed = (value >= 0f) ? 255 : -255;
        }
        else
        {
            // 🔧 修改：使用基于区间的映射替代连续映射
            int mag = MapValueToBandedSpeed(a);
            signed = (value >= 0f) ? mag : -mag;
        }

        if (Mathf.Abs(signed - previousValue) < minDeltaPwm) return false;

        lastSent[prefix] = signed;
        tokens.Add($"{prefix}{signed}");
        cooldownTimer = now;

        return false;
    }

    // 检查是否所有手指都已停止或在死区
    bool AllFingersStoppedOrInDeadzone()
    {
        return lastSent["t"] == 0 &&
               lastSent["i"] == 0 &&
               lastSent["m"] == 0 &&
               lastSent["r"] == 0 &&
               lastSent["p"] == 0;
    }

    void OpenSerialPort(ref SerialPort port, string name, int baud)
    {
        try
        {
            if (port != null && port.IsOpen) port.Close();
            port = new SerialPort(name, baud) { ReadTimeout = 50, NewLine = "\n" };
            port.Open();
            Debug.Log($"✅ Serial opened: {name}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"❌ Failed to open {name}: {e.Message}");
        }
    }

    void SendLine(string line)
    {
        if (serial == null || !serial.IsOpen)
        {
            Debug.LogWarning("⚠️ Serial not open. Reopening...");
            OpenSerialPort(ref serial, portName, baudRate);
        }

        if (serial != null && serial.IsOpen)
        {
            try
            {
                serial.Write(line);
                Debug.Log("📤 Sent: " + line.Trim());
            }
            catch (Exception e)
            {
                Debug.LogWarning("⚠️ Send failed: " + line.Trim() + " → " + e.Message);
            }
        }
        else
        {
            Debug.LogError("❌ Serial still not open, cannot send.");
        }
    }

    void OnApplicationQuit()
    {
        if (serial != null && serial.IsOpen) serial.Close();
    }

    // ======= 提供给事件调用的方法 =======
    public void StopMotors()
    {
        SendLine("s\n");
        Debug.Log("🛑 Stop command sent → 's'");

        // 重置所有手指 PWM 记录
        var keys = new List<string>(lastSent.Keys);
        foreach (var key in keys) lastSent[key] = 0;
    }

    // 可选的回调，方便调试或计数
    public void OnContactEnterEvent(Collider other)
    {
        Debug.Log($"🔗 Enter event from: {other.name}");
    }

    public void OnContactExitEvent(Collider other)
    {
        Debug.Log($"🔗 Exit event from: {other.name}");

        // 当物体退出碰撞时，也可以在这里调用停止
        if (AllFingersStoppedOrInDeadzone())
        {
            StopMotors();
            anyFingerMoving = false;
        }
    }

    // ===== 编辑器停止播放 / 脚本被禁用时的急停 =====
    void OnDisable()
    {
        StopMotors();
        Debug.Log("🛑 OnDisable triggered → global stop.");
    }
}
