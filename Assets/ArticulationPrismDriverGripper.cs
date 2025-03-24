using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;
using System.IO;
using System.Linq; 
//using Unity.VisualScripting;

public class DataClass
{
    public List<string> trialInfo = new List<string>();
    public List<float> xPos = new List<float>();
    public List<float> yPos = new List<float>();
    public List<float> zPos = new List<float>();
    public List<float> xRot = new List<float>();
    public List<float> yRot = new List<float>();
    public List<float> zRot = new List<float>();
    public List<float> xTPos = new List<float>();
    public List<float> yTPos = new List<float>();
    public List<float> zTPos = new List<float>();
    public List<float> xTrot = new List<float>();
    public List<float> yTrot = new List<float>();
    public List<float> zTrot = new List<float>();
    public List<float> time = new List<float>();

    public DataClass()
    {
        trialInfo = new List<string>();
        xPos = new List<float>();
        yPos = new List<float>();
        zPos = new List<float>();
        xRot = new List<float>();
        yRot = new List<float>();
        zRot = new List<float>();
        xTPos = new List<float>();
        yTPos = new List<float>();
        zTPos = new List<float>();
        xTrot = new List<float>();
        yTrot = new List<float>();
        zTrot = new List<float>();
        time = new List<float>();
    }

    public void ClearAllVars()
    {
        trialInfo.Clear();
        xPos.Clear();
        yPos.Clear();
        zPos.Clear();
        xRot.Clear();
        yRot.Clear();
        zRot.Clear();
        xTPos.Clear();
        yTPos.Clear();
        zTPos.Clear();
        xTrot.Clear();
        yTrot.Clear();
        zTrot.Clear();
        time.Clear();
    }

}

public class ArticulationPrismDriverGripper : MonoBehaviour
{
    #region Variables
    public DataClass myData;

    public bool threaded = true;

    public string path;
    private Coroutine saveDataCoroutine;
    private int trialNumber = 0;
    private float startTime;
    private float elapsedTime = 0f;

    private bool startRecord = false;

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

    Rigidbody targetCube_RB;
    int[][] blockedTrials;

    public bool startTrial;
    bool trialEnd = false;

    public GameObject forcePosBody;
    GameObject forceatposbody; // = new GameObject();

    string[] force_directions = new string[] { "Nothing", "Tilt_Right", "Tilt_Left", "Tilt_Forward", "Tilt_Backward" };

    #endregion

    private void Start()
    {
        myData = new DataClass();
        startTime = Time.time;

        forceatposbody = new GameObject();

        force_directions = new string[] { "Tilt_Left", "Tilt_Right", "Tilt_Backward", "Tilt_Forward" };
        trialEnd = true; // For the first ever trial it should be set to true 

        // Experimental design 
        // Here we need to initially craete a pseudo-random array of trials + blocks (haptics/no-haptics) 
        // On average a trial takes about 5 seconds = 300 seconds per user

        // Create a double array (array of arrays)
        blockedTrials = new int[2][];

        // (0) Represents No Haptics and (1) Represents Haptics
        //blockedTrials[0] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 };
        //blockedTrials[0] = new int[] { 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
        blockedTrials[0] = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1};


        //blockedTrials[0] = new int[] {0, 0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1,1,1 };
        //blockedTrials[0] = new int[] {0, 0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1,1,1 };

