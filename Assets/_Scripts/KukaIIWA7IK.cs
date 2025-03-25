using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;

public class KukaIIWA7IK : MonoBehaviour
{
    [Header("Robotic Arm Joints (from base to end-effector)")]
    public ArticulationBody[] joints; // Should be 7 joints from A1 to A7

    [Header("Target Transform (User's Hand)")]
    public Transform targetTransform;

    [Header("IK Parameters")]
    public int maxIterations = 10;
    public float positionTolerance = 0.01f;
    public float orientationTolerance = 1f; // Degrees
    public float dampingFactor = 0.1f;

    private float[] jointAngles; // Current joint angles

    Vector<float> deltaTheta; 

    void Start()
    {
        if (joints.Length != 7)
        {
            Debug.LogError("Please assign all 7 joints of the robotic arm.");
            enabled = false;
            return;
        }

        jointAngles = new float[7];

        deltaTheta = Vector<float>.Build.Dense(6);
    }

    void FixedUpdate()
    {
        if(Input.GetKeyDown(KeyCode.A))
        { 
            PerformIK(); 
        }
            
    }

    void PerformIK()
    {
        // Get current joint angles
        for (int i = 0; i < joints.Length; i++)
        {
            jointAngles[i] = joints[i].jointPosition[0];
        }

        // Perform IK iterations
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            // Forward kinematics to get current end-effector transform
            Transform endEffector = joints[joints.Length - 1].transform;
            Vector3 currentPosition = endEffector.position;
            Quaternion currentRotation = endEffector.rotation;

            // Compute position and orientation error
            Vector3 positionError = targetTransform.position - currentPosition;
            Quaternion rotationError = targetTransform.rotation * Quaternion.Inverse(currentRotation);

            // Check if within tolerances
            if (positionError.magnitude < positionTolerance &&
                Quaternion.Angle(currentRotation, targetTransform.rotation) < orientationTolerance)
            {
                break; // Desired position and orientation reached
            }

            // Compute the Jacobian matrix
            Matrix4x4[] jointTransforms = new Matrix4x4[joints.Length];
            jointTransforms[0] = joints[0].transform.localToWorldMatrix;

            for (int i = 1; i < joints.Length; i++)
            {
                jointTransforms[i] = jointTransforms[i - 1] * Matrix4x4.TRS(
                    joints[i].transform.localPosition,
                    joints[i].transform.localRotation,
                    joints[i].transform.localScale);
            }

            Vector3[] jointAxes = new Vector3[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                jointAxes[i] = jointTransforms[i].GetColumn(2); // Assuming rotation around local Z axis
            }

            // Compute Jacobian
            var jacobian = Matrix<float>.Build.Dense(6, joints.Length);
            Vector3 endEffectorPos = endEffector.position;

            for (int i = 0; i < joints.Length; i++)
            {
                Vector3 jointPos = joints[i].transform.position;
                Vector3 axis = jointAxes[i];

                Vector3 linearVelocity = Vector3.Cross(axis, endEffectorPos - jointPos);
                Vector3 angularVelocity = axis;

                jacobian[0, i] = linearVelocity.x;
                jacobian[1, i] = linearVelocity.y;
                jacobian[2, i] = linearVelocity.z;
                jacobian[3, i] = angularVelocity.x;
                jacobian[4, i] = angularVelocity.y;
                jacobian[5, i] = angularVelocity.z;
            }

            // Compute the error vector
            var errorVector = Vector<float>.Build.Dense(6);
            errorVector[0] = positionError.x;
            errorVector[1] = positionError.y;
            errorVector[2] = positionError.z;
            errorVector[3] = rotationError.eulerAngles.x * Mathf.Deg2Rad;
            errorVector[4] = rotationError.eulerAngles.y * Mathf.Deg2Rad;
            errorVector[5] = rotationError.eulerAngles.z * Mathf.Deg2Rad;

            // Damped Least Squares solution
            var jacobianTranspose = jacobian.Transpose();
            float lambda = dampingFactor * dampingFactor;

            var identity = Matrix<float>.Build.DenseIdentity(6);
            var dampingMatrix = lambda * identity;

            var jacobianProduct = jacobian * jacobianTranspose + dampingMatrix;

            var pseudoInverse = jacobianTranspose * jacobianProduct.Inverse();

            deltaTheta = pseudoInverse * errorVector;
        }

        // Update joint angles
        for (int i = 0; i < joints.Length; i++) 
        {
            jointAngles[i] += deltaTheta[i];

            // Apply joint limits
            float lowerLimit = joints[i].xDrive.lowerLimit * Mathf.Deg2Rad;
            float upperLimit = joints[i].xDrive.upperLimit * Mathf.Deg2Rad;
            jointAngles[i] = Mathf.Clamp(jointAngles[i], lowerLimit, upperLimit);

            // Update the joint target value
            var drive = joints[i].xDrive;
            drive.target = jointAngles[i] * Mathf.Rad2Deg;
            joints[i].xDrive = drive;
        }
    }
}
