using UnityEngine;

[ExecuteAlways] // ��ʹ��������Ϸ��Ҳ���� Scene ��ͼ����
public class CenterOfMassVisualizer : MonoBehaviour
{
    [Header("����˵�� | Instructions")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
        "���ܣ��� Scene ��ͼ������ʱ���� Rigidbody ������λ�ã����� Gizmo��\n" +
        "Inspector��size ������İ뾶\n" +
        "Inspector��gizmoColor ���� Gizmo ����ɫ\n" +
        "Ҫ�󣺶�������� Rigidbody �����������ʾ\n" +
        "����/�༭ģʽ������ʵʱ��ʾ����λ��\n";

    [Header("Gizmo Settings | Gizmo ����")]
    public float size = 0.01f;                 // ��İ뾶
    public Color gizmoColor = Color.yellow;    // �����ɫ

    private Rigidbody rb;

    void OnDrawGizmos()
    {
        // ��ȡ Rigidbody���༭��ģʽҲ���ԣ�
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // ����
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(rb.worldCenterOfMass, size);
        }
    }
}