        // (1,2,3,4) represent the 4 different drop offsets 
        blockedTrials[1] = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 };

        // Randomly shuffle the conditions for each participant once at the start 
        //Shuffle(blockedTrials[0]);
        Shuffle(blockedTrials[1]);

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


    void FindGripper()
    {
        try
        {
            physicsObjects[0] = GameObject.FindWithTag("tmb").GetComponent<ArticulationBody>();
            physicsObjects[1] = GameObject.FindWithTag("idx").GetComponent<ArticulationBody>();
            physicsObjects[2] = GameObject.FindWithTag("mid").GetComponent<ArticulationBody>();
            physicsObjects[3] = GameObject.FindWithTag("rng").GetComponent<ArticulationBody>();
            physicsObjects[4] = GameObject.FindWithTag("wrist").GetComponent<ArticulationBody>();
            print("Robot gripper found!");
        }
        catch
        {
            print("Robot gripper not available!");
        }
    }

    public void StartTrialFunction(float val)
    {
        print("Button val: " + val);
        startTrial = true;
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
        if (Input.GetKeyDown(KeyCode.R) | startTrial)
        {
            startTrial = false;

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
            targetCube_RB = targetCube.GetComponent<Rigidbody>();
            //offsetMass = Instantiate(offsetMassObj);
            //targetCube.GetComponent<FixedJoint>().connectedBody = offsetMass.GetComponent<Rigidbody>();
            originalMass = targetCube_RB.mass;
            targetCube_RB.velocity = Vector3.zero;
            targetCube_RB.angularVelocity = Vector3.zero;
            originalPosition = targetCube.transform.position;
            originalRotation = targetCube.transform.rotation;

            if (renderSequence != null)
            {
                StopCoroutine(renderSequence);
            }
            renderSequence = StartCoroutine(ExperimentalProcedure());
        }
        //float vrThumbValue = Vector3.Distance(thumbTip.transform.position, palm.transform.position);
        //print("Thumb: " + vrThumbValue);

        // Get an average velocity measure and use that instead of current velocity
        if (targetCube != null)
        {
            averageVel[k] = targetCube_RB.velocity.y;
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
            if (physicsObjects[4] == null)
            {
                print("Looking for gripper again!");
                FindGripper();
            }

            #region Orientation Control
            if (driverObjects.gameObject.name.Contains("Palm"))
            {
                // 1. Compute target orientation with offset
                Quaternion offsetQ = Quaternion.Euler(RotationOffset);
                Quaternion desiredRotation = driverObjects.rotation * offsetQ; // Apply offset to driver's rotation

                // 2. Rotation Control
                // Calculate rotation difference
                Quaternion rotationDiff = desiredRotation * Quaternion.Inverse(physicsObjects[4].transform.rotation);
                rotationDiff.ToAngleAxis(out float angleDegrees, out Vector3 rotationAxis);

                // Handle the case where angle = 0
                if (Mathf.Approximately(angleDegrees, 0f)) return;

                // Convert angle to radians and apply torque
                float angleRadians = angleDegrees * Mathf.Deg2Rad;
                Vector3 torque = rotationAxis.normalized * (angleRadians * rotationGain);
                physicsObjects[4].AddTorque(torque, ForceMode.Force);
                //}
                #endregion

                #region Position Control
                // 0. Counter gravity 
                physicsObjects[4].AddForce(-Physics.gravity * physicsObjects[4].mass);

                // 1. Position Control
                Vector3 positionError = (driverObjects.position + PositionOffset) - physicsObjects[4].worldCenterOfMass;

                // Calculate desired velocity
                Vector3 desiredVelocity = positionError / movementTime;

                // Calculate velocity difference
                Vector3 velocityDifference = desiredVelocity - physicsObjects[4].velocity;

                // Apply force based on velocity difference
                physicsObjects[4].AddForce(velocityDifference * physicsObjects[4].mass * velocityGain);

                // Apply damping
                physicsObjects[4].AddForce(-physicsObjects[4].velocity * dampingFactor * physicsObjects[4].mass);
                #endregion

                Grip();
            }
        }

        if (startRecord)
        {
            elapsedTime = Time.time - startTime;

            // Get the position and rotation of the object and target, as well as Trial info
            myData.xPos.Add(palm.transform.position.x);
            myData.yPos.Add(palm.transform.position.y);
            myData.zPos.Add(palm.transform.position.z);
            myData.xRot.Add(palm.transform.rotation.x);
            myData.yRot.Add(palm.transform.rotation.y);
            myData.zRot.Add(palm.transform.rotation.z);

            myData.xTPos.Add(targetCube.transform.position.x);
            myData.yTPos.Add(targetCube.transform.position.y);
            myData.zTPos.Add(targetCube.transform.position.z);
            myData.xTrot.Add(targetCube.transform.rotation.x);
            myData.yTrot.Add(targetCube.transform.rotation.y);
            myData.zTrot.Add(targetCube.transform.rotation.z);

            myData.time.Add(elapsedTime);
            myData.trialInfo.Add("Trial " + trialNumber.ToString());
        }


        if (Input.GetKeyDown(KeyCode.S))
        {
            if (saveDataCoroutine != null)
            {
                StopCoroutine(saveDataCoroutine);
            }
            saveDataCoroutine = StartCoroutine(SaveFile());
            trialNumber++;
            startTime = Time.time;
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
    float[] GetVRFingerState()
    {
        float[] fingerstate = new float[5];

        fingerstate[0] = Vector3.Distance(thumbTip.transform.position, palm.transform.position);
        fingerstate[1] = Vector3.Distance(indexTip.transform.position, palm.transform.position);
        fingerstate[2] = Vector3.Distance(midTip.transform.position, palm.transform.position);
        fingerstate[3] = Vector3.Distance(ringTip.transform.position, palm.transform.position);
        fingerstate[4] = fingerstate[0] + fingerstate[1] + fingerstate[2] + fingerstate[3];

        return fingerstate;
    }

    IEnumerator ExperimentalProcedure()
    {
        startTime = Time.time;
        startRecord = true;
        int localTrial = trial;
        int _trial = localTrial % 8;

        trialEnd = false;

        targetHeight.GetComponent<MeshRenderer>().enabled = true;
        // Another loop for blocks (blocks with haptics and without) 
        instructions.text = "Lift object and hold for 2.5 seconds. \nTrial: " + trial;
        //instructions.text = force_directions[blockedTrials[1][_trial]].ToString() + "\nHaptics: " + blockedTrials[0][_trial].ToString();

        // If we reach the target height, trigger the cube falling sequence through the offset mass
        while (targetCube.transform.position.y < targetHeight.position.y)
        {
            yield return null;
        }

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
                trialEnd = false;
            }
            yield return null;
        }

        instructions.text = "Trial: " + trial + "\nKeep hold and keep steady.";
        dropForce = 0;
        // Increase the offset mass (later also change the offset mass location) 
        float xRand = 0f;
        float zRand = 0f;

        float[] offsetvals = DeterminePositionalOffset(_trial);
        xRand = offsetvals[0];
        zRand = offsetvals[1];

        while (targetCube.transform.position.y > targetHeight.transform.position.y) // Corrected this away from mass and velocity and use the height of the target object instead 
        {
            //targetCube_RB.mass += 0.5f;
            //targetCube_RB.angularVelocity = new Vector3(0f,0f,10f); 
            dropForce -= 0.05f;

            Vector3 offsetPos = new Vector3(transform.position.x + xRand, transform.position.y + 0.015f, transform.position.z + zRand);
            // For debugging, instantiate a visual aid to show where the offset force is being applied
            if (forceatposbody != null)
                Destroy(forceatposbody);
            forceatposbody = Instantiate(forcePosBody, targetCube.transform.position + offsetPos, Quaternion.identity);

            targetCube_RB.AddForceAtPosition(new Vector3(0f, dropForce, 0f), targetCube.transform.position + forceatposbody.transform.position, ForceMode.Acceleration);
            yield return null;
        }
        if (forceatposbody != null)
            Destroy(forceatposbody);

        if (blockedTrials[0][trial] == 0)
        {
            // No haptics 
            print("No slip rendering");
        }
        else
        {
            SlipMethod3(forceatposbody.transform.position);
        }

        yield return new WaitForSeconds(1f);

        targetCube_RB.velocity = Vector3.zero;
        targetCube_RB.angularVelocity = Vector3.zero;

        instructions.text = "Press button for next trial!";
        trialEnd = true;

        startRecord = false;
        if (saveDataCoroutine != null)
        {
            StopCoroutine(saveDataCoroutine);
        }
        saveDataCoroutine = StartCoroutine(SaveFile());

        yield return null;
    }

    float[] DeterminePositionalOffset(int _trial)
    {
        float xRand = 0f;
        float zRand = 0f;
        float[] result = new float[2] { 0f, 0f };

        if (blockedTrials[1][_trial] == 0) // East (positive) x-axis positional offset for applied force 
        {
            xRand = 0.02f;
            zRand = 0f;
        }
        if (blockedTrials[1][_trial] == 1) // West (negative) x-axis positional offset for applied force 
        {
            xRand = -0.02f;
            zRand = 0f;
        }
        if (blockedTrials[1][_trial] == 2) // North (positive) z-axis positional offset for applied force 
        {
            xRand = 0f;
            zRand = 0.02f;
        }
        if (blockedTrials[1][_trial] == 3) // South (negative) z-axis positional offset for applied force 
        {
            xRand = 0f;
            zRand = -0.02f;
        }
        if (blockedTrials[1][_trial] == 4) // NE (positive) x-axis positional offset for applied force 
        {
            xRand = 0.01f;
            zRand = 0.01f;
        }
        if (blockedTrials[1][_trial] == 5) // NW (negative) x-axis positional offset for applied force 
        {
            xRand = 0.01f;
            zRand = -0.01f;
        }
        if (blockedTrials[1][_trial] == 6) // SE (positive) z-axis positional offset for applied force 
        {
            xRand = -0.01f;
            zRand = 0.01f;
        }
        if (blockedTrials[1][_trial] == 7) // SW (negative) z-axis positional offset for applied force 
        {
            xRand = -0.01f;
            zRand = -0.01f;
        }


        result[0] = xRand;
        result[1] = zRand;

        return result;
    }

    void SlipMethod1()
    {
        try
        {
            // Fixed velocity 
            serial1.WriteLine("fgfgbnbn");
            serial2.WriteLine("fgfgbnbn");
            Debug.Log("Slipping!");
        }
        catch
        {
            print("Serial device error!");
        }
    }
    void SlipMethod2()
    {
        try
        {
            // Velocity and direction 
            if (targetCube.transform.position.y >= 0.3)
            {


            }
            Debug.Log("Slipping!");
        }
        catch
        {
            print("Serial device error!");
        }
    }


    void SlipMethod3(Vector3 slipPivot) // i.e. our forceatposbody 
    {
        try
        {
            // Get the direction from the palm to the pivot (projected on the XZ plane)
            Vector3 pivot = slipPivot;
            Vector3 palmForward = new Vector3(palm.transform.forward.x, 0, palm.transform.forward.z).normalized;
            Vector3 toPivot = (new Vector3(pivot.x, 0, pivot.z) - new Vector3(palm.transform.position.x, 0, palm.transform.position.z)).normalized;

            // Determine the angle between palm forward and the vector to the pivot
            float angle = Vector3.Angle(palmForward, toPivot);
            // Use the cross product to decide if the pivot is to the right or left of the palm forward
            float crossY = Vector3.Cross(palmForward, toPivot).y;

            string command = "";
            if (angle < 45f)
            {
                // Slip direction roughly matches palm forward  actuate index finger ("b")
                command = "b";
                Debug.Log("Index Slip");
            }
            else if (angle > 135f)
            {
                // Slip is opposite to palm forward  actuate middle finger ("g")
                command = "g";
                Debug.Log("Middle Slip");
            }
            else
            {
                if (crossY > 0)
                {
                    // Pivot is to the right  actuate thumb ("f")
                    command = "f";
                    Debug.Log("Thumb Slip");
                }
                else
                {
                    // Pivot is to the left  actuate ring finger ("n")
                    command = "n";
                    Debug.Log("Ring Slip");
                }
            }

            // Send the haptic command to both serial devices
            serial1.WriteLine(command);
            serial2.WriteLine(command);
            Debug.Log("Haptic slip command: " + command);
        }
        catch
        {
            print("Serial device error!");
        }
    }

    // Mapping function to get the value between 0 and 1.
    public static float map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }

    static System.Random _random = new System.Random();
    public static void Shuffle<T>(T[] array)
    {
        int n = array.Length;
        for (int i = 0; i < (n - 1); i++)
        {
            int r = i + _random.Next(n - i);
            T t = array[r];
            array[r] = array[i];
            array[i] = t;
        }
    }

    private IEnumerator SaveFile()
    {
        // Convert to json and send to another site on the server
        string jsonString = JsonConvert.SerializeObject(myData, Formatting.Indented);

        // Save the data to a file
        if (!threaded)
        {
            File.WriteAllText(path + "testfile" + trial.ToString() + ".json", jsonString);
        }
        else 
        {
            // create new thread to save the data to a file (only operation that can be done in background)
            new System.Threading.Thread(() =>
            {
                File.WriteAllText(path + "testfile" + trial.ToString() + ".json", jsonString);
            }).Start();
        }

        // Empty text fields for next trials (potential for issues with next trial)
        myData.ClearAllVars();

        yield return null;
    }
}

// TO DO:
// Add slip rendering based on fingers
// Finish the experimental procedure arrays.
