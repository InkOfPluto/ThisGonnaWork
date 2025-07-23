using UnityEngine;

public class VisualDisplay : MonoBehaviour
{
    [Header("Game Objects | 游戏物体")]
    public Transform cubeCenter;
    public Transform cylinder;

    [Header("Finger Cubes | 手指方块")]
    public Transform[] fingers;

    [Header("Cube Points | 方块表面中心点")]
    public Transform CubePoint_DA;
    public Transform CubePoint_SHI;
    public Transform CubePoint_ZHONG;
    public Transform CubePoint_WU;
    public Transform CubePoint_XIAO;

    [Header("Point2Point Vectors | 方块到投影点连线向量")]
    public Vector3 ObjectPoint_DA;
    public Vector3 ObjectPoint_SHI;
    public Vector3 ObjectPoint_ZHONG;
    public Vector3 ObjectPoint_WU;
    public Vector3 ObjectPoint_XIAO;

    private Vector3 localYCenterWorld;
    private float fixedRadius = 0.015f;

    [Header("Visual Display Point Radius | 点的可视化小球半径")]
    public float gizmoSphereRadius = 0.001f; 

    [Header("Value Of Slipping DistanceY | Y方向滑动数值")]
    public float DistanceDA;
    public float DistanceSHI;
    public float DistanceZHONG;
    public float DistanceWU;
    public float DistanceXIAO;


    void Update()
    {
        if (cubeCenter == null || cylinder == null || fingers.Length < 5)
            return;

        Vector3 cylinderY = cylinder.up;
        Vector3 vecToCylinder = cylinder.position - cubeCenter.position;
        float t = Vector3.Dot(vecToCylinder, cylinderY);
        localYCenterWorld = cylinder.position - cylinderY * t;
        Vector3 localYCenter = cylinder.InverseTransformPoint(localYCenterWorld);

        for (int i = 0; i < 5; i++)
        {
            Vector3 localFinger = cylinder.InverseTransformPoint(fingers[i].position);
            Vector2 centerXZ = new Vector2(localYCenter.x, localYCenter.z);
            Vector2 fingerXZ = new Vector2(localFinger.x, localFinger.z);
            Vector2 dir = (fingerXZ - centerXZ).normalized;

            Vector2 projectedXZ = centerXZ + fixedRadius * dir;
            Vector3 projectedLocal = new Vector3(projectedXZ.x, localYCenter.y, projectedXZ.y);
            Vector3 projectedWorld = cylinder.TransformPoint(projectedLocal);

            switch (i)
            {
                case 0: ObjectPoint_DA = projectedWorld; break;
                case 1: ObjectPoint_SHI = projectedWorld; break;
                case 2: ObjectPoint_ZHONG = projectedWorld; break;
                case 3: ObjectPoint_WU = projectedWorld; break;
                case 4: ObjectPoint_XIAO = projectedWorld; break;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (cylinder == null || fingers == null || fingers.Length < 5)
            return;

        Vector3[] points = { ObjectPoint_DA, ObjectPoint_SHI, ObjectPoint_ZHONG, ObjectPoint_WU, ObjectPoint_XIAO };
        Transform[] cubePoints = { CubePoint_DA, CubePoint_SHI, CubePoint_ZHONG, CubePoint_WU, CubePoint_XIAO };

        // 🟡 圆心小球
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(localYCenterWorld, gizmoSphereRadius);

        // 🟢 显示五个 CubePoint 小球
        Gizmos.color = Color.green;
        for (int i = 0; i < cubePoints.Length; i++)
        {
            if (cubePoints[i] != null)
                Gizmos.DrawSphere(cubePoints[i].position, gizmoSphereRadius);
        }

        for (int i = 0; i < points.Length; i++)
        {
            // 🔵 投影点小球
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(points[i], gizmoSphereRadius);

            if (cubePoints[i] != null)
            {
                Vector3 start = cubePoints[i].position;
                Vector3 end = points[i];

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(start, gizmoSphereRadius);

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(start, end);

                // 🔴 红色线段：Y轴投影
                Vector3 yProjectionStart = new Vector3(start.x, end.y, start.z);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(start, yProjectionStart);

                // ✅ 计算带方向的 Y 轴差值
                float deltaY = end.y - start.y;

                // ✅ 存入对应变量
                switch (i)
                {
                    case 0: DistanceDA = deltaY; break;
                    case 1: DistanceSHI = deltaY; break;
                    case 2: DistanceZHONG = deltaY; break;
                    case 3: DistanceWU = deltaY; break;
                    case 4: DistanceXIAO = deltaY; break;
                }
            }

        }

        // 🟣 画出圆柱上的圆
        Gizmos.color = Color.magenta;
        int segments = 60;
        Vector3 localYCenter = cylinder.InverseTransformPoint(localYCenterWorld);

        for (int i = 0; i < segments; i++)
        {
            float angle1 = Mathf.Deg2Rad * (360f * i / segments);
            float angle2 = Mathf.Deg2Rad * (360f * (i + 1) / segments);

            Vector3 local1 = new Vector3(Mathf.Cos(angle1) * fixedRadius, 0, Mathf.Sin(angle1) * fixedRadius);
            Vector3 local2 = new Vector3(Mathf.Cos(angle2) * fixedRadius, 0, Mathf.Sin(angle2) * fixedRadius);

            Vector3 world1 = cylinder.TransformPoint(localYCenter + local1);
            Vector3 world2 = cylinder.TransformPoint(localYCenter + local2);
            Gizmos.DrawLine(world1, world2);
        }
    }
}
