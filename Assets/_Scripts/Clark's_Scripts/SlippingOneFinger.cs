using UnityEngine;
using System.IO.Ports;
using System;
using System.Collections.Generic;

public class SlippingOneFinger : MonoBehaviour
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

    [Tooltip("全局映射阈值：|value| < inputMin 不发；|value| ∈ [inputMin, inputMax] → 分段PWM")]
    public float inputMin = 0.0005f;   // 死区 & 映射起点
    public float inputMax = 0.11f;     // 默认上限；> 硬阈值则直接 ±255

    [Tooltip("低端顺滑指数（>1 更柔和），用于生成分段PWM级别")]
    public float gamma = 1.2f;

    [Tooltip("最小变化量（PWM 级），小于该差值则不重发")]
    public int minDeltaPwm = 8;

    [Header("PWM Range (Inspector 可调)")]
    [Range(0, 255)]
    public int pwmMin = 100;
    [Range(0, 255)]
    public int pwmMax = 255;

    [Header("Band Mapping Settings | 区间映射设置")]
    [Tooltip("PWM 区间数量，将 pwmMin 到 pwmMax 分成多少个离散级别")]
    [Range(3, 12)]
    public int numberOfBands = 3;

    [Header("Non-uniform Binning | 非均匀分段")]
    [Tooltip("非均匀分段幂次 (0.2~0.9)，越小低值更细/更敏感")]
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

    [Header("Constant Threshold | 固定阈值")]
    // 固定硬阈值：超过即直接饱和到 ±255
    public const float HARD_SAT_CUTOFF = 0.11f;

    // ⬇️ 预计算的分段数据
    private int[] pwmBands;            // 每个分段对应的 PWM
    private float[] inputThresholds;   // 分段阈值（长度 = numberOfBands + 1）

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

        numberOfBands = Mathf.Clamp(numberOfBands, 3, 12);
        beta = Mathf.Clamp(beta, 0.2f, 0.9f);

        CalculateNonUniformBands();
    }

    void Start()
    {
        CalculateNonUniformBands();
        OpenSerialPort(ref serial, portName, baudRate);
        Debug.Log($"🟢 SlippingOneFinger ready. Non-uniform band mapping (bands={numberOfBands}, beta={beta:F2}), single-port protocol t/i/m/r/p±NNN; 's' to stop. Only the most-negative finger is driven.");
    }

    // 计算非均匀输入阈值 + 分段PWM
    private void CalculateNonUniformBands()
    {
        if (numberOfBands < 3) numberOfBands = 3;

        pwmBands = new int[numberOfBands];
        inputThresholds = new float[numberOfBands + 1];

        // 输入阈值：在 y∈[0,1] 等分，x = y^(1/beta) 做反变换，再映射回 [inputMin, inputMax]
        for (int k = 0; k <= numberOfBands; k++)
        {
            float y = (float)k / numberOfBands;
            float x = Mathf.Pow(y, 1.0f / beta);
            inputThresholds[k] = inputMin + (inputMax - inputMin) * x;
        }

        // 每段的 PWM：沿 gamma 曲线从 pwmMin 到 pwmMax
        for (int i = 0; i < numberOfBands; i++)
        {
            float t = (numberOfBands == 1) ? 1f : (float)i / (numberOfBands - 1);
            t = Mathf.Pow(t, gamma);
            int pwmValue = Mathf.RoundToInt(Mathf.Lerp(pwmMin, pwmMax, t));
            pwmBands[i] = Mathf.Clamp(pwmValue, pwmMin, pwmMax);
        }

        // 调试信息
        string thresholdStr = string.Join(", ", Array.ConvertAll(inputThresholds, v => v.ToString("F6")));
        string pwmStr = string.Join(", ", pwmBands);
        Debug.Log($"🎛️ Input thresholds (beta={beta:F2}): [{thresholdStr}]");
        Debug.Log($"🎛️ PWM Bands (gamma={gamma:F2}): [{pwmStr}]");

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

        // 获取所有距离值
        float[] distances = {
            visual.DistanceDA,
            visual.DistanceSHI,
            visual.DistanceZHONG,
            visual.DistanceWU,
            visual.DistanceXIAO
        };

        string[] prefixes = { "t", "i", "m", "r", "p" };
        string[] fingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        // 找到最大负值（即最小值，如果存在负值）
        int maxNegativeIndex = -1;
        float maxNegativeValue = 0f; // 只考虑负值
        for (int i = 0; i < distances.Length; i++)
        {
            if (distances[i] < maxNegativeValue)
            {
                maxNegativeValue = distances[i];
                maxNegativeIndex = i;
            }
        }

        var tokens = new List<string>(1); // 最多只有一个马达会被激活
        bool needStop = true; // 默认需要停止所有马达

        // 仅处理最负的那一根手指（若存在负值）
        if (maxNegativeIndex >= 0)
        {
            float[] cooldowns = { thumbCooldown, indexCooldown, middleCooldown, ringCooldown, pinkyCooldown };

            bool fingerProcessed = CheckSingleFinger(
                tokens,
                distances[maxNegativeIndex],
                ref cooldowns[maxNegativeIndex],
                now,
                prefixes[maxNegativeIndex],
                fingerNames[maxNegativeIndex]
            );

            // 更新冷却时间
            thumbCooldown = cooldowns[0];
            indexCooldown = cooldowns[1];
            middleCooldown = cooldowns[2];
            ringCooldown = cooldowns[3];
            pinkyCooldown = cooldowns[4];

            if (fingerProcessed)
            {
                needStop = false; // 有手指在运动，不需要停止
            }
        }

        // 停止所有其他未被选中的马达
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (i != maxNegativeIndex && lastSent[prefixes[i]] != 0)
            {
                lastSent[prefixes[i]] = 0;
                Debug.Log($"🔻 {fingerNames[i]} stopped (not max negative)");
            }
        }

        // 发送指令
        if (!needStop && tokens.Count > 0)
        {
            anyFingerMoving = true;
            lastSlipEventTime = Time.time;
            SendLine(string.Join(",", tokens) + ",\n");
        }
        else if (needStop && anyFingerMoving)
        {
            StopMotors();
            anyFingerMoving = false;
        }

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            StopMotors();
            anyFingerMoving = false;
        }
    }

    // —— 将单指的连续值映射为“分段PWM” —— //
    private int MapValueToBandedSpeed(float absValue)
    {
        if (inputThresholds == null || pwmBands == null || pwmBands.Length == 0)
        {
            CalculateNonUniformBands();
        }

        // 边界（含死区外的起点）
        if (absValue <= inputThresholds[0]) return pwmBands[0];
        if (absValue >= inputThresholds[numberOfBands]) return pwmBands[numberOfBands - 1];

        // 线性扫描找区间（band i: [thr[i], thr[i+1])）
        for (int i = 0; i < numberOfBands; i++)
        {
            if (absValue < inputThresholds[i + 1])
            {
                return pwmBands[i];
            }
        }

        return pwmBands[numberOfBands - 1];
    }

    // 检查单个手指状态，返回是否有运动指令（true 表示这帧给出了运动指令）
    bool CheckSingleFinger(List<string> tokens, float value, ref float cooldownTimer, float now, string prefix, string fingerName)
    {
        if (now - cooldownTimer < cooldownTime) return false;

        float a = Mathf.Abs(value);
        int previousValue = lastSent[prefix];

        // 死区
        if (a < inputMin)
        {
            if (previousValue != 0)
            {
                lastSent[prefix] = 0;
                cooldownTimer = now;
                Debug.Log($"🔻 {fingerName} entered deadzone (was {previousValue})");
            }
            return false;
        }

        int signed;
        // 超过硬阈值直接饱和
        if (a > HARD_SAT_CUTOFF)
        {
            signed = (value >= 0f) ? 255 : -255;
        }
        else
        {
            int mag = MapValueToBandedSpeed(a);
            signed = (value >= 0f) ? mag : -mag;
        }

        if (Mathf.Abs(signed - previousValue) < minDeltaPwm) return false;

        lastSent[prefix] = signed;
        tokens.Add($"{prefix}{signed}");
        cooldownTimer = now;

        return true;
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
