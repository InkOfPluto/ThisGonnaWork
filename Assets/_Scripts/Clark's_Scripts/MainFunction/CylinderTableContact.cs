using UnityEngine;
using System;

public class CylinderTableContact : MonoBehaviour
{
    // ��̬�¼�����Ľű������� CylinderTableContact.OnCylinderTouchTable += ���� ������
    public static event Action OnCylinderTouchTable;

    // �������뿪�¼�
    public static event Action OnCylinderLeaveTable;

    [SerializeField] private string tableTag = "Table"; // Table �� Tag

    private void OnCollisionEnter(Collision collision)
    {
        // ����Ƿ����� Table
        if (collision.gameObject.CompareTag(tableTag))
        {
            Debug.Log("Cylinder �� Table �Ӵ�");
            OnCylinderTouchTable?.Invoke(); // �����Ӵ��¼�
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        // ����Ƿ����뿪 Table
        if (collision.gameObject.CompareTag(tableTag))
        {
            Debug.Log("Cylinder �뿪 Table");
            OnCylinderLeaveTable?.Invoke(); // �����뿪�¼�
        }
    }
}
