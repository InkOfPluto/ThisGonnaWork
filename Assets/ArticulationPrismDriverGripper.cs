using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArticulationPrismDriverGripper : MonoBehaviour
{

    public Transform[] driverObjects;
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
    public ArticulationBody tmb, index, mid, ring;
    float tmbCloseState = 0.05f;
    float indexCloseState = 0.05f;
    float midCloseState = 0.05f;
    float ringCloseState = 0.05f;

    bool start; 

    private void Start()
    {
        physicsObjects = new ArticulationBody[5]; 
        Invoke("FindGripper", 1f); 
    }

    // Do we assign these to the robot wrist digits on the prefab? They currently are unassigned in Unity
    void FindGripper()
    {
        physicsObjects[0] = GameObject.FindWithTag("wrist").GetComponent<ArticulationBody>();
        physicsObjects[1] = GameObject.FindWithTag("tmb").GetComponent<ArticulationBody>();
        physicsObjects[2] = GameObject.FindWithTag("idx").GetComponent<ArticulationBody>();
        physicsObjects[3] = GameObject.FindWithTag("mid").GetComponent<ArticulationBody>();
        physicsObjects[4] = GameObject.FindWithTag("rng").GetComponent<ArticulationBody>();

        start = true; 
    }

    void FixedUpdate()
    {
        if (start)
        {
            for (int i = 0; i < driverObjects.Length; i++)
            {
                if (physicsObjects[i] == null || driverObjects[i] == null) return;

                #region Orientation Control
                if (driverObjects[i].gameObject.name.Contains("Palm"))
                {
                    // 1. Compute target orientation with offset
                    Quaternion offsetQ = Quaternion.Euler(RotationOffset);
                    Quaternion desiredRotation = driverObjects[i].rotation * offsetQ; // Apply offset to driver's rotation

                    // 2. Rotation Control
                    // Calculate rotation difference
                    Quaternion rotationDiff = desiredRotation * Quaternion.Inverse(physicsObjects[i].transform.rotation);
                    rotationDiff.ToAngleAxis(out float angleDegrees, out Vector3 rotationAxis);

                    // Handle the case where angle = 0
                    if (Mathf.Approximately(angleDegrees, 0f)) return;

                    // Convert angle to radians and apply torque
                    float angleRadians = angleDegrees * Mathf.Deg2Rad;
                    Vector3 torque = rotationAxis.normalized * (angleRadians * rotationGain);
                    physicsObjects[i].AddTorque(torque, ForceMode.Force);
                }
                #endregion

                #region Position Control
                // 0. Counter gravity 
                physicsObjects[i].AddForce(-Physics.gravity * physicsObjects[i].mass);

                // 1. Position Control
                Vector3 positionError = (driverObjects[i].position + PositionOffset) - physicsObjects[i].worldCenterOfMass;

                // Calculate desired velocity
                Vector3 desiredVelocity = positionError / movementTime;

                // Calculate velocity difference
                Vector3 velocityDifference = desiredVelocity - physicsObjects[i].velocity;

                // Apply force based on velocity difference
                physicsObjects[i].AddForce(velocityDifference * physicsObjects[i].mass * velocityGain);

                // Apply damping
                physicsObjects[i].AddForce(-physicsObjects[i].velocity * dampingFactor * physicsObjects[i].mass);
                #endregion
            }


            Grip(); 

        }
    }

    void Grip()
    {
        float vrThumbValue = GetVRThumbValue();
        float vrIndexValue = GetVRIndexValue();
        float vrMiddleValue = GetVRMiddleValue();
        float vrRingValue = GetVRRingValue();

        // 1. Finger A and B to close on the object (to finger close state) 
        ArticulationDrive thumb_digit = physicsObjects[1].yDrive;
        thumb_digit.target = mapThumb(vrThumbValue, 0f, 1f, tmbCloseState, 0f); // Replaced the zero value with this, does it make sense?
        physicsObjects[1].yDrive = thumb_digit;

        ArticulationDrive index_digit = physicsObjects[2].yDrive;
        index_digit.target = mapIndex(vrIndexValue, 0f, 1f, indexCloseState, 0f);
        physicsObjects[2].yDrive = index_digit;

        ArticulationDrive mid_digit = physicsObjects[3].zDrive;
        mid_digit.target = mapRing(vrMiddleValue, 0f, 1f, midCloseState, 0f);
        physicsObjects[3].zDrive = mid_digit;

        ArticulationDrive ring_digit = physicsObjects[4].zDrive;
        ring_digit.target = mapRing(vrRingValue, 0f, 1f, ringCloseState, 0f);
        physicsObjects[4].zDrive = ring_digit;
    }

    // Placeholder to get VR finger values: I added in the tip gameObjects for reach finger in order to get their relative value to the palm, not sure how to implement this.
    private float GetVRThumbValue() { return 0.5f; }
    private float GetVRIndexValue() { return 0.5f; }
    private float GetVRMiddleValue() { return 0.5f; }
    private float GetVRRingValue() { return 0.5f; }


    // Mapping function to get the value between 0 and 1.
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

// Are we going to basically close the robot digit when say the relative thumbTip position to the palm is > 0.7? This makes sense in my head but I don't know if it fully works, let me know.