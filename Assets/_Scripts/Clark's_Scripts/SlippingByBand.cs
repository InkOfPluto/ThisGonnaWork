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

    [Header("Non-uniform Binning | 非均匀分段")]
    [Tooltip("非均匀分段的幂次指数 (0<beta<1)，越小则低值区间越窄越敏感")]
    [Range(0.2f, 0.9f)]
    public float beta = 0.5f;

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

    // 🔧 新增：预计算的PWM区间值和阈值
    private int[] pwmBands;
    private float[] inputThresholds;

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
        
        // 确保beta在合理范围内
        beta = Mathf.Clamp(beta, 0.2f, 0.9f);
        
        // 重新计算PWM区间和阈值
        CalculateNonUniformBands();
    }

    void Start()
    {
        // 计算非均匀PWM区间值和阈值
        CalculateNonUniformBands();
        
        OpenSerialPort(ref serial, portName, baudRate);
        Debug.Log($"🟢 SlippingByBand ready. Non-uniform band-based mapping with {numberOfBands} discrete PWM levels (beta={beta:F2}). Single-port protocol: t/i/m/r/p±NNN; 's' for stop. Saturates to ±255 when |value| > 0.09.");
    }

    // 🔧 修改：计算非均匀PWM区间值和输入阈值
    private void CalculateNonUniformBands()
    {
        pwmBands = new int[numberOfBands];
        inputThresholds = new float[numberOfBands + 1];
        
        // 计算输入阈值（非均匀分段）
        for (int k = 0; k <= numberOfBands; k++)
        {
            // 在变换空间中等分 [0,1]
            float y = (float)k / numberOfBands;
            
            // 幂次反变换：y = x^beta => x = y^(1/beta)
            float x = Mathf.Pow(y, 1.0f / beta);
            
            // 映射回原输入范围
            inputThresholds[k] = inputMin + (inputMax - inputMin) * x;
        }
        
        // 计算每个区间对应的PWM值（保持原有的gamma曲线）
        for (int i = 0; i < numberOfBands; i++)
        {
            float t = (float)i / (numberOfBands - 1);
            t = Mathf.Pow(t, gamma);
            int pwmValue = Mathf.RoundToInt(Mathf.Lerp(pwmMin, pwmMax, t));
            pwmBands[i] = Mathf.Clamp(pwmValue, pwmMin, pwmMax);
        }
        
        // 调试输出
        string thresholdStr = string.Join(", ", System.Array.ConvertAll(inputThresholds, x => x.ToString("F6")));
        string pwmStr = string.Join(", ", pwmBands);
        Debug.Log($"🎛️ Non-uniform input thresholds (beta={beta:F2}): [{thresholdStr}]");
        Debug.Log($"🎛️ PWM Bands: [{pwmStr}]");
        
        // 显示分段区间宽度（用于验证非均匀性）
        for (int i = 0; i < numberOfBands; i++)
        {
            float width = inputThresholds[i + 1] - inputThresholds[i];
            Debug.Log($"   Band {i}: [{inputThresholds[i]:F6}, {inputThresholds[i + 1]:F6}) → PWM {pwmBands[i]}, width={width:F6}");
        }
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

    // 🔧 修改：基于非均匀分段的速度映射方法
    private int MapValueToBandedSpeed(float absValue)
    {
        if (inputThresholds == null || pwmBands == null || pwmBands.Length == 0)
        {
            CalculateNonUniformBands();
        }
        
        // 边界处理
        if (absValue <= inputThresholds[0]) return pwmBands[0];
        if (absValue >= inputThresholds[numberOfBands]) return pwmBands[numberOfBands - 1];
        
        // 查找输入值所属的区间
        for (int i = 0; i < numberOfBands; i++)
        {
            if (absValue < inputThresholds[i + 1])
            {
                return pwmBands[i];
            }
        }
        
        // 默认返回最后一个区间的PWM值
        return pwmBands[numberOfBands - 1];
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
            // 🔧 修改：使用基于非均匀分段的映射
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