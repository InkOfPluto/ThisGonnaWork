using UnityEngine;
using System.IO.Ports;
using System;

public class MotorTesting : MonoBehaviour
{
    [Header("Serial Port | 串口")]
    [Tooltip("串口名，例如 COM11 / Serial port name, e.g., COM11")]
    public string portName = "COM11";

    [Tooltip("波特率 | Baud rate")]
    public int baudRate = 115200;

    [Header("操作说明 | Instructions")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
          "按 - ：五指统一负向旋转（使用各自速度）\n" +
          "按 = ：五指统一正向旋转（使用各自速度）\n" +
          "单指：j(Thumb) k(Index) l(Middle) ;(Ring) '(Pinky)\n" +
          "      按住 Shift + 上述按键 = 负向；不按 Shift = 正向\n" +
          "按 0 ：发送 s 急停\n";

    private SerialPort serial;

    [Header("Finger Speeds (0~255) | 各手指速度")]
    public int thumbSpeed = 200;   // t
    public int indexSpeed = 200;   // i
    public int middleSpeed = 200;  // m
    public int ringSpeed = 200;    // r
    public int pinkySpeed = 200;   // p

    void Start()
    {
        OpenSerialPort(ref serial, portName, baudRate);
        Debug.Log("🧪 MotorTest ready. '-'=All Negative, '='=All Positive, '0'=StopAll, j/k/l/;/'.");
    }

    void Update()
    {
        // —— 全体负向：'-'
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            SendBatchCommand(new string[] {
                Tok("t", -thumbSpeed),
                Tok("i", -indexSpeed),
                Tok("m", -middleSpeed),
                Tok("r", -ringSpeed),
                Tok("p", -pinkySpeed),
            });
            Debug.Log("🔻 All motors: NEGATIVE (use per-finger speeds).");
        }

        // —— 全体正向：'='
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            SendBatchCommand(new string[] {
                Tok("t", +thumbSpeed),
                Tok("i", +indexSpeed),
                Tok("m", +middleSpeed),
                Tok("r", +ringSpeed),
                Tok("p", +pinkySpeed),
            });
            Debug.Log("🔺 All motors: POSITIVE (use per-finger speeds).");
        }

        // —— 单指控制：j k l ; '
        bool neg = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.J)) SendOne("t", thumbSpeed, neg, "Thumb");
        if (Input.GetKeyDown(KeyCode.K)) SendOne("i", indexSpeed, neg, "Index");
        if (Input.GetKeyDown(KeyCode.L)) SendOne("m", middleSpeed, neg, "Middle");
        if (Input.GetKeyDown(KeyCode.Semicolon)) SendOne("r", ringSpeed, neg, "Ring");
        if (Input.GetKeyDown(KeyCode.Quote)) SendOne("p", pinkySpeed, neg, "Pinky");

        // —— 一键急停：'0'
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            SendLine("s\n");
            Debug.Log("🛑 Sent global stop 's'.");
        }
    }

    // 发送单指
    void SendOne(string prefix, int speed, bool negative, string name)
    {
        int signed = negative ? -Clamp255(speed) : +Clamp255(speed);
        SendBatchCommand(new string[] { $"{prefix}{signed}" });
        Debug.Log($"{(negative ? "⬅️" : "➡️")} {name}: {prefix}{signed}");
    }

    // 批量发送（自动拼接逗号和换行）
    void SendBatchCommand(string[] commands)
    {
        string commandLine = string.Join(",", commands) + ",\n";
        SendLine(commandLine);
    }

    // 生成 token
    string Tok(string prefix, int signedSpeed) => $"{prefix}{Mathf.Clamp(signedSpeed, -255, 255)}";

    void SendLine(string line)
    {
        if (serial == null || !serial.IsOpen)
        {
            Debug.LogWarning("⚠️ Serial not open. Trying to reopen...");
            OpenSerialPort(ref serial, portName, baudRate);
        }

        if (serial != null && serial.IsOpen)
        {
            try { serial.Write(line); Debug.Log("✅ Sent: " + line.Trim()); }
            catch (Exception e) { Debug.LogWarning("⚠️ Failed to send: " + line.Trim() + " → " + e.Message); }
        }
        else
        {
            Debug.LogError("❌ Serial still not open, cannot send.");
        }
    }

    int Clamp255(int v) => Mathf.Clamp(v, 0, 255);

    void OpenSerialPort(ref SerialPort port, string name, int baud)
    {
        try
        {
            if (port != null && port.IsOpen) port.Close();
            port = new SerialPort(name, baud);
            port.ReadTimeout = 50;
            port.NewLine = "\n";
            port.Open();
            Debug.Log("✅ Serial opened: " + name);
        }
        catch (Exception e)
        {
            Debug.LogWarning("❌ Failed to open port " + name + ": " + e.Message);
        }
    }

    void OnApplicationQuit()
    {
        if (serial != null && serial.IsOpen) serial.Close();
    }
}
