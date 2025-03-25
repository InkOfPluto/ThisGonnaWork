using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateSqueezeForce : MonoBehaviour
{
    public Transform finger_physics;
    public Transform finger_real;
    public UDP_Comms udpComms;

    private Vector2 valueRange;
    private float minFingDistVal = 0.005f, maxFingDistVal = 0.05f, fingerDistVal = 0f;
    private float mappedVals;

    protected Queue<float> filterDataQueue = new Queue<float>();
    public int filterLength = 3;
    private int iterCounter = 0;
    private float filteredFingerData;


    public GameObject fingerTip_1;
    private InConactMeter incontactmet;

    private void Start()
    {
        incontactmet = fingerTip_1.GetComponent<InConactMeter>();
    }

    void Update()
    {
        fingerDistVal = Vector3.Distance(finger_real.position, finger_physics.position);
        print("Distance: " + fingerDistVal);
        float mappedFingerDistance = map(fingerDistVal, minFingDistVal, maxFingDistVal, 0f, 1f);

        if (iterCounter < filterLength)
        {
            filteredFingerData += mappedFingerDistance;
            iterCounter++;
        }
        else
        {
            if (incontactmet.inContact)
            {
                filteredFingerData /= filterLength;

                valueRange = new Vector2(15, 165); // Hand typed 
                mappedVals = map(filteredFingerData, 0f, 1f, valueRange.x, valueRange.y);
                //Physics.defaultMaxAngularSpeed = Mathf.RoundToInt(mappedVals);

                udpComms.SendMsgtoWrist(mappedVals);

                iterCounter = 0;
            }
        }


    }

    public static float map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }
}
