using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetPose : MonoBehaviour
{
    Vector3 initPos;
    Quaternion initRot;
    Rigidbody rb; 

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>(); 
        initPos = transform.position;
        initRot = transform.rotation; 
    }

    //// Update is called once per frame
    //void Update()
    //{
    //    if (transform.position.y > 5f)
    //    {
    //        transform.position = new Vector3(initPos.x, initPos.y + 0.1f, initPos.z);
    //        transform.rotation = initRot;
    //        print("Reset from height of 5 m"); 
    //    }
    //}

    private void OnCollisionEnter(Collision other)
    {
        if(other.gameObject.name == "Floor")
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero; 
            transform.position = new Vector3(initPos.x, initPos.y + 0.05f, initPos.z);
            transform.rotation = initRot; 
        }
    }
}
