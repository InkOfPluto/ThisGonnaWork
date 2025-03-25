using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;

public class KukaIIWA7Glob : MonoBehaviour
{
    public ArticulationBody[] joints;
    public Transform targetTransform;

    // Controller gains
    public float Kp_pos = 100f;
    public float Kd_pos = 20f;
    public float Kp_rot = 80f;
    public float Kd_rot = 15f;

    // Damping value for the drives
    public float dampingValue = 100f;
    public float maxForceLimit = 1000f;

    private Vector3 lastEndEffectorPos;
    private Vector3 lastEndEffectorRot;
    private float deltaTime;

    void Start()
    {
        if (joints.Length != 7)
        {
            Debug.LogError("Please assign all 7 joints of the robotic arm.");
            enabled = false;
            return;
        }

        deltaTime = Time.fixedDeltaTime;
        lastEndEffectorPos = joints[joints.Length - 1].transform.position;
        lastEndEffectorRot = joints[joints.Length - 1].transform.eulerAngles;
    }

    void FixedUpdate()
    {
        Transform endEffector = joints[joints.Length - 1].transform;
        Vector3 currentPos = endEffector.position;
        Vector3 currentRotEuler = endEffector.eulerAngles;

        // Compute velocities
        Vector3 endEffectorVelLinear = (currentPos - lastEndEffectorPos) / deltaTime;
        Vector3 endEffectorVelAngular = (currentRotEuler - lastEndEffectorRot) / deltaTime;

        lastEndEffectorPos = currentPos;
        lastEndEffectorRot = currentRotEuler;

        // Position and orientation errors
        Vector3 posError = targetTransform.position - currentPos;

        Quaternion currentRot = endEffector.rotation;
        Quaternion rotError = targetTransform.rotation * Quaternion.Inverse(currentRot);
        rotError.ToAngleAxis(out float angleInDegrees, out Vector3 rotAxis);
        if (angleInDegrees > 180f) angleInDegrees -= 360f;
        Vector3 rotErrorVec = rotAxis * Mathf.Deg2Rad * angleInDegrees;

        // Desired velocities
        Vector3 desiredLinearVel = Kp_pos * posError - Kd_pos * endEffectorVelLinear;
        Vector3 desiredAngularVel = Kp_rot * rotErrorVec - Kd_rot * endEffectorVelAngular;

        // Desired end-effector velocity vector
        var desiredEndEffectorVel = Vector<float>.Build.Dense(new float[]
        {
            desiredLinearVel.x,
            desiredLinearVel.y,
            desiredLinearVel.z,
            desiredAngularVel.x,
            desiredAngularVel.y,
            desiredAngularVel.z
        });

        // Compute Jacobian
        Matrix<float> jacobian = ComputeJacobian();

        // Compute joint torques using Jacobian transpose
        Vector<float> jointTorques = jacobian.TransposeThisAndMultiply(desiredEndEffectorVel);

        // Apply joint torques or adjust target velocities
        ApplyJointTorques(jointTorques);
    }

    Matrix<float> ComputeJacobian()
    {
        int numJoints = joints.Length;
        var jacobian = Matrix<float>.Build.Dense(6, numJoints);

        Vector3 endEffectorPos = joints[numJoints - 1].transform.position;

        for (int i = 0; i < numJoints; i++)
        {
            Vector3 jointAxis = joints[i].transform.TransformDirection(joints[i].anchorRotation.eulerAngles);
            Vector3 jointPos = joints[i].transform.position;

            Vector3 linearComponent = Vector3.Cross(jointAxis, endEffectorPos - jointPos);
            Vector3 angularComponent = jointAxis;

            jacobian[0, i] = linearComponent.x;
            jacobian[1, i] = linearComponent.y;
            jacobian[2, i] = linearComponent.z;
            jacobian[3, i] = angularComponent.x;
            jacobian[4, i] = angularComponent.y;
            jacobian[5, i] = angularComponent.z;
        }

        return jacobian;
    }

    void ApplyJointTorques(Vector<float> jointTorques)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            var joint = joints[i];
            var drive = joint.xDrive;

            // Since we can't apply torque directly, we adjust the target velocity
            drive.targetVelocity = jointTorques[i];
            drive.stiffness = 0f;
            drive.damping = dampingValue;
            drive.forceLimit = maxForceLimit;

            joint.xDrive = drive;
        }
    }
}
