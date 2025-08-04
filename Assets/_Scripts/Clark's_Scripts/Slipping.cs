using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class Slipping : MonoBehaviour
{
    [Header("Ports | 串口设置")]
    public string Comport1 = "COM5";
    public string Comport2 = "COM6";
    private SerialPort serial1;
    private SerialPort serial2;

    [Header("依赖组件 | 自动获取")]
    [SerializeField] private PincherController pincherController;
    [SerializeField] private VisualDisplay visualDisplay;

    [Header("调试参数")]
    public float slipThreshold = 0.001f;   // 滑动阈值
    public float amplify = 100f;           // 放大滑动速度
    public float cooldownTime = 0.2f;      // 每个手指串口冷却时间

    private Coroutine slipMonitorRoutine;
    private float[] lastSendTime = new float[5];  // [DA, SHI, ZHONG, WU, XIAO]

    private void Start()
    {

        // 自动查找组件
        if (pincherController == null)
            pincherController = GetComponent<PincherController>();
        if (visualDisplay == null)
            visualDisplay = GetComponent<VisualDisplay>();

        if (pincherController == null)
            Debug.LogError("❌ 未找到 PincherController！");
        if (visualDisplay == null)
            Debug.LogError("❌ 未找到 VisualDisplay！");

        // 打开串口
        try
        {
            serial1 = new SerialPort(Comport1, 115200);
            serial2 = new SerialPort(Comport2, 115200);
            serial1.Open();
            serial2.Open();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("串口连接失败: " + e.Message);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (slipMonitorRoutine != null)
                StopCoroutine(slipMonitorRoutine);
            slipMonitorRoutine = StartCoroutine(SlipMonitorRoutine());
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            if (slipMonitorRoutine != null)
            {
                StopCoroutine(slipMonitorRoutine);
                Debug.Log("滑动监测关闭 ×");
            }
        }
    }

    private IEnumerator SlipMonitorRoutine()
    {
        Debug.Log("滑动监测启动 ✅");

        while (true)
        {
            if (pincherController != null && pincherController.gripState != GripState.Opening)

            {
                float t = Time.time;

                // ✨ 注意：DistanceXXX 本身就是高度差，乘以放大倍率就代表滑动速度
                float speedDA = visualDisplay.DistanceDA * amplify;
                float speedSHI = visualDisplay.DistanceSHI * amplify;
                float speedZHONG = visualDisplay.DistanceZHONG * amplify;
                float speedWU = visualDisplay.DistanceWU * amplify;
                float speedXIAO = visualDisplay.DistanceXIAO * amplify;

                Debug.Log($"[🧪滑动速度] DA:{speedDA:F3} | SHI:{speedSHI:F3} | ZHONG:{speedZHONG:F3} | WU:{speedWU:F3} | XIAO:{speedXIAO:F3}");

                // === 串口控制逻辑 ===

                // 大拇指（COM5）
                if (Mathf.Abs(speedDA) > slipThreshold && t - lastSendTime[0] > cooldownTime)
                {
                    if (serial1?.IsOpen ?? false)
                        serial1.WriteLine(speedDA > 0 ? "fffff" : "bbbbb");
                    lastSendTime[0] = t;
                }

                // 食指（COM6）
                if (Mathf.Abs(speedSHI) > slipThreshold && t - lastSendTime[1] > cooldownTime)
                {
                    if (serial2?.IsOpen ?? false)
                        serial2.WriteLine(speedSHI > 0 ? "fffff" : "bbbbb");
                    lastSendTime[1] = t;
                }

                // 中指（COM6）
                if (Mathf.Abs(speedZHONG) > slipThreshold && t - lastSendTime[2] > cooldownTime)
                {
                    if (serial2?.IsOpen ?? false)
                        serial2.WriteLine(speedZHONG > 0 ? "ggggg" : "nnnnn");
                    lastSendTime[2] = t;
                }

                // 无名指（COM5）
                if (Mathf.Abs(speedWU) > slipThreshold && t - lastSendTime[3] > cooldownTime)
                {
                    if (serial1?.IsOpen ?? false)
                        serial1.WriteLine(speedWU > 0 ? "ggggg" : "nnnnn");
                    lastSendTime[3] = t;
                }

                // 小拇指（可注释）
                /*
                if (Mathf.Abs(speedXIAO) > slipThreshold && t - lastSendTime[4] > cooldownTime)
                {
                    if (serial1?.IsOpen ?? false)
                        serial1.WriteLine(speedXIAO > 0 ? "xxxxx" : "yyyyy");
                    lastSendTime[4] = t;
                }
                */
            }

            yield return new WaitForFixedUpdate();
        }
    }
}
