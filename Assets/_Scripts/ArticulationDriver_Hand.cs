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

public enum Hand
{
    None,
    Right,
    Left,
};

public class ArticulationDriver_Hand : MonoBehaviour
{
    public Hand handedness = Hand.Right;

    // Physics body driver
    public ArticulationBody _palmBody;
    public Transform driverHand;

    public Transform[] driverJoints;
    public ArticulationBody[] articulationBods;
    public Transform driverHandRoot;
    public Vector3 driverHandOffset;
    public Vector3 rotataionalOffset;
    public CapsuleCollider[] _capsuleColliders;
    public BoxCollider[] _palmColliders;
    public TMP_Text infoText;

    ArticulationBody thisArticulation; // Root-Parent articulation body 
    float xTargetAngle, yTargetAngle, zTargetAngle = 0f;
    float[] minx, miny, minz, maxx, maxy, maxz = new float[15];

    [Range(-90f, 90f)]
    public float angle = 0f;


    void Start()
    {
        thisArticulation = GetComponent<ArticulationBody>();
        //StartCoroutine(UpdateArtHand());
    }

    void FixedUpdate()
    {
        //// Wrist/Hand movement 
        //Quaternion rotWithOffset = driverHandRoot.rotation * Quaternion.Euler(rotataionalOffset); ;
        //thisArticulation.TeleportRoot(driverHandRoot.position, rotWithOffset);

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
        //_palmBody.velocity = Vector3.zero;
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
        //palmBody.angularVelocity = Vector3.zero;
        //palmBody.AddTorque(angularVelocity);
        _palmBody.angularVelocity = angularVelocity;
        _palmBody.angularDamping = 50f;

        // *******************************************************************************************
        // *******************************************************************************************
        // *******************************************************************************************

        #region Stabilize ArticulationBody / Prevent Random Jittering
        foreach (BoxCollider collider in _palmColliders)
        {
            collider.enabled = false;
        }
        foreach (CapsuleCollider collider in _capsuleColliders)
        {
            collider.enabled = false;
        }
        for (int a = 0; a < articulationBods.Length; a++)
        {
            articulationBods[a].jointVelocity = new ArticulationReducedSpace(0f, 0f, 0f);
            articulationBods[a].velocity = Vector3.zero;
            articulationBods[a].angularVelocity = Vector3.zero;
        }
        foreach (BoxCollider collider in _palmColliders)
        {
            collider.enabled = true;
        }
        foreach (CapsuleCollider collider in _capsuleColliders)
        {
            collider.enabled = true;
        }
        #endregion

        // *******************************************************************************************
        // *******************************************************************************************
        // *******************************************************************************************

        //if(Input.GetKeyDown(KeyCode.M))
        //{

        //}


        for (int i = 0; i < driverJoints.Length; i++)
        {
            float tempAngX = 0f;
            float tempAngY = 0f;
            float tempAngZ = 0f;

            float ang_targX = 0f;
            float ang_targY = 0f;
            float ang_targZ = 0f; 

            // For the left and right hand thumb joint 0 the direction in one of the axis is reversed 
            if (handedness == Hand.Right)
            {

                // Adduction-Abduction
                //tempAngX = driverJoints[i].transform.localRotation.eulerAngles.x;
                //if (tempAngX < 100f) { xTargetAngle = tempAngX + 360f; }
                //else { xTargetAngle = tempAngX; }
                //if (xTargetAngle < minx[i]) { minx[i] = xTargetAngle; }
                //if (xTargetAngle > maxx[i]) { maxx[i] = xTargetAngle; }
                //ang_targX = map(xTargetAngle, minx[i], maxx[i], -10f, 10f);
                //ang_targX = xTargetAngle;

                //// Flexion-Extention
                //tempAngY = driverJoints[i].transform.localRotation.eulerAngles.y;
                //if (tempAngY < 100f) { xTargetAngle = tempAngY + 360f; }
                //else { yTargetAngle = tempAngY; }
                //if(yTargetAngle < miny[i]) { miny[i] =  yTargetAngle; }
                //if(yTargetAngle > maxy[i]) { maxy[i] = yTargetAngle; }
                //ang_targX = map(yTargetAngle, miny[i], maxy[i], -35f, 35f);

                //// Thumb rotation only on MCP joint?
                //tempAngZ = driverJoints[i].transform.localRotation.eulerAngles.z;
                //if (tempAngZ < 100f) { zTargetAngle = tempAngZ + 360f; }
                //else { zTargetAngle = tempAngZ; }
                //if (zTargetAngle < minz[i]) { minz[i] = zTargetAngle; }
                //if (zTargetAngle > maxz[i]) { maxz[i] = zTargetAngle; }
                //ang_targZ = map(zTargetAngle, minz[i], maxz[i], -35f, 35f);

                if (driverJoints[i].gameObject.name.Contains("IndexProx"))
                {
                    tempAngX = driverJoints[i].transform.localRotation.eulerAngles.x;

                    infoText.text = "Angle: " + tempAngX.ToString();
                }

                //if (driverJoints[i].tag.Contains("thumb0"))
                //{
                //    // X-Axis joint control
                //    tempAngX = driverJoints[i].transform.localRotation.eulerAngles.z;
                //    if (tempAngX < 100f) { xTargetAngle = tempAngX + 360f; }
                //    else { xTargetAngle = tempAngX; }
                //    xTargetAngle = tempAngX;
                //    ang_targX = map(xTargetAngle, 365f, 300f, -29f, 29f);

                //    tempAngY = driverJoints[i].transform.localRotation.eulerAngles.y;
                //    if (tempAngY < 100f) { yTargetAngle = tempAngY + 360f; }
                //    else { yTargetAngle = tempAngY; }
                //    yTargetAngle = tempAngY;
                //    ang_targY = map(yTargetAngle, 300f, 285f, -15, 5f);
                //}
                //else if (driverJoints[i].tag.Contains("thumb1"))
                //{
                //    tempAngY = driverJoints[i].transform.localRotation.eulerAngles.y;
                //    if (tempAngY < 100f) { yTargetAngle = tempAngY + 360f; }
                //    else { yTargetAngle = tempAngY; }
                //    yTargetAngle = tempAngY;
                //    ang_targY = map(yTargetAngle, 330f, 25f, -10f, 50f);
                //}
                //else if (driverJoints[i].tag.Contains("thumb2"))
                //{
                //    tempAngX = driverJoints[i].transform.localRotation.eulerAngles.z;
                //    if (tempAngX < 100f) { xTargetAngle = tempAngX + 360f; }
                //    else { xTargetAngle = tempAngX; }

                //    ang_targX = map(xTargetAngle, 380f, 300f, -40f, 80f);
                //}
                //else
                //{
                //    tempAngX = driverJoints[i].transform.localRotation.eulerAngles.z;
                //    if (tempAngX < 100f) { xTargetAngle = tempAngX + 360f; }
                //    else { xTargetAngle = tempAngX; }
                //    ang_targX = map(xTargetAngle, 372f, 270f, -10f, 85f);

                //    tempAngY = driverJoints[i].transform.localRotation.eulerAngles.x;

                //    yTargetAngle = tempAngY;
                //    ang_targY = map(yTargetAngle, 10, 1f, 0f, 10f);
                //}

                RotateTo(articulationBods[i], ang_targX, ang_targY, ang_targZ);
            }
        }
    }

