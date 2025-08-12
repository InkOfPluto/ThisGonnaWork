using UnityEngine;

public class VisualDisplay : MonoBehaviour
{
    [Header("操作说明 | Instructions")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
        "功能：计算 Cylinder 周围 5 个手指方块的滑动距离，并在 Scene 视图中可视化圆环、投影点、连接线等\n" +
        "Inspector：\n" +
        " - cylinder：要围绕计算的圆柱\n" +
        " - cubeTouching：Y 会自动对齐 5 指方块的平均 Y\n" +
        " - fingers：5 个手指方块（顺序：DA, SHI, ZHONG, WU, XIAO）\n" +
        " - CubePoint_XXX：每个方块对应的表面中心点 Transform\n" +
        "运行逻辑：\n" +
        " - 每帧计算 5 个手指方块的平均 Y，并将 cubeTouching 的 Y 对齐\n" +
        " - 将每个手指位置在 Cylinder 局部投影到固定半径圆环上\n" +
        " - 计算投影点与 CubePoint 的 Y 差值作为滑动距离\n" +
        "可视化：\n" +
        " - showCenterPoint：显示圆环中心\n" +
        " - showCubePoints：显示方块中心点\n" +
        " - showProjectedPoints：显示投影点\n" +
        " - showConnectingLines：CubePoint 到投影点的线\n" +
        " - showYProjectionLines：CubePoint 到同 Y 高度点的垂直线\n" +
        " - showCircleRing：显示圆环\n";

    [Header("Game Objects | 游戏物体")]
    public Transform cylinder;

    [Header("Cube Touching | 与均值Y对齐的物体")]
    public Transform cubeTouching;

    [Header("Finger Cubes | 手指方块（5个小方块）")]
    public Transform[] fingers;  // 必须包含5个有效项

    [Header("Cube Points | 方块表面中心点")]
    public Transform CubePoint_DA;
    public Transform CubePoint_SHI;
    public Transform CubePoint_ZHONG;
    public Transform CubePoint_WU;
    public Transform CubePoint_XIAO;

    // 投影点（世界坐标）
    private Vector3 ObjectPoint_DA, ObjectPoint_SHI, ObjectPoint_ZHONG, ObjectPoint_WU, ObjectPoint_XIAO;

    // 与 5 个小方块 Y 均值等高、落在 cylinder 轴线上的中心点（世界坐标）
    private Vector3 localYCenterWorld;

    // 投影圆环半径（cylinder 局部空间）
    private float fixedRadius = 0.015f;

    [Header("Visual Display Point Radius | 点的可视化小球半径")]
    private float gizmoSphereRadius = 0.001f;

    [Header("Value Of Slipping DistanceY | Y方向滑动数值")]
    public float DistanceDA;
    public float DistanceSHI;
    public float DistanceZHONG;
    public float DistanceWU;
    public float DistanceXIAO;

    [Header("Gizmos Display Settings | Gizmos显示设置")]
    public bool showCenterPoint = true;
    public bool showCubePoints = true;
    public bool showProjectedPoints = true;
    public bool showConnectingLines = true;
    public bool showYProjectionLines = true;
    public bool showCircleRing = true;

    [Header("Gizmos Colors | Gizmo颜色设置")]
    private Color centerPointColor = Color.white;
    private Color cubePointColor = Color.red;
    private Color projectedPointColor = Color.cyan;
    private Color connectingLineColor = Color.cyan;
    private Color yProjectionLineColor = Color.red;
    private Color ringColor = Color.magenta;

    void Update()
    {
        if (!Ready()) return;

        // 1) 用 5 个小方块的 Y 均值作为圆环中心 Y（实时，无平滑）
        float avgY = AverageFingerY();
        Vector3 localYAxis = cylinder.up;
        float deltaY = avgY - cylinder.position.y;
        localYCenterWorld = cylinder.position + localYAxis * deltaY;

        // 2) 同步 cubeTouching 的 Y 到均值
        if (cubeTouching != null)
        {
            Vector3 p = cubeTouching.position;
            p.y = avgY;
            cubeTouching.position = p;
        }

        // 3) 在 cylinder 局部空间做几何计算
        Vector3 localYCenter = cylinder.InverseTransformPoint(localYCenterWorld);

        for (int i = 0; i < 5; i++)
        {
            Transform finger = fingers[i];
            if (finger == null) continue;

            Vector3 localFinger = cylinder.InverseTransformPoint(finger.position);

            Vector2 centerXZ = new Vector2(localYCenter.x, localYCenter.z);
            Vector2 fingerXZ = new Vector2(localFinger.x, localFinger.z);
            Vector2 diff = fingerXZ - centerXZ;
            Vector2 dir = diff.sqrMagnitude > 1e-12f ? diff.normalized : Vector2.right;

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

            Transform cubePoint = GetCubePointByIndex(i);
            if (cubePoint != null)
            {
                float slipDeltaY = projectedWorld.y - cubePoint.position.y;
                if (Mathf.Abs(slipDeltaY) < 0.0001f) slipDeltaY = 0f;
                slipDeltaY = Mathf.Round(slipDeltaY * 1_000_000f) / 1_000_000f;

                switch (i)
                {
                    case 0: DistanceDA = slipDeltaY; break;
                    case 1: DistanceSHI = slipDeltaY; break;
                    case 2: DistanceZHONG = slipDeltaY; break;
                    case 3: DistanceWU = slipDeltaY; break;
                    case 4: DistanceXIAO = slipDeltaY; break;
                }
            }
        }
    }

    private bool Ready()
    {
        return cylinder != null && fingers != null && fingers.Length >= 5
               && fingers[0] != null && fingers[1] != null && fingers[2] != null && fingers[3] != null && fingers[4] != null;
    }

    private float AverageFingerY()
    {
        float sum = fingers[0].position.y + fingers[1].position.y + fingers[2].position.y + fingers[3].position.y + fingers[4].position.y;
        return sum / 5f;
    }

    private Transform GetCubePointByIndex(int i)
    {
        switch (i)
        {
            case 0: return CubePoint_DA;
            case 1: return CubePoint_SHI;
            case 2: return CubePoint_ZHONG;
            case 3: return CubePoint_WU;
            case 4: return CubePoint_XIAO;
            default: return null;
        }
    }

    void OnDrawGizmos()
    {
        if (!Ready()) return;

        // 在 Scene 视图绘制前也用“5个小方块Y均值”刷新一次
        float avgY = AverageFingerY();
        Vector3 localYAxis = cylinder.up;
        float deltaY = avgY - cylinder.position.y;
        localYCenterWorld = cylinder.position + localYAxis * deltaY;

        Vector3[] points = { ObjectPoint_DA, ObjectPoint_SHI, ObjectPoint_ZHONG, ObjectPoint_WU, ObjectPoint_XIAO };
        Transform[] cubePoints = { CubePoint_DA, CubePoint_SHI, CubePoint_ZHONG, CubePoint_WU, CubePoint_XIAO };

        if (showCenterPoint)
        {
            Gizmos.color = centerPointColor;
            Gizmos.DrawSphere(localYCenterWorld, gizmoSphereRadius);
        }

        if (showCubePoints)
        {
            Gizmos.color = cubePointColor;
            for (int i = 0; i < cubePoints.Length; i++)
            {
                if (cubePoints[i] != null)
                    Gizmos.DrawSphere(cubePoints[i].position, gizmoSphereRadius);
            }
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (cubePoints[i] == null) continue;

            Vector3 start = cubePoints[i].position;
            Vector3 end = points[i];

            if (showProjectedPoints)
            {
                Gizmos.color = projectedPointColor;
                Gizmos.DrawSphere(end, gizmoSphereRadius);
            }

            if (showConnectingLines)
            {
                Gizmos.color = connectingLineColor;
                Gizmos.DrawLine(start, end);
            }

            if (showYProjectionLines)
            {
                Vector3 yProjectionStart = new Vector3(start.x, end.y, start.z);
                Gizmos.color = yProjectionLineColor;
                Gizmos.DrawLine(start, yProjectionStart);
            }
        }

        if (showCircleRing)
        {
            Gizmos.color = ringColor;
            int segments = 60;
            Vector3 localYCenter = cylinder.InverseTransformPoint(localYCenterWorld);

            for (int i = 0; i < segments; i++)
            {
                float a1 = Mathf.Deg2Rad * (360f * i / segments);
                float a2 = Mathf.Deg2Rad * (360f * (i + 1) / segments);

                Vector3 local1 = new Vector3(Mathf.Cos(a1) * fixedRadius, 0, Mathf.Sin(a1) * fixedRadius);
                Vector3 local2 = new Vector3(Mathf.Cos(a2) * fixedRadius, 0, Mathf.Sin(a2) * fixedRadius);

                Vector3 world1 = cylinder.TransformPoint(localYCenter + local1);
                Vector3 world2 = cylinder.TransformPoint(localYCenter + local2);
                Gizmos.DrawLine(world1, world2);
            }
        }
    }
}
