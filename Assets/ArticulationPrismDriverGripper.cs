using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;
using TMPro;
//using Unity.VisualScripting;

public class ArticulationPrismDriverGripper : MonoBehaviour
{
    [Header("Slip Parameters")]
    public string Comport1;
    public string Comport2;
    private SerialPort serial1;
    private SerialPort serial2;
    public GameObject TargetObj, offsetMassObj;
    private GameObject targetCube, offsetMass;
    float originalMass = 0f;
    public float targetOffsetMass = 2f;
    public Transform targetHeight;
    private Vector3 velocity = SlipDetector.vel;

    public Transform driverObjects;
    private ArticulationBody[] physicsObjects;

    [Header("Control Parameters")]
    public float positionGain = 100f;      // Force multiplier for position error
    public float rotationGain = 50f;       // Torque multiplier for rotation error
    public float velocityGain = 100;       // Adjust this value for desired responsiveness
    public float dampingFactor = 0.1f;     // Adjust this value for desired smoothness
    public float movementTime = 0.1f; // Adjust this value based on your needs

    Quaternion originalRotation, originalOffsetRotation;
    Vector3 originalPosition = new Vector3();
    Vector3 originalOffsetPosition = new Vector3();
    public Vector3 RotationOffset = new Vector3();
    public Vector3 PositionOffset = new Vector3();

    // Tip gameObjects for calculating the relative distance from tip to palm, let me know what you think.
    public GameObject thumbTip, indexTip, midTip, ringTip, palm;

    public TMPro.TMP_Text instructions;

    // Close states for the mapping function, not sure about the value might have to think about it again.
    //public ArticulationBody tmb, index, mid, ring;
    float tmbCloseState = 0.5f;
    float indexCloseState = 0.5f;
    float midCloseState = 0.5f;
    float ringCloseState = 0.5f;

    float[] fingerOpenState, fingerClosedState = new float[5] { 1f, 1f, 1f, 1f, 1f };

    bool start, ready;

    Coroutine renderSequence, sendSerialSequence;
    public float dropForce = 100f;

    private float[] averageVel = new float[100];
    private float meanVel = 0f;
    private int k = 0;
    int trial = -1; 

    private void Start()
    {
        // Experimental design 
        // Here we need to initially craete a pseudo-random array of trials + blocks (haptics/no-haptics) 
        // On average a trial takes about 5 seconds = 

        fingerOpenState = new float[5] { 1f, 1f, 1f, 1f, 1f };
        fingerClosedState = new float[5] { 1f, 1f, 1f, 1f, 1f };

        physicsObjects = new ArticulationBody[6];
        Invoke("FindGripper", 1f);

        averageVel = new float[100];

        try
        {
            serial1 = new SerialPort(Comport1, 115200);
            serial2 = new SerialPort(Comport2, 115200);

            serial1.Open();
            serial2.Open();
        }
        catch { print("Something went wrong!"); }
    }

