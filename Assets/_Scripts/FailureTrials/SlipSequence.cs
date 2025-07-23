using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using Unity.VisualScripting;
using UnityEditor.Experimental.Rendering;
using UnityEngine;

public class SlipSequence : MonoBehaviour
{
    public string Comport1, Comport2; 
    private SerialPort serial1;
    private SerialPort serial2;

    public GameObject targetCube, offsetMass;
    //public ArticulationBody shoulderLink;
    public ArticulationBody tmb, index, mid, ring;
    public float graspDepth = 30f;

    //public float targetGrasperHeight = 19;

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
        catch (Exception e) { print(e); }
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

        //// 2. want shoulder link to lift up to a given target height (targetgrasperheight)
        //drive_shoulder = shoulderlink.xdrive; // assuming rotation around x-axis
        //drive_shoulder.target = targetgrasperheight;
        //shoulderlink.xdrive = drive_shoulder;

        //// set a small pause here to give the arm a bit of time to lift up 
        //yield return new waitforseconds(1f);

        // 3. once at the target height, then slowly increase the attached weight to the target object 
        Debug.Log("start increasing the object's offset mass!");


        while (offsetMass.GetComponent<Rigidbody>().mass < targetOffsetMass)
        {
            offsetMass.GetComponent<Rigidbody>().mass += 1f;

            if (targetCube.GetComponent<Rigidbody>().velocity.y < -0.1f)
            {
                try
                {
                    serial1.WriteLine("fgfgbnbn");
                    serial2.WriteLine("fgfgbnbn");
                    Debug.Log("slipping!");
                    break;
                }
                catch { print("something went wrong!"); }
            }

            yield return null;
        }

        Debug.Log("slip ended!");


        yield return null;

    }
    //mapping function from vr hand to robot hand
    public static float mapIndex(float value, float leftmin, float leftmax, float rightmin, float rightmax)
    {
        return rightmin + (value - leftmin) * (rightmax - rightmin) / (leftmax - leftmin);
    }
    public static float mapMiddle(float value, float leftmin, float leftmax, float rightmin, float rightmax)
    {
        return rightmin + (value - leftmin) * (rightmax - rightmin) / (leftmax - leftmin);
    }
    public static float mapRing(float value, float leftmin, float leftmax, float rightmin, float rightmax)
    {
        return rightmin + (value - leftmin) * (rightmax - rightmin) / (leftmax - leftmin);
    }
    public static float mapThumb(float value, float leftmin, float leftmax, float rightmin, float rightmax)
    {
        return rightmin + (value - leftmin) * (rightmax - rightmin) / (leftmax - leftmin);
    }

    //private void Update()
    //{
    //    if (Input.GetKeyUp(KeyCode.Z))
    //    {
    //        serial1.WriteLine("fffffffffffffffffffffff");
    //    }
    //    if (Input.GetKeyUp(KeyCode.X))
    //    {
    //        serial1.WriteLine("bbbbbbbbbbbbbbbbbbbbb");
    //    }
    //    if (Input.GetKeyUp(KeyCode.C))
    //    {
    //        serial1.WriteLine("gggggggggggggggggggggg");    //Serail1 Ring g clock down 相对于手指向下滑（右边看 顺时针）
    //    }
    //    if (Input.GetKeyUp(KeyCode.V))
    //    {
    //        serial1.WriteLine("nnnnnnnnnnnnnnnnnnnnnnn");   //Serial1 Ring n COUNTERCLOCK UP 相对于手指向上滑（右边看 逆时针）
    //    }
    //    if (Input.GetKeyUp(KeyCode.B))
    //    {
    //        serial2.WriteLine("ffffffffffffffffffff");    //Serail2 Index f clock down 相对于手指向下滑（右边看 顺时针）
    //    }
    //    if (Input.GetKeyUp(KeyCode.N))
    //    {
    //        serial2.WriteLine("bbbbbbbbbbbbbbbbbbbbb");
    //    }
    //    if (Input.GetKeyUp(KeyCode.D))
    //    {
    //        serial2.WriteLine("gggggggggggggggggggggg");    //Serial2 Mid g COUNTERCLOCK UP 相对于手指向上滑（右边看 逆时针）
    //    }
    //    if (Input.GetKeyUp(KeyCode.F))
    //    {
    //        serial2.WriteLine("nnnnnnnnnnnnnnnnnnnnnnn");   //Serail2 Mid n clock down 相对于手指向下滑（右边看 顺时针）
    //    }
    //}
}
