using UnityEngine;

public class MoveArticulationBody : MonoBehaviour
{
    public float movementSpeed = 5f;
    public float rotationSpeed = 50f;
    public ArticulationBody myArticulationBody; // Assign in the Inspector

    void Update()
    {
        if (myArticulationBody != null)
        {
            // Movement
            float verticalInput = 0f;

            if (Input.GetKey(KeyCode.W))
                verticalInput =0.01f;

            if (Input.GetKey(KeyCode.S))
                verticalInput = -0.01f;

            // Teleport root articulation body
            if (verticalInput != 0)
            {
                Vector3 moveDirection = new Vector3(0f, verticalInput, 0f).normalized;
                myArticulationBody.TeleportRoot(myArticulationBody.transform.position + moveDirection * movementSpeed * Time.deltaTime, Quaternion.identity);
                myArticulationBody.velocity = Vector3.zero; // Reset velocity after teleporting
                myArticulationBody.angularVelocity = Vector3.zero;
            }
            //// Rotation
            //float rotationInput = Input.GetAxis("Mouse X");
            //if (rotationInput != 0)
            //{
            //    myArticulationBody.TeleportRoot(myArticulationBody.transform.position, myArticulationBody.transform.rotation * Quaternion.Euler(0, rotationInput * rotationSpeed * Time.deltaTime, 0));
            //    myArticulationBody.velocity = Vector3.zero;
            //    myArticulationBody.angularVelocity = Vector3.zero;
            //}
        }
    }
}