using UnityEngine;

public class ThresholdLock : MonoBehaviour
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
