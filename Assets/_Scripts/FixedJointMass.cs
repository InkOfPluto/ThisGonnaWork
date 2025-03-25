using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixedJointMass : MonoBehaviour
{


    GameObject offsetMass = new GameObject(); 

    void Start()
    {
        //Invoke("ConnectMass", 1f);
    }

    void ConnectMass()
    {
        offsetMass = GameObject.FindWithTag("offset");
        GetComponent<FixedJoint>().connectedBody = offsetMass.GetComponent<Rigidbody>();
        offsetMass.GetComponent<Rigidbody>().isKinematic = false;
    }

}
