using UnityEngine;

public class RoboticArmIK : MonoBehaviour
{
    [Header("IK Settings")]
    public int ChainLength = 7;
    public Transform Target;
    public Transform[] Joints;

    [Header("Solver Parameters")]
    public int Iterations = 10;
    public float Delta = 0.001f;
    [Range(0, 1)]
    public float SnapBackStrength = 1f;

    private float[] BonesLength;
    private float CompleteLength;
    private Vector3[] Positions;
    private Quaternion[] StartRotations;
    private Quaternion TargetRotation;

    void Awake()
    {
        Init();
    }

    void Init()
    {
        // Initialize arrays
        BonesLength = new float[ChainLength];
        Positions = new Vector3[ChainLength + 1];
        StartRotations = new Quaternion[ChainLength + 1];

        // Total length of the arm
        CompleteLength = 0;

        // Calculate bones lengths and store initial rotations
        for (int i = 0; i < ChainLength; i++)
        {
            var current = Joints[i];
            var next = Joints[i + 1];

            BonesLength[i] = Vector3.Distance(current.position, next.position);
            CompleteLength += BonesLength[i];

            StartRotations[i] = current.localRotation;
        }
        StartRotations[ChainLength] = Joints[ChainLength].localRotation;

        TargetRotation = Target.rotation;
    }

    void LateUpdate()
    {
        ResolveIK();
    }

    void ResolveIK()
    {
        if (Target == null)
            return;

        // Get positions
        for (int i = 0; i <= ChainLength; i++)
        {
            Positions[i] = Joints[i].position;
        }

        // Check if target is reachable
        if ((Target.position - Joints[0].position).sqrMagnitude >= CompleteLength * CompleteLength)
        {
            // Stretch towards the target
            Vector3 direction = (Target.position - Positions[0]).normalized;
            for (int i = 1; i <= ChainLength; i++)
            {
                Positions[i] = Positions[i - 1] + direction * BonesLength[i - 1];
            }
        }
        else
        {
            // FABRIK algorithm
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                // Backward reaching
                Positions[ChainLength] = Target.position;
                for (int i = ChainLength - 1; i >= 0; i--)
                {
                    Positions[i] = Positions[i + 1] + (Positions[i] - Positions[i + 1]).normalized * BonesLength[i];
                }

                // Forward reaching
                Positions[0] = Joints[0].position;
                for (int i = 1; i <= ChainLength; i++)
                {
                    Positions[i] = Positions[i - 1] + (Positions[i] - Positions[i - 1]).normalized * BonesLength[i - 1];
                }

                // Check for convergence
                if ((Positions[ChainLength] - Target.position).sqrMagnitude < Delta * Delta)
                    break;
            }
        }

        // Apply positions and rotations
        for (int i = 0; i < ChainLength; i++)
        {
            Vector3 dir = Positions[i + 1] - Positions[i];
            Joints[i].rotation = Quaternion.FromToRotation(Joints[i].TransformDirection(Vector3.right), dir.normalized) * Joints[i].rotation;

            // Apply joint constraints (-180 to 180 degrees)
            Vector3 eulerAngles = Joints[i].localEulerAngles;
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, -180f, 180f);
            Joints[i].localEulerAngles = eulerAngles;

            Joints[i].position = Positions[i];
        }

        // Align end-effector's orientation
        Joints[ChainLength].rotation = Target.rotation;
    }

    void OnDrawGizmos()
    {
        if (Joints == null || Joints.Length == 0)
            return;

        // Draw bones
        for (int i = 0; i < Joints.Length - 1; i++)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(Joints[i].position, Joints[i + 1].position);
        }

        // Draw target
        if (Target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(Target.position, 0.05f);
        }
    }
}
