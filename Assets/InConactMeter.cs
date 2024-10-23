using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InConactMeter : MonoBehaviour
{

    Collider selfCollider; 
    public bool inContact; 

    // Start is called before the first frame update
    void Start()
    {
        selfCollider = GetComponent<Collider>(); 
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Interactable")
            inContact = true; 
    }
    //private void OnCollisionStay(Collision collision)
    //{
    //    if (collision.gameObject.tag == "Interactable")
    //        inContact = true;
    //}
    private void OnCollisionExit(Collision collision)
    {
        inContact = false;
    }
}
