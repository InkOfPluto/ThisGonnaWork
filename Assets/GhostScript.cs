using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostScript : MonoBehaviour
{

    public Transform vrHand;
    public Transform robotHand; 
    public Vector3 positionOffset;
    public Vector3 rotationalOffset;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(vrHand != null && robotHand != null)
        {
            //Positional offset
            Vector3 robotPosOffset = vrHand.transform.position + positionOffset;

            // // Rotational offset
            Vector3 robotRotOffset = vrHand.transform.rotation.eulerAngles + rotationalOffset;

            //Apply offsets to robot hand
            robotHand.position = robotPosOffset;
            robotHand.rotation = Quaternion.Euler(robotRotOffset);
        }
    }
/* TODO:
MAPPING FUNCTION FROM HUMAN HAND TO ROBOT HAND
*/
}
