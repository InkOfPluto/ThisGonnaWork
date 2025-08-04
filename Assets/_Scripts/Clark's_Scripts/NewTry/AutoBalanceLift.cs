using UnityEngine;

public class AutoBalanceLift : MonoBehaviour
{
    [Header("Cylinder 本体刚体")]
    public Rigidbody cylinderRb;

    [Header("5个施力点（依次为 Thumb, Index, Middle, Ring, Pinky）")]
    public Transform[] points = new Transform[5];

    [Header("总上升力（合力）")]
    public float totalForce = 10f;

    [Header("可视化设置")]
    public Color[] fingerColors = new Color[5]
    {
        Color.red,                             // 拇指
        new Color(1f, 0.5f, 0f),               // 食指（橙色）
        Color.yellow,                          // 中指
        new Color(1f, 0.4f, 0.7f),             // 无名指（粉色）
        new Color(0.6f, 0.4f, 1f)              // 小拇指（紫色）
    };

    public float debugScale = 0.05f;
    public bool showDebug = true;

    void FixedUpdate()
    {
        if (cylinderRb == null || points.Length != 5) return;

        Vector3[] positions = new Vector3[5];
        for (int i = 0; i < 5; i++)
        {
            positions[i] = points[i].position - cylinderRb.worldCenterOfMass;
        }

        // 构建线性方程组 Ax = b
        // x 是每个点上的力 Fi
        // A 为 3x5 矩阵，b 为右侧 3x1 向量
        // 方程组:
        // [ 1 1 1 1 1 ] [F1]   = totalForce
        // [ x1 x2 x3 x4 x5 ]   = 0   （X方向力矩为0）
        // [ z1 z2 z3 z4 z5 ]   = 0   （Z方向力矩为0）

        float[,] A = new float[3, 5];
        float[] b = new float[3] { totalForce, 0f, 0f };

        for (int i = 0; i < 5; i++)
        {
            A[0, i] = 1f;
            A[1, i] = positions[i].x;
            A[2, i] = positions[i].z;
        }

        float[] Fi = SolveMinimumNorm(A, b); // 最小范数解


        // 打印五个点的力值 Fi
        string forceInfo = "Applied Forces [Fi]: ";
        for (int i = 0; i < 5; i++)
        {
            forceInfo += $"F{i + 1} = {Fi[i]:F3}  ";
        }
        Debug.Log(forceInfo);


        // 施加力
        for (int i = 0; i < 5; i++)
        {
            Vector3 force = Vector3.up * Fi[i];
            cylinderRb.AddForceAtPosition(force, points[i].position, ForceMode.Force);

            if (showDebug)
            {
                Debug.DrawRay(points[i].position, force * debugScale, fingerColors[i]);
            }
        }
    }

    // 求最小范数解的函数：使用广义逆 Moore-Penrose Pseudo-inverse
    float[] SolveMinimumNorm(float[,] A, float[] b)
    {
        // 用 Unity 的 Matrix4x4 不方便，直接手动解一个小矩阵系统
        // 使用正规方程 A^T A x = A^T b 求最小二乘解

        int rows = A.GetLength(0); // 3
        int cols = A.GetLength(1); // 5

        float[,] ATA = new float[cols, cols];
        float[] ATb = new float[cols];

        // 计算 ATA
        for (int i = 0; i < cols; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                ATA[i, j] = 0f;
                for (int k = 0; k < rows; k++)
                {
                    ATA[i, j] += A[k, i] * A[k, j];
                }
            }
        }

        // 计算 ATb
        for (int i = 0; i < cols; i++)
        {
            ATb[i] = 0f;
            for (int k = 0; k < rows; k++)
            {
                ATb[i] += A[k, i] * b[k];
            }
        }

        // 解 ATA * x = ATb，使用高斯消元
        return SolveLinearSystem(ATA, ATb);
    }

    // 解线性方程组 Ax = b（高斯消元）
    float[] SolveLinearSystem(float[,] A, float[] b)
    {
        int n = b.Length;
        float[,] M = new float[n, n + 1];

        // 构造增广矩阵
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) M[i, j] = A[i, j];
            M[i, n] = b[i];
        }

        // 高斯消元
        for (int i = 0; i < n; i++)
        {
            // 主元归一
            float div = M[i, i];
            for (int j = 0; j <= n; j++) M[i, j] /= div;

            // 消去
            for (int k = 0; k < n; k++)
            {
                if (k == i) continue;
                float factor = M[k, i];
                for (int j = 0; j <= n; j++) M[k, j] -= factor * M[i, j];
            }
        }

        // 得到解
        float[] x = new float[n];
        for (int i = 0; i < n; i++) x[i] = M[i, n];
        return x;
    }
}
