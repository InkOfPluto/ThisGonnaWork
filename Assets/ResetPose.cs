using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetPose : MonoBehaviour
{
    Vector3 initPos;
    Quaternion initRot; 

    // Start is called before the first frame update
    void Start()
    {
        initPos = transform.position;
        initRot = transform.rotation; 
    }

    // Update is called once per frame
    //void Update()
    //{
    //    if(transform.position.y < (initPos.y - (initPos.y*0.5f)))
    //    {
    //        transform.position = new Vector3(initPos.x, initPos.y + 0.1f, initPos.z); 
    //    }
    //}

    private void OnCollisionEnter(Collision other)
    {
        if(other.gameObject.name == "Floor")
        {
            transform.position = new Vector3(initPos.x, initPos.y + 0.1f, initPos.z);
        }

    }
}
