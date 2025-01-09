/*
 
    This is the main script responsible for driving the articulation bodies of the target manipulator/hand/arm etc. 
    
    Author: Diar Abdlakrim
    Email: contact@obirobotics.com
    Date: 21st December 2019
    
    This software is propriatery and may not be used, copied, modified, or distributed 
    for any commercial purpose without explicit written permission Obi Robotics Ltd (R) 2024.
 */


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
//using NumpyDotNet; 
//using NumSharp;


public class ArticulationDriver : MonoBehaviour
{
    // Physics body driver
    public ArticulationBody _palmBody;
    public Transform driverHand;

    public ArticulationBody[] articulationBods;
    public Vector3 driverHandOffset;
    public Vector3 rotataionalOffset;
    public MeshCollider[] _meshColliders;
    public MeshCollider[] _palmColliders;

    ArticulationBody thisArticulation; // Root-Parent articulation body 
    float xTargetAngle, yTargetAngle = 0f;

    [Range(-90f, 90f)]
    public float angle = 0f;


    void Start()
    {
        thisArticulation = GetComponent<ArticulationBody>();
        //StartCoroutine(UpdateArtHand());
    }

    void FixedUpdate()
    {
        #region End-Effector Positioning
        // Counter Gravity; force = mass * acceleration
        _palmBody.AddForce(-Physics.gravity * _palmBody.mass);
        foreach (ArticulationBody body in articulationBods)
        {
            //int dofs = body.jointVelocity.dofCount;
            float velLimit = 1.75f;
            body.maxAngularVelocity = velLimit;
            body.maxDepenetrationVelocity = 3f;

            body.AddForce(-Physics.gravity * body.mass);
        }

        // Apply tracking position velocity; force = (velocity * mass) / deltaTime
        float massOfHand = _palmBody.mass; // + (N_FINGERS * N_ACTIVE_BONES * _perBoneMass);
        Vector3 palmDelta = ((driverHand.transform.position + driverHandOffset) +
          (driverHand.transform.rotation * Vector3.back * driverHandOffset.x) +
          (driverHand.transform.rotation * Vector3.up * driverHandOffset.y)) - _palmBody.worldCenterOfMass;

        // Setting velocity sets it on all the joints, adding a force only adds to root joint
        float alpha = 0.05f; // Blend between existing velocity and all new velocity
        _palmBody.velocity *= alpha;
        _palmBody.AddForce(Vector3.ClampMagnitude((((palmDelta / Time.fixedDeltaTime) / Time.fixedDeltaTime) * (_palmBody.mass + (1f * 5))) * (1f - alpha), 8000f * 1f));

        // Apply tracking rotation velocity 
        // TODO: Compensate for phantom forces on strongly misrotated appendages
        // AddTorque and AngularVelocity both apply to ALL the joints in the chain
        Quaternion palmRot = _palmBody.transform.rotation * Quaternion.Euler(rotataionalOffset);
        Quaternion rotation = driverHand.transform.rotation * Quaternion.Inverse(palmRot);
        Vector3 angularVelocity = Vector3.ClampMagnitude((new Vector3(
          Mathf.DeltaAngle(0, rotation.eulerAngles.x),
          Mathf.DeltaAngle(0, rotation.eulerAngles.y),
          Mathf.DeltaAngle(0, rotation.eulerAngles.z)) / Time.fixedDeltaTime) * Mathf.Deg2Rad, 45f * 1f);

        _palmBody.angularVelocity = angularVelocity;
        _palmBody.angularDamping = 0.5f;
        #endregion

        #region End-Effector Orienting
        // Get the local axes
        Vector3 endEffectorXAxis = _palmBody.transform.right; // Local x-axis
        Vector3 driverHandZAxis = driverHand.forward;          // Local z-axis of driverHand

        // Project the axes onto the plane perpendicular to the axis of rotation (local y-axis)
        Vector3 rotationAxis = _palmBody.transform.up; // Axis of rotation (local y-axis)

        Vector3 projectedEndEffectorX = Vector3.ProjectOnPlane(endEffectorXAxis, rotationAxis);
        Vector3 projectedDriverHandZ = Vector3.ProjectOnPlane(driverHandZAxis, rotationAxis);

        // Calculate the angle between the projected vectors
        float angleToRotate = Vector3.SignedAngle(projectedEndEffectorX, projectedDriverHandZ, rotationAxis);



        // Get the revolute joint (assuming it's the last in the array)
        ArticulationBody revoluteJoint = articulationBods[articulationBods.Length - 1];

        // Get the current drive
        ArticulationDrive drive = revoluteJoint.xDrive; // Assuming rotation around x-axis

        // Adjust the target angle
        drive.target = angleToRotate;

        // Apply the drive back to the joint
        revoluteJoint.xDrive = drive;


        // Adjust joint limits if necessary
        drive.lowerLimit = Mathf.Min(drive.lowerLimit, angleToRotate);
        drive.upperLimit = Mathf.Max(drive.upperLimit, angleToRotate);
        revoluteJoint.xDrive = drive;


        #endregion

        // This is due to Unity bug. And I am mitigating it here. 
        #region Stabilize ArticulationBody / Prevent Random Jittering
        foreach (MeshCollider collider in _palmColliders)
        {
            collider.enabled = false;
        }
        foreach (MeshCollider collider in _meshColliders)
        {
            collider.enabled = false;
        }
        for (int a = 0; a < articulationBods.Length; a++)
        {
            articulationBods[a].jointVelocity = new ArticulationReducedSpace(0f, 0f, 0f);
            articulationBods[a].velocity = Vector3.zero;
            articulationBods[a].angularVelocity = Vector3.zero;
        }
        foreach (MeshCollider collider in _palmColliders)
        {
            collider.enabled = true;
        }
        foreach (MeshCollider collider in _meshColliders)
        {
            collider.enabled = true;
        }
        #endregion

    }

}