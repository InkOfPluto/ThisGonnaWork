using UnityEngine;
using System.IO.Ports;
using System;
using System.Collections.Generic;

public class SlippingTwoFinger : MonoBehaviour
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
    public float inputMax = 0.11f;     // 默认上限 0.11；但 >0.11 直接 ±255

    [Tooltip("低端顺滑指数（>1 更柔和）")]
    public float gamma = 1.2f;

    [Tooltip("最小变化量（PWM 级），小于该差值则不重发")]
    public int minDeltaPwm = 8;

    [Header("PWM Range (Inspector 可调)")]
    [Range(0, 255)]
    public int pwmMin = 100;
    [Range(0, 255)]
    public int pwmMax = 255;

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
    }

    void Start()
    {
        OpenSerialPort(ref serial, portName, baudRate);
        Debug.Log("🟢 Slipping ready. Protocol: t/i/m/r/p±NNN; 's' for stop. Now drives most-negative and (if present) most-positive finger.");
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

        // 获取所有距离值（依次：大拇指、食指、中指、无名指、小拇指）
        float[] distances = {
            visual.DistanceDA,
            visual.DistanceSHI,
            visual.DistanceZHONG,
            visual.DistanceWU,
            visual.DistanceXIAO
        };

        string[] prefixes = { "t", "i", "m", "r", "p" };
        string[] fingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        // === 选出一个“最负值(最向下)”与一个“最正值(最向上)” ===
        int minNegIndex = -1;
        float minNegValue = 0f; // 只记录负值中的最小（数值更小）
        int maxPosIndex = -1;
        float maxPosValue = 0f; // 只记录正值中的最大

        for (int i = 0; i < distances.Length; i++)
        {
            float v = distances[i];
            if (v < 0f)
            {
                if (minNegIndex < 0 || v < minNegValue)
                {
                    minNegIndex = i;
                    minNegValue = v;
                }
            }
            else if (v > 0f)
            {
                if (maxPosIndex < 0 || v > maxPosValue)
                {
                    maxPosIndex = i;
                    maxPosValue = v;
                }
            }
        }

        var tokens = new List<string>(2); // 最多两个马达会被激活（一个向下，一个向上）
        bool needStop = true; // 默认需要停止

        // 把需要处理的手指索引收集起来：负向最大一定尝试；正向最大“若存在”也尝试
        List<int> selectedIndices = new List<int>(2);
        if (minNegIndex >= 0) selectedIndices.Add(minNegIndex);
        if (maxPosIndex >= 0) selectedIndices.Add(maxPosIndex);

        // 分别检查并生成对应的指令
        if (selectedIndices.Count > 0)
        {
            float[] cooldowns = { thumbCooldown, indexCooldown, middleCooldown, ringCooldown, pinkyCooldown };

            foreach (int idx in selectedIndices)
            {
                bool fingerProcessed = CheckSingleFinger(
                    tokens,
                    distances[idx],
                    ref cooldowns[idx],
                    now,
                    prefixes[idx],
                    fingerNames[idx]
                );
                if (fingerProcessed) needStop = false;
            }

            // 更新冷却时间
            thumbCooldown = cooldowns[0];
            indexCooldown = cooldowns[1];
            middleCooldown = cooldowns[2];
            ringCooldown = cooldowns[3];
            pinkyCooldown = cooldowns[4];
        }

        // 停止其他未被选中的手指（仅重置本地状态与日志，不下发单独的 stop token）
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (!selectedIndices.Contains(i) && lastSent[prefixes[i]] != 0)
            {
                lastSent[prefixes[i]] = 0;
                Debug.Log($"🔻 {fingerNames[i]} stopped (not selected)");
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

    // 检查单个手指状态，返回是否有运动指令
    bool CheckSingleFinger(List<string> tokens, float value, ref float cooldownTimer, float now, string prefix, string fingerName)
    {
        if (now - cooldownTimer < cooldownTime) return false;

        float a = Mathf.Abs(value);
        int previousValue = lastSent[prefix];

        // 当值小于死区时
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
        if (a > HARD_SAT_CUTOFF)
        {
            signed = (value >= 0f) ? 255 : -255;
        }
        else
        {
            // 正常比例映射：inputMin..inputMax → pwmMin..pwmMax，再取正负号
            float t = Mathf.InverseLerp(inputMin, inputMax, a);
            t = Mathf.Pow(Mathf.Clamp01(t), gamma);
            int mag = Mathf.RoundToInt(Mathf.Lerp((float)pwmMin, (float)pwmMax, t));
            mag = Mathf.Clamp(mag, pwmMin, pwmMax);
            signed = (value >= 0f) ? mag : -mag;
        }

        if (Mathf.Abs(signed - previousValue) < minDeltaPwm) return false;

        lastSent[prefix] = signed;
        tokens.Add($"{prefix}{signed}");
        cooldownTimer = now;

        return true; // 有运动指令
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
