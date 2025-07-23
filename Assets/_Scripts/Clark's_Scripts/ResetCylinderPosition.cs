using UnityEngine;
using System.Collections.Generic;

public class ResetCylinderPosition : MonoBehaviour
{
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Rigidbody rb;

    public GameObject tableObject; // 👉 拖入 Table
    public List<GameObject> fingerCubes; // 👉 拖入五个 Cube（手指）

    private bool isTouchingTable = false;
    private HashSet<GameObject> touchingCubes = new HashSet<GameObject>();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    public void ResetPosition()
    {
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = initialPosition;
        transform.rotation = initialRotation;
        Debug.Log("[✅] Cylinder 已重置");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPosition();
        }

        //UpdateFreezeConstraints();
    }

    //private void UpdateFreezeConstraints()
    //{
    //    float yPos = transform.position.y;

    //    // 🥇 优先级最高：接触任意手指 → 解锁 Freeze
    //    if (touchingCubes.Count > 0)
    //    {
    //        if (rb.constraints != RigidbodyConstraints.None)
    //        {
    //            rb.constraints = RigidbodyConstraints.None;
    //            //Debug.Log("[🧊] Freeze：❌（接触手指）");
    //        }
    //        return;
    //    }

    //    // 🥈 第二优先级：接触桌子 且 Y ≤ 0.75 → FreezeAll
    //    if (isTouchingTable && yPos <= 0.75f)
    //    {
    //        if (rb.constraints != RigidbodyConstraints.FreezeAll)
    //        {
    //            rb.constraints = RigidbodyConstraints.FreezeAll;
    //            //Debug.Log("[🧊] Freeze：✔️（接触桌面 且位置 ≤ 0.75 且未接触手指）");
    //        }
    //        return;
    //    }

    //    // 🥉 否则 → 解锁 Freeze
    //    if (rb.constraints != RigidbodyConstraints.None)
    //    {
    //        rb.constraints = RigidbodyConstraints.None;
    //        //Debug.Log("[🧊] Freeze：❌（其他情况）");
    //    }
    //}

    private void OnCollisionEnter(Collision collision)
    {
        GameObject other = collision.gameObject;

        if (other.name == "Floor")
        {
            ResetPosition();
        }

        if (other == tableObject)
        {
            isTouchingTable = true;
        }

        if (fingerCubes.Contains(other))
        {
            touchingCubes.Add(other);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        GameObject other = collision.gameObject;

        if (other == tableObject)
        {
            isTouchingTable = false;
        }

        if (fingerCubes.Contains(other))
        {
            touchingCubes.Remove(other);
        }
    }
}
