using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grasp_HandTracking : MonoBehaviour
{
    [Header("手部引用 | Hand Refs")]
    public Transform[] fingerTips;
    public Transform thumbtip;
    public GameObject hand;

    [Header("抓握距离参数")]
    public float minGripDistance = 0.01f;
    public float maxGripDistance = 0.1f;

    [Header("抓握次数统计")]
    [ReadOnly, SerializeField] private int attempt = 0; // Inspector 只读显示

    private bool hasClosed = false;

    // 缓存引用，避免每帧查找
    private ExperimentSaveData_JSON saveJSON;
    private PincherController pincherController;

    void Start()
    {
        saveJSON = FindObjectOfType<ExperimentSaveData_JSON>();
        if (hand != null)
        {
            pincherController = hand.GetComponent<PincherController>();
            if (pincherController == null)
                Debug.LogWarning("[Grasp_HandTracking] hand 上未找到 PincherController 组件。");
        }
        else
        {
            Debug.LogWarning("[Grasp_HandTracking] hand 未绑定。");
        }

        // 基本参数保护
        if (maxGripDistance <= minGripDistance)
        {
            Debug.LogWarning("[Grasp_HandTracking] maxGripDistance 应大于 minGripDistance，已自动微调。");
            maxGripDistance = minGripDistance + 0.001f;
        }
    }

    void Update()
    {
        if (pincherController == null || thumbtip == null || fingerTips == null || fingerTips.Length == 0)
            return;

        // 计算拇指与其余指尖的平均距离
        float fingertipDist = 0f;
        int validCount = 0;
        for (int i = 0; i < fingerTips.Length; i++)
        {
            Transform t = fingerTips[i];
            if (t == null) continue;
            fingertipDist += Vector3.Distance(thumbtip.position, t.position);
            validCount++;
        }
        if (validCount == 0) return;

        float avfingertipDist = fingertipDist / validCount;

        // 归一化到 [-1, 1]：越小越“闭合”（负），越大越“张开”（正）
        float normalized = Mathf.Clamp01((avfingertipDist - minGripDistance) / (maxGripDistance - minGripDistance));
        float gripInput = normalized * 2f - 1f;

        // 写入抓取状态
        pincherController.gripState = GripStateForInput(gripInput);

        // 边沿检测：从张开 -> 闭合 计一次
        if (pincherController.gripState == GripState.Closing && !hasClosed)
        {
            attempt++;
            if (saveJSON != null) saveJSON.OnGraspAttempt(); // 用缓存，不再 FindObjectOfType
            hasClosed = true;
        }
        else if (pincherController.gripState == GripState.Opening)
        {
            hasClosed = false;
        }
    }

    public void ResetAttempt()
    {
        attempt = 0;
        hasClosed = false; // 避免紧接着一次误判继续 +1
    }

    // 对外只读
    public int AttemptCount => attempt;

    static GripState GripStateForInput(float input)
    {
        if (input < 0f) return GripState.Closing;
        if (input > 0f) return GripState.Opening;
        return GripState.Fixed;
    }
}
