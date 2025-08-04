using UnityEngine;

public class FingerOffsetLogger : MonoBehaviour
{
    [Header("Cylinder ����")]
    public Rigidbody cylinderRb;

    [Header("�����ָ�㣨Thumb, Index, Middle, Ring, Pinky��")]
    public Transform[] points = new Transform[5];

    private readonly string[] fingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

    void Start()
    {
        if (points.Length != 5 || cylinderRb == null)
        {
            Debug.LogError("��ȷ�������� 5 ����ָ�� �� Cylinder Rigidbody��");
            return;
        }

        LogFingerOffsets();
    }

    void LogFingerOffsets()
    {
        Vector3 center = cylinderRb.worldCenterOfMass;

        Debug.Log("=== ��ָ�����Բ�����ĵ�ƫ������X, Z�� ===");
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 offset = points[i].position - center;
            Debug.Log($"{fingerNames[i]}: x = {offset.x:F4}, z = {offset.z:F4}");
        }
    }
}
