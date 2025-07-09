using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForceCylender : MonoBehaviour
{
    public GameObject Object;
    public GameObject Fingertip1;
    public GameObject Fingertip2;
    public GameObject Fingertip3;
    public float forceMagnitude = 1f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        Vector3 forceDirection1 = (Object.transform.position - this.transform.position).normalized;
        Vector3 force = forceDirection1 * forceMagnitude;
        rb.AddForce(force, ForceMode.Force);
    }
}
