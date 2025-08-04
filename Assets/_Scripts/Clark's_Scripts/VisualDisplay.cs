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

    [Header("Gizmos Display Settings | Gizmos显示设置")]
    public bool showCenterPoint = true;
    public bool showCubePoints = true;
    public bool showProjectedPoints = true;
    public bool showConnectingLines = true;
    public bool showYProjectionLines = true;
    public bool showCircleRing = true;

    [Header("Gizmos Colors | Gizmo颜色设置")]
    public Color centerPointColor = Color.red;
    public Color cubePointColor = Color.green;
    public Color projectedPointColor = Color.cyan;
    public Color connectingLineColor = Color.cyan;
    public Color yProjectionLineColor = Color.red;
    public Color ringColor = Color.magenta;

    void Update()
    {
        if (cubeCenter == null || cylinder == null || fingers.Length < 5)
            return;

        float cubeY = cubeCenter.position.y;
        Vector3 localYAxis = cylinder.up;
        float deltaY = cubeY - cylinder.position.y;
        localYCenterWorld = cylinder.position + localYAxis * deltaY;

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

            Transform cubePoint = GetCubePointByIndex(i);
            if (cubePoint != null)
            {
                float slipDeltaY = projectedWorld.y - cubePoint.position.y;
                if (Mathf.Abs(slipDeltaY) < 0.001f) slipDeltaY = 0f;
                slipDeltaY = Mathf.Round(slipDeltaY * 1000f) / 1000f;

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
        if (cylinder == null || fingers == null || fingers.Length < 5)
            return;

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
}
