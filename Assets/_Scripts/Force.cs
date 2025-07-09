using UnityEngine;

public class ApplyOpposingForces : MonoBehaviour
{
    public GameObject Object;
    public float forceMagnitude = 1f;


    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        Vector3 forceDirection = (Object.transform.position - this.transform.position).normalized;
        Vector3 force = forceDirection * forceMagnitude;
        rb.AddForce(force,ForceMode.Force);
    }
}