    void RotateTo(ArticulationBody body, float targetTorX = 0f, float targetTorY = 0f, float targetTorZ = 0f)
    {
        body.xDrive = new ArticulationDrive()
        {
            stiffness = body.xDrive.stiffness,
            forceLimit = body.xDrive.forceLimit,
            damping = body.xDrive.damping,
            lowerLimit = body.xDrive.lowerLimit,
            upperLimit = body.xDrive.upperLimit,
            target = targetTorX
        };
        body.yDrive = new ArticulationDrive()
        {
            stiffness = body.yDrive.stiffness,
            forceLimit = body.yDrive.forceLimit,
            damping = body.yDrive.damping,
            lowerLimit = body.yDrive.lowerLimit,
            upperLimit = body.yDrive.upperLimit,
            target = targetTorY
        };
        body.zDrive = new ArticulationDrive()
        {
            stiffness = body.zDrive.stiffness,
            forceLimit = body.zDrive.forceLimit,
            damping = body.zDrive.damping,
            lowerLimit = body.zDrive.lowerLimit,
            upperLimit = body.zDrive.upperLimit,
            target = targetTorZ
        };
    }

    public static float map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }
}


// Code graveyard

//float xTargetAngle = AngleBetween(prevBone.position, bone.position);
//VelocityDrive(artBod, xTargetAngle);

//var driveX = articulation.xDrive;
//driveX.target = targetTor;
//articulation.xDrive = driveX;


//Vector3.SignedAngle(driverJoints[0].transform.localRotation * Vector3.right,
//                                   driverJoints[1].transform.localRotation * Vector3.right,
//                                   driverJoints[0].transform.localRotation * Vector3.down);

//xTargetAngle = Vector3.SignedAngle(driverJoints[1].transform.localRotation * Vector3.right,
//                           driverJoints[2].transform.localRotation * Vector3.right,
//                           driverJoints[1].transform.localRotation * Vector3.up);
//RotateTo(articulationBods[1], xTargetAngle);

//xTargetAngle = Vector3.SignedAngle(driverJoints[2].transform.localRotation * Vector3.left,
//                   driverJoints[1].transform.localRotation * Vector3.left,
//                   driverJoints[2].transform.localRotation * Vector3.down);
//RotateTo(articulationBods[2], xTargetAngle);
//infoText.text = "ProxyHand v0.1 \nAngles: " + xTargetAngle.ToString("F2");

//int i = 0;
//int j = 0;
//Transform prevBone, bone; 
//foreach (ArticulationBody artBod in articulationBods)
//{
//    if (i == 0)
//        infoText.text = "ProxyHand v0.1 \nAngles: " + xTargetAngle.ToString("F2");

//    Transform prevBone = driverJoints[i];
//    i++;
//    j = i % (articulationBods.Length);
//    Transform bone = driverJoints[j];

//    RotateTo(artBod, xTargetAngle);

//    Vector3 targetDir = prevBone.transform.position - bone.transform.position;
//    xTargetAngle = Vector3.Angle(targetDir, prevBone.transform.forward);

//    //if (i == 0 | i == 2)
//    //{
//    //    xTargetAngle = Vector3.SignedAngle(prevBone.localRotation * Vector3.right,
//    //                                       bone.transform.localRotation * Vector3.right,
//    //                                       prevBone.transform.localRotation * Vector3.up);
//    //}
//    //else
//    //{
//    //    xTargetAngle = Vector3.SignedAngle(prevBone.localRotation * Vector3.right,
//    //                                       bone.transform.localRotation * Vector3.right,
//    //                                       prevBone.transform.localRotation * Vector3.down);

//    //    infoText.text = "ProxyHand v0.1 \nAngles: " + xTargetAngle.ToString("F2");
//    //}
//}

