using UnityEngine;

public class ThresholdLock : MonoBehaviour
{
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            //Debug.LogError("ThresholdLock 脚本要求物体上挂有 Rigidbody！");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Threshold"))
        {
            //Debug.Log("Cylinder 进入 Threshold，锁定 X/Z 位置和所有旋转");
            rb.constraints = RigidbodyConstraints.FreezePositionX |
                             RigidbodyConstraints.FreezePositionZ |
                             RigidbodyConstraints.FreezeRotation;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Threshold"))
        {
            //Debug.Log("Cylinder 离开 Threshold，解锁所有限制");
            rb.constraints = RigidbodyConstraints.None;
        }
    }
}
