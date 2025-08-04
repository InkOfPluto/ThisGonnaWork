using UnityEngine;

public class BalancedForceFromAngles : MonoBehaviour
{
    [Header("Cylinder 设置")]
    public Rigidbody cylinderRb;
    public Transform cylinderTransform;

    [Header("施力大小")]
    public float F = 1f; // 非拇指施加的力

    [Header("施力点半径")]
    public float radius = 0.03f;

    [Header("五个角度（单位：度）")]
    public float[] anglesDeg = new float[5] { 20f, 95f, 160f, 225f, 290f };

    [Header("颜色：Thumb → Pinky")]
    public Color[] fingerColors = new Color[5]
    {
        Color.red,                            // Thumb
        new Color(1f, 0.5f, 0f),              // Index
        Color.yellow,                         // Middle
        new Color(1f, 0.4f, 0.7f),            // Ring
        new Color(0.6f, 0.4f, 1f)             // Pinky
    };

    [Header("调试选项")]
    public bool showDebug = true;
    public float debugScale = 0.05f;
    public float gizmoSize = 0.005f;

    private Vector3[] pointPositions = new Vector3[5];

    void FixedUpdate()
    {
        if (cylinderRb == null || cylinderTransform == null) return;

        Vector3 center = cylinderRb.worldCenterOfMass;

        // Step 1: 计算5个点在圆上的位置（相对 cylinder）
        for (int i = 0; i < 5; i++)
        {
            float thetaRad = anglesDeg[i] * Mathf.Deg2Rad;
            float x = radius * Mathf.Cos(thetaRad);
            float z = radius * Mathf.Sin(thetaRad);
            pointPositions[i] = cylinderTransform.TransformPoint(new Vector3(x, 0f, z)); // 本地 → 世界
        }

        // Step 2: 获取相对质心位置
        Vector3 r_T = pointPositions[0] - center;
        Vector3[] r_other = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            r_other[i] = pointPositions[i + 1] - center;
        }

        // Step 3: 计算 F_T / F
        float sumX = 0f, sumZ = 0f;
        foreach (Vector3 r in r_other)
        {
            sumX += r.x;
            sumZ += r.z;
        }

        float FT_over_F_x = -sumX / r_T.x;
        float FT_over_F_z = -sumZ / r_T.z;
        float FT_over_F = (Mathf.Abs(FT_over_F_x - FT_over_F_z) < 1e-4f) ? FT_over_F_x : (FT_over_F_x + FT_over_F_z) / 2f;
        float FT = FT_over_F * F;

        // Step 4: 施加力
        ApplyForceAt(pointPositions[0], Vector3.up * FT, fingerColors[0]); // Thumb
        for (int i = 1; i < 5; i++)
        {
            ApplyForceAt(pointPositions[i], Vector3.up * F, fingerColors[i]);
        }

        Debug.Log($"[BalancedForce] 角度: {string.Join(", ", anglesDeg)} | FT/F ≈ {FT_over_F:F3}");
    }

    void ApplyForceAt(Vector3 worldPos, Vector3 force, Color color)
    {
        cylinderRb.AddForceAtPosition(force, worldPos, ForceMode.Force);
        if (showDebug)
        {
            Debug.DrawRay(worldPos, force * debugScale, color);
        }
    }

    // ✅ 可视化施力点位置（Scene 视图）
    void OnDrawGizmos()
    {
        if (cylinderTransform == null || anglesDeg.Length != 5 || fingerColors.Length != 5) return;

        Gizmos.color = Color.white;
        Vector3 origin = cylinderTransform.position;

        for (int i = 0; i < 5; i++)
        {
            float thetaRad = anglesDeg[i] * Mathf.Deg2Rad;
            float x = radius * Mathf.Cos(thetaRad);
            float z = radius * Mathf.Sin(thetaRad);
            Vector3 localPos = new Vector3(x, 0f, z);
            Vector3 worldPos = cylinderTransform.TransformPoint(localPos);

            Gizmos.color = fingerColors[i];
            Gizmos.DrawSphere(worldPos, gizmoSize);
        }
    }
}
