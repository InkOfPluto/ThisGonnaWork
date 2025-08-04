using UnityEngine;

public class AutoBalanceLift : MonoBehaviour
{
    [Header("Cylinder �������")]
    public Rigidbody cylinderRb;

    [Header("5��ʩ���㣨����Ϊ Thumb, Index, Middle, Ring, Pinky��")]
    public Transform[] points = new Transform[5];

    [Header("����������������")]
    public float totalForce = 10f;

    [Header("���ӻ�����")]
    public Color[] fingerColors = new Color[5]
    {
        Color.red,                             // Ĵָ
        new Color(1f, 0.5f, 0f),               // ʳָ����ɫ��
        Color.yellow,                          // ��ָ
        new Color(1f, 0.4f, 0.7f),             // ����ָ����ɫ��
        new Color(0.6f, 0.4f, 1f)              // СĴָ����ɫ��
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

        // �������Է����� Ax = b
        // x ��ÿ�����ϵ��� Fi
        // A Ϊ 3x5 ����b Ϊ�Ҳ� 3x1 ����
        // ������:
        // [ 1 1 1 1 1 ] [F1]   = totalForce
        // [ x1 x2 x3 x4 x5 ]   = 0   ��X��������Ϊ0��
        // [ z1 z2 z3 z4 z5 ]   = 0   ��Z��������Ϊ0��

        float[,] A = new float[3, 5];
        float[] b = new float[3] { totalForce, 0f, 0f };

        for (int i = 0; i < 5; i++)
        {
            A[0, i] = 1f;
            A[1, i] = positions[i].x;
            A[2, i] = positions[i].z;
        }

        float[] Fi = SolveMinimumNorm(A, b); // ��С������


        // ��ӡ��������ֵ Fi
        string forceInfo = "Applied Forces [Fi]: ";
        for (int i = 0; i < 5; i++)
        {
            forceInfo += $"F{i + 1} = {Fi[i]:F3}  ";
        }
        Debug.Log(forceInfo);


        // ʩ����
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

    // ����С������ĺ�����ʹ�ù����� Moore-Penrose Pseudo-inverse
    float[] SolveMinimumNorm(float[,] A, float[] b)
    {
        // �� Unity �� Matrix4x4 �����㣬ֱ���ֶ���һ��С����ϵͳ
        // ʹ�����淽�� A^T A x = A^T b ����С���˽�

        int rows = A.GetLength(0); // 3
        int cols = A.GetLength(1); // 5

        float[,] ATA = new float[cols, cols];
        float[] ATb = new float[cols];

        // ���� ATA
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

        // ���� ATb
        for (int i = 0; i < cols; i++)
        {
            ATb[i] = 0f;
            for (int k = 0; k < rows; k++)
            {
                ATb[i] += A[k, i] * b[k];
            }
        }

        // �� ATA * x = ATb��ʹ�ø�˹��Ԫ
        return SolveLinearSystem(ATA, ATb);
    }

    // �����Է����� Ax = b����˹��Ԫ��
    float[] SolveLinearSystem(float[,] A, float[] b)
    {
        int n = b.Length;
        float[,] M = new float[n, n + 1];

        // �����������
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) M[i, j] = A[i, j];
            M[i, n] = b[i];
        }

        // ��˹��Ԫ
        for (int i = 0; i < n; i++)
        {
            // ��Ԫ��һ
            float div = M[i, i];
            for (int j = 0; j <= n; j++) M[i, j] /= div;

            // ��ȥ
            for (int k = 0; k < n; k++)
            {
                if (k == i) continue;
                float factor = M[k, i];
                for (int j = 0; j <= n; j++) M[k, j] -= factor * M[i, j];
            }
        }

        // �õ���
        float[] x = new float[n];
        for (int i = 0; i < n; i++) x[i] = M[i, n];
        return x;
    }
}
