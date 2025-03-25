using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;
using Unity.VisualScripting;

public class SlipSequence : MonoBehaviour
{
    public string Comport1, Comport2; 
    private SerialPort serial1;
    private SerialPort serial2;

    public GameObject targetCube, offsetMass;
    //public ArticulationBody shoulderLink;
    public ArticulationBody tmb, index, mid, ring;
    public float graspDepth = 30f;

    public float targetGrasperHeight = 19;

    float tmbCloseState = 0.05f;
    float indexCloseState = -0.05f;
    float middleCloseState = -0.05f;
    float ringCloseState = 0.05f;

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
            serial1 = new SerialPort(Comport1, 115200);
            serial2 = new SerialPort(Comport2, 115200);

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
            if (graspholdRoutine != null)
            {
                StopCoroutine(graspholdRoutine);
            }
            graspholdRoutine = StartCoroutine(GraspHolderSequence());
        }

        //Mapping VR fingers to digits
        float vrThumbValue = GetVRThumbValue();
        float vrIndexValue = GetVRIndexValue();
        float vrMiddleValue = GetVRMiddleValue();
        float vrRingValue = GetVRRingValue();

        ArticulationDrive thumbDrive = tmb.yDrive;
        thumbDrive.target = mapThumb(vrThumbValue, 0f, 1f, tmbCloseState, 0f);
        tmb.yDrive = thumbDrive;

        ArticulationDrive indexDrive = index.yDrive;
        indexDrive.target = mapIndex(vrIndexValue, 0f, 1f, indexCloseState, 0f);
        index.yDrive = indexDrive;

        ArticulationDrive middleDrive = mid.zDrive;
        middleDrive.target = mapMiddle(vrMiddleValue, 0f, 1f, middleCloseState, 0f);
        mid.zDrive = middleDrive;

        ArticulationDrive ringDrive = ring.zDrive;
        ringDrive.target = mapRing(vrRingValue, 0f, 1f, ringCloseState, 0f);
        ring.zDrive = ringDrive;
    }

    //Placeholder to get VR Finger values
    private float GetVRThumbValue()
    {
        return 0.5f;
    }

    private float GetVRIndexValue()
    {
        return 0.5f;
    }

    private float GetVRMiddleValue()
    {
        return 0.5f;
    }

    private float GetVRRingValue()
    {
        return 0.5f;
    }


    // Special loop (Coroutine) where we can control the sequence of events in a more controlled fashion
    private IEnumerator GraspHolderSequence()
    {
        // Reset everything to the starting conditions
        targetCube.GetComponent<Rigidbody>().isKinematic = true;
        offsetMass.GetComponent<Rigidbody>().isKinematic = true;
        offsetMass.GetComponent<Rigidbody>().mass = originalMass;
        //targetCube.GetComponent<BoxCollider>().enabled = false;
        //targetCube.GetComponent<SphereCollider>().enabled = false;
        targetCube.GetComponent<CapsuleCollider>().enabled = false;
        offsetMass.GetComponent<BoxCollider>().enabled = false;

        ArticulationDrive thumb_digit = tmb.yDrive; // Assuming rotation around x-axis
        thumb_digit.target = 0f;
        tmb.yDrive = thumb_digit;

        ArticulationDrive index_digit = index.yDrive; // Assuming rotation around x-axis
        index_digit.target = 0f;
        index.yDrive = index_digit;

        ArticulationDrive middle_digit = mid.zDrive; // Assuming rotation around x-axis
        middle_digit.target = 0f;
        mid.zDrive = middle_digit;

        ArticulationDrive ring_digit = ring.zDrive; // Assuming rotation around x-axis
        ring_digit.target = 0f;
        ring.zDrive = ring_digit;

        //ArticulationDrive drive_shoulder = shoulderLink.xDrive; // Assuming rotation around x-axis
        //drive_shoulder.target = graspDepth;
        //shoulderLink.xDrive = drive_shoulder;

        yield return new WaitForSeconds(1f);

        targetCube.transform.position = originalPos;
        targetCube.transform.rotation = originalRot;
        offsetMass.transform.position = originalOffsetPos;
        offsetMass.transform.rotation = originalOffsetRot;

        yield return new WaitForSeconds(1f);

        //targetCube.GetComponent<BoxCollider>().enabled = true;
        //targetCube.GetComponent<SphereCollider>().enabled = true;
        targetCube.GetComponent<CapsuleCollider>().enabled = true;
        offsetMass.GetComponent<BoxCollider>().enabled = true;
        targetCube.GetComponent<Rigidbody>().isKinematic = false;
        offsetMass.GetComponent<Rigidbody>().isKinematic = false;

        // --------------------------------------------------------------------------------------------
        // --------------------------------------------------------------------------------------------
        // --------------------------------------------------------------------------------------------

        // 1. Finger A and B to close on the object (to finger close state) 
        thumb_digit = tmb.yDrive; // Assuming rotation around x-axis
        thumb_digit.target = tmbCloseState;
        tmb.yDrive = thumb_digit;

        index_digit = index.yDrive; // Assuming rotation around x-axis
        index_digit.target = indexCloseState;
        index.yDrive = index_digit;

        middle_digit = mid.zDrive; // Assuming rotation around x-axis
        middle_digit.target = middleCloseState;
        mid.zDrive = middle_digit;

        ring_digit = ring.zDrive; // Assuming rotation around x-axis
        ring_digit.target = ringCloseState;
        ring.zDrive = ring_digit;

        // Set a small pause here to give the fingers a bit of time to close
        yield return new WaitForSeconds(1f);

        //// 2. Want shoulder link to lift up to a given target height (targetGrasperHeight)
        //drive_shoulder = shoulderLink.xDrive; // Assuming rotation around x-axis
        //drive_shoulder.target = targetGrasperHeight;
        //shoulderLink.xDrive = drive_shoulder;

        // Set a small pause here to give the arm a bit of time to lift up 
        yield return new WaitForSeconds(1f);

        // 3. Once at the target height, then slowly increase the attached weight to the target object 
        Debug.Log("Start increasing the object's offset mass!");


        while (offsetMass.GetComponent<Rigidbody>().mass < targetOffsetMass)
        {
            offsetMass.GetComponent<Rigidbody>().mass += 1f;

            if (targetCube.GetComponent<Rigidbody>().velocity.y < -0.1f)
            {
                try
                {
                    serial1.WriteLine("fgfgbnbn");
                    serial2.WriteLine("fgfgbnbn");
                    Debug.Log("Slipping!");
                    break;
                }
                catch { print("Something went wrong!"); }
            }

            yield return null;
        }

        Debug.Log("Slip ended!");


        yield return null;

    }
    //MAPPING FUNCTION FROM VR HAND TO ROBOT HAND
    public static float mapIndex(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }
    public static float mapMiddle(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }
    public static float mapRing(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }
    public static float mapThumb(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }
}
