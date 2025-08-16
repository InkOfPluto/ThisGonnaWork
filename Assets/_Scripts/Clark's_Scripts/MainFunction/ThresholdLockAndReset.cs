using UnityEngine;
using System.Collections;
using System.Linq;

public class ThresholdLockAndReset : MonoBehaviour
{
    private Rigidbody rb;

    [ReadOnly, SerializeField] private bool inThreshold = false;
    [ReadOnly, SerializeField] private bool kinematicFlipInProgress = false;

    private readonly Vector3 targetPos = new Vector3(0f, 0f, 0.3f);
    private readonly Quaternion targetRot = Quaternion.identity;

    private const float posEpsilon = 1e-3f;
    private const float angleEpsilon = 0.1f;

    [ReadOnly, SerializeField] private bool hasDeviation = false;

    // 可选：直接指定阈值触发器，避免 Tag/父子节点混淆
    [SerializeField] private Collider thresholdTrigger;

    private Collider selfCol;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        selfCol = GetComponent<Collider>();

        // --- 关键补丁①：场景一开始就处于触发器中时，主动判定一次 ---
        if (thresholdTrigger != null && selfCol != null)
        {
            // 用 ClosestPoint 判定是否在触发器内部（点在内部时返回点本身）
            Vector3 c = selfCol.bounds.center;
            Vector3 closest = thresholdTrigger.ClosestPoint(c);
            inThreshold = (closest == c);
        }
        else
        {
            // 没显式指定时，用重叠查询 + Tag 兜底
            Collider[] hits = Physics.OverlapSphere(transform.position, 0.01f, ~0, QueryTriggerInteraction.Collide);
            inThreshold = hits.Any(h => h.CompareTag("Threshold"));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsThreshold(other))
        {
            inThreshold = true;
            SnapAndFreeze();
        }
    }

    // --- 关键补丁②：只要仍在触发器里，每帧都把 inThreshold 维持为 true ---
    private void OnTriggerStay(Collider other)
    {
        if (IsThreshold(other))
        {
            inThreshold = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsThreshold(other))
        {
            inThreshold = false;
            rb.constraints = RigidbodyConstraints.None;
        }
    }

    private bool IsThreshold(Collider other)
    {
        if (thresholdTrigger != null) return other == thresholdTrigger;
        return other != null && other.CompareTag("Threshold");
    }

    private void SnapAndFreeze()
    {
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

    private void FixedUpdate()
    {
        if (!inThreshold || rb == null) return;

        Vector3 p = transform.position;
        bool posDeviate = Mathf.Abs(p.x - targetPos.x) > posEpsilon ||
                          Mathf.Abs(p.z - targetPos.z) > posEpsilon;

        float angle = Quaternion.Angle(transform.rotation, targetRot);
        bool rotDeviate = angle > angleEpsilon;

        hasDeviation = posDeviate || rotDeviate;

        if (hasDeviation && !kinematicFlipInProgress)
        {
            StartCoroutine(FlipKinematicOneFrame());
        }
    }

    private IEnumerator FlipKinematicOneFrame()
    {
        kinematicFlipInProgress = true;
        bool prevKinematic = rb.isKinematic;
        rb.isKinematic = true;

        Vector3 alignedPos = transform.position;
        alignedPos.x = targetPos.x;
        alignedPos.z = targetPos.z;
        transform.position = alignedPos;
        transform.rotation = targetRot;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        yield return new WaitForFixedUpdate();

        rb.isKinematic = prevKinematic;
        kinematicFlipInProgress = false;
    }
}
