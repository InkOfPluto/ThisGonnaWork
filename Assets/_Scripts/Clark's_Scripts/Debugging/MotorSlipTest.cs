using System.Collections;
using System.IO.Ports;
using UnityEngine;

public class MotorSlipTest : MonoBehaviour
{
    public string Comport1 = "COM5";
    public string Comport2 = "COM6";

    private SerialPort serial1;
    private SerialPort serial2;

    private Coroutine testRoutine;

    void Start()
    {
        try
        {
            serial1 = new SerialPort(Comport1, 115200);
            serial2 = new SerialPort(Comport2, 115200);
            serial1.Open();
            serial2.Open();
            Debug.Log("✅ 串口连接成功");
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ 串口连接失败: " + e.Message);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            StartSending("negative");  // 向下滑
        }

        if (Input.GetKeyDown(KeyCode.Equals))
        {
            StartSending("positive"); // 向上滑
        }

        if (Input.GetKeyUp(KeyCode.Minus) || Input.GetKeyUp(KeyCode.Equals))
        {
            StopSending();
        }
    }

    void StartSending(string direction)
    {
        if (testRoutine != null) StopCoroutine(testRoutine);
        testRoutine = StartCoroutine(SendSimulatedSlip(direction));
    }

    void StopSending()
    {
        if (testRoutine != null)
        {
            StopCoroutine(testRoutine);
            testRoutine = null;
            Debug.Log("⏹️ 停止发送");
        }
    }

    IEnumerator SendSimulatedSlip(string direction)
    {
        Debug.Log($"▶️ 模拟滑动方向: {(direction == "positive" ? "向上↑" : "向下↓")}");

        string command1 = direction == "positive" ? "f:150" : "b:150";  // COM5
        string command2 = direction == "positive" ? "g:150" : "n:150";  // COM6

        while (true)
        {
            if (serial1?.IsOpen ?? false) serial1.WriteLine(command1);
            if (serial2?.IsOpen ?? false) serial2.WriteLine(command2);

            yield return new WaitForSeconds(0.05f);
        }
    }



    private void OnApplicationQuit()
    {
        if (serial1?.IsOpen ?? false) serial1.Close();
        if (serial2?.IsOpen ?? false) serial2.Close();
    }
}
