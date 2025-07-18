using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grasp_HandTracking : MonoBehaviour
{
    public Transform[] fingerTips;        // 手指指尖对象数组
    public Transform thumbtip;            // 拇指指尖对象
    public GameObject hand;               // 手部对象

    [Header("抓握距离参数")]
    public float minGripDistance = 0.01f; // 手指最闭合时的距离
    public float maxGripDistance = 0.1f; // 手完全张开时的距离

    void Update()
    {
        float fingertipDist = 0f;

        // 计算所有手指指尖到拇指的平均距离
        foreach (Transform t in fingerTips)
        {
            fingertipDist += Vector3.Distance(thumbtip.position, t.position);
        }

        float avfingertipDist = fingertipDist / fingerTips.Length;

        // 将平均距离从 [min, max] 映射到 [-1, 1]：越闭合 -> 趋近 -1，越张开 -> 趋近 1
        float normalized = Mathf.Clamp01((avfingertipDist - minGripDistance) / (maxGripDistance - minGripDistance));
        float gripInput = normalized * 2f - 1f; // 线性映射到 [-1, 1]
        //Debug.Log("GripInput is " + gripInput);

        // 设置 GripState
        PincherController pincherController = hand.GetComponent<PincherController>();
        pincherController.gripState = GripStateForInput(gripInput);

        // 调试输出
        //Debug.Log($"[🤏] 平均指尖距: {avfingertipDist:F4} | input: {gripInput:F4}");
    }

    // GripState 三态判定
    static GripState GripStateForInput(float input)
    {
        if (input < 0f)
        {
            return GripState.Closing;
        }
        else if (input > 0f)
        {
            return GripState.Opening;
        }
        else
        {
            return GripState.Fixed;
        }
    }
}
