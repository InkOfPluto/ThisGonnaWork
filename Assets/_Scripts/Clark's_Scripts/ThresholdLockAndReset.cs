using UnityEngine;

public class ThresholdLockAndReset : MonoBehaviour
{
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            //Debug.LogError("ThresholdLock �ű�Ҫ�������Ϲ��� Rigidbody��");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Threshold"))
        {
            // ��ȡ��ǰ����λ��
            Vector3 worldPos = transform.position;

            // �޸� X �� Z������ Y
            worldPos.x = 0f;
            worldPos.z = 0.3f;
            transform.position = worldPos;

            // ������ת
            transform.rotation = Quaternion.identity;

            //Debug.Log("Cylinder ���� Threshold������ X/Z λ�ú�������ת");
            rb.constraints = RigidbodyConstraints.FreezePositionX |
                             RigidbodyConstraints.FreezePositionZ |
                             RigidbodyConstraints.FreezeRotation;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Threshold"))
        {
            //Debug.Log("Cylinder �뿪 Threshold��������������");
            rb.constraints = RigidbodyConstraints.None;
        }
    }
}
