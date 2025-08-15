using UnityEngine;
using System.Collections;

public class ThresholdLockAndReset : MonoBehaviour
{
    private Rigidbody rb;

    private bool inThreshold = false;              // �Ƿ�����ֵ���� / inside threshold flag
    private bool kinematicFlipInProgress = false;  // һ֡Kinematic��ת��־ / guard flag

    private readonly Vector3 targetPos = new Vector3(0f, 0f, 0.3f); // Ŀ��X/Zλ��
    private readonly Quaternion targetRot = Quaternion.identity;    // Ŀ����ת

    private const float posEpsilon = 1e-3f;     // λ���ݲ�
    private const float angleEpsilon = 0.1f;    // �Ƕ��ݲ�

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        // if (rb == null) Debug.LogError("ThresholdLock �ű�Ҫ�������Ϲ��� Rigidbody��");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Threshold"))
        {
            inThreshold = true;

            Vector3 worldPos = transform.position;
            worldPos.x = targetPos.x;
            worldPos.z = targetPos.z;
            transform.position = worldPos;

            transform.rotation = targetRot;

            rb.constraints = RigidbodyConstraints.FreezePositionX |
                             RigidbodyConstraints.FreezePositionZ |
                             RigidbodyConstraints.FreezeRotation;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Threshold"))
        {
            inThreshold = false;
            rb.constraints = RigidbodyConstraints.None;
        }
    }

    private void FixedUpdate()
    {
        if (!inThreshold || rb == null) return;

        Vector3 p = transform.position;
        bool posDeviate = Mathf.Abs(p.x - targetPos.x) > posEpsilon ||
                          Mathf.Abs(p.z - targetPos.z) > posEpsilon;

        float angle = Quaternion.Angle(transform.rotation, targetRot);
        bool rotDeviate = angle > angleEpsilon;

        if ((posDeviate || rotDeviate) && !kinematicFlipInProgress)
        {
            StartCoroutine(FlipKinematicOneFrame());
        }
    }

    private IEnumerator FlipKinematicOneFrame()
    {
        kinematicFlipInProgress = true;

        bool prevKinematic = rb.isKinematic;
        rb.isKinematic = true;

        // **ǿ�ƶ���λ������ת** / Force re-align before the frame
        Vector3 alignedPos = transform.position;
        alignedPos.x = targetPos.x;
        alignedPos.z = targetPos.z;
        transform.position = alignedPos;

        transform.rotation = targetRot;

        // �����ٶ� / reset velocity
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // �ȴ�һ֡���� / wait one physics frame
        yield return new WaitForFixedUpdate();

        rb.isKinematic = prevKinematic;
        kinematicFlipInProgress = false;
    }
}
