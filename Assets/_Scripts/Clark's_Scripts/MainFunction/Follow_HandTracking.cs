using System.Collections.Generic;
using UnityEngine;

public class Follow_HandTracking : MonoBehaviour
{
    [Header("中心与目标")]
    public Transform cubeCenter; // 圆心
    public Transform handCenter; // 手掌中心（参考点）

    [Header("仅跟踪的大拇指Tip")]
    public Transform rThumbTip; // 只用它来决定方向

    [Header("对应的五个Cube")]
    public Transform fingerDa; // 大拇指
    public Transform fingerShi; // 食指
    public Transform fingerZhong; // 中指
    public Transform fingerWu; // 无名指
    public Transform fingerXiao; // 小拇指

    [Header("旋转半径")]
    public float radius = 0.05f;

    [Header("大拇指跟随角速度(度/秒)——调小=更慢")]
    public float thumbAngularSpeedDegPerSec = 180f;

    [Header("旋转固定高度")]
    public float fixedHeight = 0.75f;

    // 其余四指（跟随者）
    private List<Transform> followers;

    // 记录：每个"跟随者"相对大拇指在圆周上的初始角度差（单位：度）
    private readonly Dictionary<Transform, float> angleOffsetsDeg = new Dictionary<Transform, float>();

    // 平滑后的"大拇指当前角度"（度）
    private float thumbAngleDegSmoothed;

    void Start()
    {
        followers = new List<Transform> { fingerShi, fingerZhong, fingerWu, fingerXiao };

        float thumbAngleDegInit = GetAngleDegOnXZ(fingerDa.position);
        thumbAngleDegSmoothed = thumbAngleDegInit; // 初始即为当前，避免开场抖动

        foreach (var f in followers)
        {
            if (f == null) continue;

            float fAngleDeg = GetAngleDegOnXZ(f.position);
            float offset = Mathf.DeltaAngle(thumbAngleDegInit, fAngleDeg); // follower - thumb（正确号向）
            angleOffsetsDeg[f] = offset;
        }
    }

    void Update()
    {
        if (cubeCenter == null || handCenter == null || rThumbTip == null || fingerDa == null)
            return;

        // 1) 目标角：由 rThumbTip 相对 handCenter 决定（XZ平面）
        Vector3 dirThumbXZ = rThumbTip.position - handCenter.position;
        dirThumbXZ.y = 0f;
        if (dirThumbXZ.sqrMagnitude < 1e-8f) return;
        dirThumbXZ.Normalize();

        float thumbAngleDegTarget = Mathf.Atan2(dirThumbXZ.z, dirThumbXZ.x) * Mathf.Rad2Deg;

        // 2) 平滑追踪角度
        float maxStep = thumbAngularSpeedDegPerSec * Time.deltaTime;
        thumbAngleDegSmoothed = Mathf.MoveTowardsAngle(thumbAngleDegSmoothed, thumbAngleDegTarget, maxStep);

        // 3) 用平滑后的角度摆放大拇指（Y 固定）
        float thumbRad = thumbAngleDegSmoothed * Mathf.Deg2Rad;
        Vector3 thumbTarget = new Vector3(
            cubeCenter.position.x + Mathf.Cos(thumbRad) * radius,
            fixedHeight, // 固定高度
            cubeCenter.position.z + Mathf.Sin(thumbRad) * radius
        );
        MoveAndLookAtCenterSafely(fingerDa, thumbTarget);

        // 4) 其余四指
        foreach (var f in followers)
        {
            if (f == null) continue;

            float offsetDeg = angleOffsetsDeg.TryGetValue(f, out var val) ? val : 0f;
            float angleDeg = thumbAngleDegSmoothed + offsetDeg;
            float rad = angleDeg * Mathf.Deg2Rad;

            Vector3 followerTarget = new Vector3(
                cubeCenter.position.x + Mathf.Cos(rad) * radius,
                fixedHeight, // 固定高度
                cubeCenter.position.z + Mathf.Sin(rad) * radius
            );
            MoveAndLookAtCenterSafely(f, followerTarget);
        }


    }

    // —— 工具函数 —— //

    private float GetAngleDegOnXZ(Vector3 worldPos)
    {
        if (cubeCenter == null) return 0f;

        Vector3 v = worldPos - cubeCenter.position;
        v.y = 0f;
        if (v.sqrMagnitude < 1e-8f) return 0f;

        return Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
    }

    private void MoveAndLookAtCenterSafely(Transform t, Vector3 targetPos)
    {
        if (t == null) return;

        t.position = targetPos;

        ArticulationBody ab = t.GetComponent<ArticulationBody>();
        bool wasEnabled = ab != null && ab.enabled;

        if (ab != null) ab.enabled = false;

        Vector3 lookTarget = cubeCenter.position;
        lookTarget.y = t.position.y; // 保持水平朝向
        t.LookAt(lookTarget);

        if (ab != null) ab.enabled = wasEnabled;
    }
}