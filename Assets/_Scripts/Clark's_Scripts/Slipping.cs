using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class Slipping : MonoBehaviour
{
    [Header("Ports | 端口")]
    public string Comport1 = "COM5";
    public string Comport2 = "COM6";

    private SerialPort serial1;
    private SerialPort serial2;

    [Header("Game Objects | 游戏物体")]
    public GameObject cylinder;

    [Header("Finger Cubes | 手指方块")]
    public ArticulationBody CubeDA, CubeSHI, CubeZHONG, CubeWU, CubeXIAO;

    private PincherController pincherController;
    private Coroutine slipMonitorRoutine;

    private Vector3 lastCylinderPos;
    private Vector3 thumbLastPos, indexLastPos, middleLastPos, ringLastPos, pinkyLastPos;

    [Header("Mass Center | 质心")]
    public float DeltaMass = 5f;

    // 预设的中心位置
    public Vector3 centerB = new Vector3(0.03f, 0, 0.02f);
    private Vector3 centerN = Vector3.zero;
    public Vector3 centerM = new Vector3(-0.01f, -0.02f, 0);

    private void Start()
    {
        pincherController = GetComponent<PincherController>();

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

        lastCylinderPos = cylinder.transform.position;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (slipMonitorRoutine != null)
                StopCoroutine(slipMonitorRoutine);

            slipMonitorRoutine = StartCoroutine(SlipMonitorRoutine());
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            if (slipMonitorRoutine != null)
                StopCoroutine(slipMonitorRoutine);
            Debug.Log("滑动监测关闭 ×");
        }

        // 按 C 减少质量
        if (Input.GetKeyDown(KeyCode.C))
        {
            Rigidbody rb = cylinder.GetComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.1f, rb.mass - DeltaMass);
            Debug.Log($"减少质量，当前质量: {rb.mass}");
        }

        // 按 V 增加质量
        if (Input.GetKeyDown(KeyCode.V))
        {
            Rigidbody rb = cylinder.GetComponent<Rigidbody>();
            rb.mass += DeltaMass;
            Debug.Log($"增加质量，当前质量: {rb.mass}");
        }

        // 设定质心
        if (Input.GetKeyDown(KeyCode.B))
        {
            SetCenterOfMass(centerB);
            Debug.Log("质心设置为 B 预设");
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            SetCenterOfMass(centerN);
            Debug.Log("质心设置为 N 默认中心");
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            SetCenterOfMass(centerM);
            Debug.Log("质心设置为 M 预设");
        }
    }

    private void SetCenterOfMass(Vector3 center)
    {
        Rigidbody rb = cylinder.GetComponent<Rigidbody>();
        rb.centerOfMass = center;
    }

    private IEnumerator SlipMonitorRoutine()
    {
        Debug.Log("滑动监测启动 ✅");

        while (true)
        {
            if (pincherController != null && pincherController.gripState == GripState.Closing)
            {
                Vector3 currentCylinderPos = cylinder.transform.position;

                Vector3 thumbPos = CubeDA.transform.position;
                Vector3 indexPos = CubeSHI.transform.position;
                Vector3 middlePos = CubeZHONG.transform.position;
                Vector3 ringPos = CubeWU.transform.position;
                Vector3 pinkyPos = CubeXIAO.transform.position;

                float thumbVelY = (currentCylinderPos.y - thumbPos.y) - (lastCylinderPos.y - thumbLastPos.y);
                float indexVelY = (currentCylinderPos.y - indexPos.y) - (lastCylinderPos.y - indexLastPos.y);
                float middleVelY = (currentCylinderPos.y - middlePos.y) - (lastCylinderPos.y - middleLastPos.y);
                float ringVelY = (currentCylinderPos.y - ringPos.y) - (lastCylinderPos.y - ringLastPos.y);
                float pinkyVelY = (currentCylinderPos.y - pinkyPos.y) - (lastCylinderPos.y - pinkyLastPos.y);

                DetectAndSend(thumbVelY, serial1, "ffffff", "bbbbbb", "拇指");
                DetectAndSend(indexVelY, serial2, "ffffff", "bbbbbb", "食指");
                DetectAndSend(middleVelY, serial2, "gggggg", "nnnnnn", "中指");
                DetectAndSend(ringVelY, serial1, "gggggg", "nnnnnn", "无名指");
                // DetectAndSend(pinkyVelY, serial3, "gggggg", "nnnnnn", "小拇指");

                lastCylinderPos = currentCylinderPos;
                thumbLastPos = thumbPos;
                indexLastPos = indexPos;
                middleLastPos = middlePos;
                ringLastPos = ringPos;
                pinkyLastPos = pinkyPos;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    private void DetectAndSend(float relativeVelY, SerialPort serial, string downMsg, string upMsg, string fingerName)
    {
        float threshold = 0.01f;

        if (relativeVelY > threshold)
        {
            try
            {
                serial.WriteLine(downMsg);
                Debug.Log($"{fingerName} 向下滑动，发送：{downMsg}");
            }
            catch { Debug.LogWarning("串口写入失败"); }
        }
        else if (relativeVelY < -threshold)
        {
            try
            {
                serial.WriteLine(upMsg);
                Debug.Log($"{fingerName} 向上滑动，发送：{upMsg}");
            }
            catch { Debug.LogWarning("串口写入失败"); }
        }
    }
}
