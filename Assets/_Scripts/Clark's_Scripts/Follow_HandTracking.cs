using System.Collections.Generic;
using UnityEngine;

public class Follow_HandTracking : MonoBehaviour
{
    [Header("中心与目标")]
    public Transform cubeCenter;
    public Transform handCenter;

    [Header("手指目标")]
    public Transform rThumbTip;
    public Transform rIndexTip;
    public Transform rMiddleTip;
    public Transform rRingTip;
    public Transform rLittleTip;

    [Header("对应的五个Cube")]
    public Transform fingerDa;
    public Transform fingerShi;
    public Transform fingerZhong;
    public Transform fingerWu;
    public Transform fingerXiao;

    [Header("高度设置")]
    public float height = 0.8f;

    [Header("旋转半径")]
    public float radius = 0.05f;

    [Header("最小间距")]
    public float minDistance = 0.015f;

    private List<(Transform tip, Transform cube)> fingers;
    private Dictionary<Transform, Vector3> lastValidPositions = new Dictionary<Transform, Vector3>();

    void Start()
    {
        fingers = new List<(Transform, Transform)>
        {
            (rThumbTip, fingerDa),
            (rIndexTip, fingerShi),
            (rMiddleTip, fingerZhong),
            (rRingTip, fingerWu),
            (rLittleTip, fingerXiao)
        };

        // 初始化合法位置
        foreach (var (_, cube) in fingers)
        {
            if (cube != null)
                lastValidPositions[cube] = cube.position;
        }
    }

    void Update()
    {
        // 固定 cubeCenter 高度
        Vector3 centerPos = cubeCenter.position;
        centerPos.y = height;
        cubeCenter.position = centerPos;

        Dictionary<Transform, Vector3> candidatePositions = new Dictionary<Transform, Vector3>();

        // 先计算每个目标点位置
        foreach (var (tip, cube) in fingers)
        {
            if (tip == null || cube == null || handCenter == null || cubeCenter == null)
                continue;

            Vector3 directionXZ = tip.position - handCenter.position;
            directionXZ.y = 0;

            if (directionXZ != Vector3.zero)
                directionXZ.Normalize();

            Vector3 targetPos = cubeCenter.position + directionXZ * radius;
            candidatePositions[cube] = new Vector3(targetPos.x, height, targetPos.z);
        }

        // 然后逐个检测是否与其他 cube 太近
        foreach (var (tip, cube) in fingers)
        {
            if (!candidatePositions.ContainsKey(cube))
                continue;

            Vector3 proposedPos = candidatePositions[cube];
            bool isTooClose = false;

            foreach (var otherCube in candidatePositions.Keys)
            {
                if (otherCube == cube) continue;
                Vector3 otherPos = candidatePositions[otherCube];
                float dist = Vector3.Distance(proposedPos, otherPos);
                if (dist < minDistance)
                {
                    isTooClose = true;
                    break;
                }
            }

            if (isTooClose)
            {
                // 使用上一帧合法位置
                if (lastValidPositions.ContainsKey(cube))
                {
                    cube.position = lastValidPositions[cube];
                }
            }
            else
            {
                // 更新 cube 位置
                cube.position = proposedPos;
                lastValidPositions[cube] = proposedPos;
            }

            // ✅ 安全地旋转使其朝向中心
            ArticulationBody ab = cube.GetComponent<ArticulationBody>();
            bool wasEnabled = ab != null && ab.enabled;
            if (ab != null) ab.enabled = false;

            cube.LookAt(new Vector3(cubeCenter.position.x, height, cubeCenter.position.z));

            if (ab != null) ab.enabled = wasEnabled;
        }
    }

}
