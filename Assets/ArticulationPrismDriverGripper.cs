using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;
//using Unity.VisualScripting;

public class ArticulationPrismDriverGripper : MonoBehaviour
{
    [Header("Slip Parameters")]
    public string Comport1;
    public string Comport2;
    private SerialPort serial1;
    private SerialPort serial2;
    public GameObject targetCube, offsetMass;
    float originalMass = 0f; 
    public float targetOffsetMass = 2f;
    public float targetHeight = 1.05f; 

    public Transform driverObjects;
    private ArticulationBody[] physicsObjects;

    [Header("Control Parameters")]
    public float positionGain = 100f;      // Force multiplier for position error
    public float rotationGain = 50f;       // Torque multiplier for rotation error
    public float velocityGain = 100;       // Adjust this value for desired responsiveness
    public float dampingFactor = 0.1f;     // Adjust this value for desired smoothness
    public float movementTime = 0.1f; // Adjust this value based on your needs

    public Vector3 RotationOffset = new Vector3();
    public Vector3 PositionOffset = new Vector3();

    // Tip gameObjects for calculating the relative distance from tip to palm, let me know what you think.
    public GameObject thumbTip, indexTip, midTip, ringTip, palm;


    // Close states for the mapping function, not sure about the value might have to think about it again.
    //public ArticulationBody tmb, index, mid, ring;
    float tmbCloseState = 0.5f;
    float indexCloseState = 0.5f;
    float midCloseState = 0.5f;
    float ringCloseState = 0.5f;

    float[] fingerOpenState, fingerClosedState = new float[5] { 1f, 1f, 1f, 1f, 1f };

    bool start;

    Coroutine renderSequence, sendSerialSequence; 

    private void Start()
    {
        originalMass = offsetMass.GetComponent<Rigidbody>().mass;

        fingerOpenState = new float[5] { 1f, 1f, 1f, 1f, 1f };
        fingerClosedState = new float[5] { 1f, 1f, 1f, 1f, 1f };

        physicsObjects = new ArticulationBody[6];
        Invoke("FindGripper", 1f);


        try
        {
            serial1 = new SerialPort(Comport1, 115200);
            serial2 = new SerialPort(Comport2, 115200);

            serial1.Open();
            serial2.Open();
        }
        catch { print("Something went wrong!"); }

        renderSequence = StartCoroutine(RenderSlip());
    }

    // Do we assign these to the robot wrist digits on the prefab? They currently are unassigned in Unity
    void FindGripper()
    {
        physicsObjects[0] = GameObject.FindWithTag("tmb").GetComponent<ArticulationBody>();
        physicsObjects[1] = GameObject.FindWithTag("idx").GetComponent<ArticulationBody>();
        physicsObjects[2] = GameObject.FindWithTag("mid").GetComponent<ArticulationBody>();
        physicsObjects[3] = GameObject.FindWithTag("rng").GetComponent<ArticulationBody>();
        physicsObjects[4] = GameObject.FindWithTag("wrist").GetComponent<ArticulationBody>();
    }

    private void Update()
    {
        // Get distances between fingertips and the palm for open and closed hand states
        if (Input.GetKeyDown(KeyCode.O))
        {
            fingerOpenState = GetVRFingerState();
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            fingerClosedState = GetVRFingerState();
            start = true;
        }

        //float vrThumbValue = Vector3.Distance(thumbTip.transform.position, palm.transform.position);
        //print("Thumb: " + vrThumbValue);
    }

