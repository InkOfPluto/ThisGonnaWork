using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlipDetector : MonoBehaviour
{
    public static Vector3 vel; 
    public int fingerID = 0; // Thumb


    private void OnTriggerStay(Collider other)
    {
        if(other.gameObject.tag=="targetCube")
        {
            vel = other.gameObject.GetComponent<Rigidbody>().velocity; 
        }
    }


}
