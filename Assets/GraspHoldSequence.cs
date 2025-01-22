using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;
using Unity.VisualScripting;

public class GraspHoldSequence : MonoBehaviour
{

    private SerialPort serial1 = new SerialPort("COM3", 115200);
    private SerialPort serial2 = new SerialPort("COM4", 115200);

    public GameObject targetCube, offsetMass;
    public ArticulationBody shoulderLink;
    public ArticulationBody fingerA, fingerB;

    public float targetGrasperHeight = 20;

    float fingerACloseState = -0.025f; // Open state being 0 for both fingers 
    float fingerBCloseState = 0.025f;

    Coroutine graspholdRoutine;

    public float targetOffsetMass = 2f;
    float originalMass; 
    Vector3 originalPos, originalOffsetPos; 
    Quaternion originalRot, originalOffsetRot; 

    void Start()
    {
        originalMass = offsetMass.GetComponent<Rigidbody>().mass;
        originalPos = targetCube.transform.position;
        originalRot = targetCube.transform.rotation;
        originalOffsetPos = offsetMass.transform.position;
        originalOffsetRot = offsetMass.transform.rotation;

        try
        {
            serial1.Open();
            serial2.Open();
        }
        catch { print("Something went wrong!"); }
    }

    void Update()
    {
        //Debug.Log("Velocity: " + targetCube.GetComponent<Rigidbody>().velocity);

        if (Input.GetKeyDown(KeyCode.S))
        {
            if(graspholdRoutine != null)
            {
                StopCoroutine(graspholdRoutine); 
            }
            graspholdRoutine = StartCoroutine(GraspHoldSeuqnce());
        }

        //if(Input.GetKeyDown(KeyCode.F))
        //{
        //    serial.WriteLine("ffff"); 
        //}
        //if (Input.GetKeyDown(KeyCode.B))
        //{
        //    serial.WriteLine("b");
        //}
    }

    // Special loop (Coroutine) where we can control the sequence of events in a more controlled fashion
    private IEnumerator GraspHoldSeuqnce()
    {
        // Reset everything to the starting conditions
        targetCube.GetComponent<BoxCollider>().enabled = false;
        offsetMass.GetComponent<BoxCollider>().enabled = false;
        targetCube.GetComponent<Rigidbody>().isKinematic = true;
        offsetMass.GetComponent<Rigidbody>().isKinematic = true;
        offsetMass.GetComponent<Rigidbody>().mass = originalMass;

        ArticulationDrive drive_fingA = fingerA.zDrive; // Assuming rotation around x-axis
        drive_fingA.target = 0f;
        fingerA.zDrive = drive_fingA;

        ArticulationDrive drive_fingB = fingerB.zDrive; // Assuming rotation around x-axis
        drive_fingB.target = 0f;
        fingerB.zDrive = drive_fingB;

        ArticulationDrive drive_shoulder = shoulderLink.xDrive; // Assuming rotation around x-axis
        drive_shoulder.target = 23f;
        shoulderLink.xDrive = drive_shoulder;

        yield return new WaitForSeconds(1f);

        targetCube.transform.position = originalPos;
        targetCube.transform.rotation = originalRot;
        offsetMass.transform.position = originalOffsetPos;
        offsetMass.transform.rotation = originalOffsetRot;

        yield return new WaitForSeconds(1f);

        targetCube.GetComponent<BoxCollider>().enabled = true;
        offsetMass.GetComponent<BoxCollider>().enabled = true;
        targetCube.GetComponent<Rigidbody>().isKinematic = false;
        offsetMass.GetComponent<Rigidbody>().isKinematic = false;

        // --------------------------------------------------------------------------------------------
        // --------------------------------------------------------------------------------------------
        // --------------------------------------------------------------------------------------------

        // 1. Finger A and B to close on the object (to finger close state) 
        drive_fingA = fingerA.zDrive; // Assuming rotation around x-axis
        drive_fingA.target = fingerACloseState;
        fingerA.zDrive = drive_fingA;

        drive_fingB = fingerB.zDrive; // Assuming rotation around x-axis
        drive_fingB.target = fingerBCloseState;
        fingerB.zDrive = drive_fingB;

        // Set a small pause here to give the fingers a bit of time to close
        yield return new WaitForSeconds(1f); 

        // 2. Want shoulder link to lift up to a given target height (targetGrasperHeight)
        drive_shoulder = shoulderLink.xDrive; // Assuming rotation around x-axis
        drive_shoulder.target = targetGrasperHeight;
        shoulderLink.xDrive = drive_shoulder;

        // Set a small pause here to give the arm a bit of time to lift up 
        yield return new WaitForSeconds(1f);

        // 3. Once at the target height, then slowly increase the attached weight to the target object 
        Debug.Log("Start increasing the object's offset mass!");
        

        while(offsetMass.GetComponent<Rigidbody>().mass < targetOffsetMass)
        {
            offsetMass.GetComponent<Rigidbody>().mass += 0.1f;

            if(targetCube.GetComponent<Rigidbody>().velocity.y < -0.1f)
            {
                try
                {
                    serial1.WriteLine("fgfgbnbn");
                    Debug.Log("Slipping!");
                    break;

                    serial2.WriteLine("fgfgbnbn");
                    Debug.Log("Slipping");
                    break;
                }
                catch { print("Something went wrong!"); }


            }

            yield return null; 
        }

        Debug.Log("Slip ended!"); 


        yield return null; 
    }
}