    void FixedUpdate()
    {
        if (start)
        {
            //if (physicsObjects[4] == null || driverObjects == null) return;

            #region Orientation Control
            //if (driverObjects.gameObject.name.Contains("Palm"))
            //{
            //    // 1. Compute target orientation with offset
            //    Quaternion offsetQ = Quaternion.Euler(RotationOffset);
            //    Quaternion desiredRotation = driverObjects.rotation * offsetQ; // Apply offset to driver's rotation

            //    // 2. Rotation Control
            //    // Calculate rotation difference
            //    Quaternion rotationDiff = desiredRotation * Quaternion.Inverse(physicsObjects[4].transform.rotation);
            //    rotationDiff.ToAngleAxis(out float angleDegrees, out Vector3 rotationAxis);

            //    // Handle the case where angle = 0
            //    if (Mathf.Approximately(angleDegrees, 0f)) return;

            //    // Convert angle to radians and apply torque
            //    float angleRadians = angleDegrees * Mathf.Deg2Rad;
            //    Vector3 torque = rotationAxis.normalized * (angleRadians * rotationGain);
            //    physicsObjects[4].AddTorque(torque, ForceMode.Force);
            //}
            #endregion

            #region Position Control
            //// 0. Counter gravity 
            //physicsObjects[4].AddForce(-Physics.gravity * physicsObjects[4].mass);

            //// 1. Position Control
            //Vector3 positionError = (driverObjects.position + PositionOffset) - physicsObjects[4].worldCenterOfMass;

            //// Calculate desired velocity
            //Vector3 desiredVelocity = positionError / movementTime;

            //// Calculate velocity difference
            //Vector3 velocityDifference = desiredVelocity - physicsObjects[4].velocity;

            //// Apply force based on velocity difference
            //physicsObjects[4].AddForce(velocityDifference * physicsObjects[4].mass * velocityGain);

            //// Apply damping
            //physicsObjects[4].AddForce(-physicsObjects[4].velocity * dampingFactor * physicsObjects[4].mass);
            #endregion

            Grip();
        }
    }

    void Grip()
    {
        // 1. Finger A and B to close on the object (to finger close state) 
        ArticulationDrive thumb_digit = physicsObjects[0].yDrive;
        float vrThumbValue = Vector3.Distance(thumbTip.transform.position, palm.transform.position);
        float thumbTarget = map(vrThumbValue, fingerOpenState[0], fingerClosedState[0], 0f, tmbCloseState);
        //print("Thumb Target: " + thumbTarget);
        thumb_digit.target = thumbTarget;
        physicsObjects[0].yDrive = thumb_digit;

        ArticulationDrive index_digit = physicsObjects[1].yDrive;
        float vrIndexValue = Vector3.Distance(indexTip.transform.position, palm.transform.position);
        index_digit.target = map(vrIndexValue, fingerOpenState[1], fingerClosedState[1], 0f, indexCloseState);
        physicsObjects[1].yDrive = index_digit;

        ArticulationDrive mid_digit = physicsObjects[2].yDrive;
        float vrMidValue = Vector3.Distance(midTip.transform.position, palm.transform.position);
        mid_digit.target = map(vrMidValue, fingerOpenState[2], fingerClosedState[2], 0f, midCloseState);
        physicsObjects[2].yDrive = mid_digit;

        ArticulationDrive rng_digit = physicsObjects[3].yDrive;
        float vrRngValue = Vector3.Distance(ringTip.transform.position, palm.transform.position);
        rng_digit.target = map(vrRngValue, fingerOpenState[3], fingerClosedState[3], 0f, ringCloseState);
        physicsObjects[3].yDrive = rng_digit;
    }

    // Placeholder to get VR finger values: I added in the tip gameObjects for reach finger in order to get their relative value to the palm, not sure how to implement this.
    private float[] GetVRFingerState()
    {
        float[] fingerstate = new float[5];

        fingerstate[0] = Vector3.Distance(thumbTip.transform.position, palm.transform.position);
        fingerstate[1] = Vector3.Distance(indexTip.transform.position, palm.transform.position);
        fingerstate[2] = Vector3.Distance(midTip.transform.position, palm.transform.position);
        fingerstate[3] = Vector3.Distance(ringTip.transform.position, palm.transform.position);
        fingerstate[4] = fingerstate[0] + fingerstate[1] + fingerstate[2] + fingerstate[3];

        return fingerstate;
    }


    IEnumerator RenderSlip()
    {
        print("Staring render sequence!!");
        offsetMass.GetComponent<Rigidbody>().mass = originalMass;

        // If we reach the target height, trigger the cube falling sequence through the offset mass
        while (targetCube.transform.position.y < targetHeight)
        {
            yield return null; 
        }

        print("Star increasing the mass!!");
        // Increase the offset mass (later also change the offset mass location) 
        while (offsetMass.GetComponent<Rigidbody>().mass < targetOffsetMass)
        {
            offsetMass.GetComponent<Rigidbody>().mass += 1f;

            if (targetCube.GetComponent<Rigidbody>().velocity.y < -0.15f)
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
        yield return null;


    }

    // Mapping function to get the value between 0 and 1.
    public static float map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }

}

// Are we going to basically close the robot digit when say the relative thumbTip position to the palm is > 0.7? This makes sense in my head but I don't know if it fully works, let me know.