using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArticulationPrismDriver : MonoBehaviour
{

    public Transform[] driverObjects;
    public ArticulationBody[] physicsObjects;

    [Header("Control Parameters")]
    public float positionGain = 100f;      // Force multiplier for position error
    public float rotationGain = 50f;       // Torque multiplier for rotation error
    public float velocityGain = 100;       // Adjust this value for desired responsiveness
    public float dampingFactor = 0.1f;     // Adjust this value for desired smoothness
    public float movementTime = 0.1f; // Adjust this value based on your needs

    public Vector3 RotationOffset = new Vector3();
    public Vector3 PositionOffset = new Vector3();

    //void Start()
    //{
    //    foreach(ArticulationBody physicsObject in physicsObjects)
    //    {
    //        physicsObject.angularDamping = 50f;
    //    }
    //}

    //void FixedUpdate()
    //{
    //    for (int i = 0; i < driverObjects.Length; i++)
    //    {

    //        if (physicsObjects[i] == null || driverObjects[i] == null) return;

    //        // 0. Counter gravity 
    //        physicsObjects[i].AddForce(-Physics.gravity * physicsObjects[i].mass);

    //        // 1. Position Control
    //        Vector3 positionError = driverObjects[i].position - physicsObjects[i].transform.position;
    //        physicsObjects[i].AddForce(positionError * positionGain, ForceMode.Force);

    //    }
    //}


    void FixedUpdate()
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
    }

}

//    void FixedUpdate()
//    {
//        for (int i = 0; i < driverObjects.Length; i++)
//        {
//            if (physicsObjects[i] == null || driverObjects[i] == null) return;

//            #region Orientation Control
//            if (driverObjects[i].gameObject.name.Contains("Palm"))
//            {

//                // 1. Compute target orientation with offset
//                Quaternion offsetQ = Quaternion.Euler(RotationOffset);
//                Quaternion desiredRotation = offsetQ * driverObjects[i].rotation;

//                // 2. Rotation Control
//                // Calculate rotation difference
//                Quaternion rotationDiff = desiredRotation * Quaternion.Inverse(physicsObjects[i].transform.rotation);
//                rotationDiff.ToAngleAxis(out float angleDegrees, out Vector3 rotationAxis);

//                // Handle the case where angle = 0
//                if (Mathf.Approximately(angleDegrees, 0f)) return;

//                // Convert angle to radians and apply torque
//                float angleRadians = angleDegrees * Mathf.Deg2Rad;
//                Vector3 torque = rotationAxis.normalized * (angleRadians * rotationGain);
//                physicsObjects[i].AddTorque(torque, ForceMode.Force);
//            }
//            #endregion

//            #region Position Control
//            // 0. Counter gravity 
//            physicsObjects[i].AddForce(-Physics.gravity * physicsObjects[i].mass);

//            // 1. Position Control
//            Vector3 positionError = driverObjects[i].position - physicsObjects[i].worldCenterOfMass;

//            // Calculate desired velocity
//            Vector3 desiredVelocity = positionError / movementTime;

//            // Calculate velocity difference
//            Vector3 velocityDifference = desiredVelocity - physicsObjects[i].velocity;

//            // Apply force based on velocity difference
//            physicsObjects[i].AddForce(velocityDifference * physicsObjects[i].mass * velocityGain);

//            // Apply damping
//            physicsObjects[i].AddForce(-physicsObjects[i].velocity * dampingFactor * physicsObjects[i].mass);

//            #endregion
//        }
//    }
//}


//// 2. Rotation Control
//// Calculate rotation difference
//Quaternion rotationDiff = driverObjects[0].rotation * Quaternion.Inverse(physicsObjects[0].transform.rotation);
//rotationDiff.ToAngleAxis(out float angleDegrees, out Vector3 rotationAxis);

//// Handle the case where angle = 0
//if (Mathf.Approximately(angleDegrees, 0f)) return;

//// Convert angle to radians and apply torque
//float angleRadians = angleDegrees * Mathf.Deg2Rad;
//Vector3 torque = rotationAxis.normalized * (angleRadians * rotationGain);
//physicsObjects[0].AddTorque(torque, ForceMode.Force);