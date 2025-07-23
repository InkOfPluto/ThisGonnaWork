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

    [Header("旋转半径")]
    public float radius = 0.05f;

    private List<(Transform tip, Transform cube)> fingers;

    public Dictionary<Transform, float> lockedYPositions = new Dictionary<Transform, float>();


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
    }

    void Update()
    {
        foreach (var (tip, cube) in fingers)
        {
            if (tip == null || cube == null || handCenter == null || cubeCenter == null)
                continue;

            Vector3 directionXZ = tip.position - handCenter.position;
            directionXZ.y = 0;

            if (directionXZ != Vector3.zero)
                directionXZ.Normalize();

            Vector3 targetPos = cubeCenter.position + directionXZ * radius;
            if (lockedYPositions.ContainsKey(cube))
                targetPos.y = lockedYPositions[cube];
            else
                targetPos.y = cube.position.y;


            cube.position = targetPos;

            // 安全旋转朝向中心
            ArticulationBody ab = cube.GetComponent<ArticulationBody>();
            bool wasEnabled = ab != null && ab.enabled;
            if (ab != null) ab.enabled = false;

            Vector3 lookTarget = cubeCenter.position;
            lookTarget.y = cube.position.y;
            cube.LookAt(lookTarget);

            if (ab != null) ab.enabled = wasEnabled;
        }
    }
}
