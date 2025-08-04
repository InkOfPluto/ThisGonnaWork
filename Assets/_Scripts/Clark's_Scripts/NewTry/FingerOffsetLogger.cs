using UnityEngine;

public class FingerOffsetLogger : MonoBehaviour
{
    [Header("Cylinder 刚体")]
    public Rigidbody cylinderRb;

    [Header("五个手指点（Thumb, Index, Middle, Ring, Pinky）")]
    public Transform[] points = new Transform[5];

    private readonly string[] fingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

    void Start()
    {
        if (points.Length != 5 || cylinderRb == null)
        {
            Debug.LogError("请确保设置了 5 个手指点 和 Cylinder Rigidbody！");
            return;
        }

        LogFingerOffsets();
    }

    void LogFingerOffsets()
    {
        Vector3 center = cylinderRb.worldCenterOfMass;

        Debug.Log("=== 手指相对于圆柱质心的偏移量（X, Z） ===");
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 offset = points[i].position - center;
            Debug.Log($"{fingerNames[i]}: x = {offset.x:F4}, z = {offset.z:F4}");
        }
    }
}