    // Do we assign these to the robot wrist digits on the prefab? They currently are unassigned in Unity
    void FindGripper()
    {
        try
        {
            physicsObjects[0] = GameObject.FindWithTag("tmb").GetComponent<ArticulationBody>();
            physicsObjects[1] = GameObject.FindWithTag("idx").GetComponent<ArticulationBody>();
            physicsObjects[2] = GameObject.FindWithTag("mid").GetComponent<ArticulationBody>();
            physicsObjects[3] = GameObject.FindWithTag("rng").GetComponent<ArticulationBody>();
            physicsObjects[4] = GameObject.FindWithTag("wrist").GetComponent<ArticulationBody>();
        }
        catch
        {
            print("Robot gripper not available!"); 
        }
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
        if (Input.GetKeyDown(KeyCode.R))
        {
            trial++; 

            if (targetCube != null)
            {
                Destroy(targetCube);
            }
            if (offsetMass != null)
            {
                Destroy(offsetMass);
            }
            targetCube = Instantiate(TargetObj);
            //offsetMass = Instantiate(offsetMassObj);
            //targetCube.GetComponent<FixedJoint>().connectedBody = offsetMass.GetComponent<Rigidbody>();
            originalMass = targetCube.GetComponent<Rigidbody>().mass;
            targetCube.GetComponent<Rigidbody>().velocity = Vector3.zero;
            targetCube.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            originalPosition = targetCube.transform.position;
            originalRotation = targetCube.transform.rotation;

            if (renderSequence != null)
            {
                StopCoroutine(renderSequence);
            }
            renderSequence = StartCoroutine(RenderSlip());
        }
        //float vrThumbValue = Vector3.Distance(thumbTip.transform.position, palm.transform.position);
        //print("Thumb: " + vrThumbValue);

        // Get an average velocity measure and use that instead of current velocity
        if(targetCube!=null)
        {
            averageVel[k] = targetCube.GetComponent<Rigidbody>().velocity.y;
            k++;
            if (k >= 5)
            {
                float sumVel = 0;
                foreach (float vel in averageVel)
                {
                    sumVel += vel;
                }
                meanVel = sumVel / 5f;

                k = 0;
            }
        }
        else
        {
            meanVel = 0f; 
        }
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

    // Get VR Finger Values using the fingerstates.
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
        targetHeight.GetComponent<MeshRenderer>().enabled = true;

        // Another loop for blocks (blocks with haptics and without) 
        instructions.text = "Lift object and hold for 5 seconds. Trial: " + trial;

        // If we reach the target height, trigger the cube falling sequence through the offset mass
        while (targetCube.transform.position.y < targetHeight.position.y)
        {
            yield return null;
        }

        float startTime = Time.time;
        float holdTime = 2.5f; // Hold for 5 seconds
        float holdAtHeightTime = 0f;

        while (holdAtHeightTime < holdTime)
        {
            if (targetCube.transform.position.y >= targetHeight.position.y)
            {
                holdAtHeightTime += Time.deltaTime;
                instructions.text = "Hold time: " + holdAtHeightTime.ToString("F3") + "/ 2.5 seconds \n" + "Trial: " + trial;
                targetHeight.GetComponent<MeshRenderer>().enabled = false; 
            }
            else
            {
                holdAtHeightTime = 0f;
                targetHeight.GetComponent<MeshRenderer>().enabled = true;
            }
            yield return null;
        }

        instructions.text = "Increase object downward force. Trial: " + trial;
        dropForce = 0;
        // Increase the offset mass (later also change the offset mass location) 
        while (targetCube.transform.position.y > targetHeight.transform.position.y) // Corrected this away from mass and velocity and use the height of the target object instead 
        {
            //targetCube.GetComponent<Rigidbody>().mass += 0.5f;
            //targetCube.GetComponent<Rigidbody>().angularVelocity = new Vector3(0f,0f,10f); 
            dropForce -= 0.25f;
            float xRand = Random.Range(-0.15f, 0.15f);
            float zRand = Random.Range(-0.15f, 0.15f);
            Vector3 offsetPos = new Vector3(transform.position.x + xRand, transform.position.y, transform.position.z + zRand);
            targetCube.GetComponent<Rigidbody>().AddForceAtPosition(new Vector3(0f, dropForce, 0f), targetCube.transform.position + offsetPos, ForceMode.Impulse);
            yield return null;
        }

        targetCube.GetComponent<Rigidbody>().velocity = Vector3.zero;
        targetCube.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;

        SlipMethod1();
        yield return null;
    }


    void SlipMethod1()
    {
        // Fixed velocity 
        serial1.WriteLine("fgfgbnbn"); // Add velocity term to the microcontrooler 
        serial2.WriteLine("fgfgbnbn");
        Debug.Log("Slipping!");
    }
    void SlipMethod2()
    {
        // Velocity and direction 
        serial1.WriteLine("fgfgbnbn");
        serial2.WriteLine("fgfgbnbn");
        Debug.Log("Slipping!");
    }



    // Mapping function to get the value between 0 and 1.
    public static float map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }

}