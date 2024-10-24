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

public class ArticulationDriver : MonoBehaviour
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
    public MeshCollider[] _meshColliders;
    public MeshCollider[] _palmColliders;
    public TMP_Text infoText;

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

        // *******************************************************************************************
        // *******************************************************************************************
        // *******************************************************************************************

        for (int i = 0; i < driverJoints.Length; i++)
        {
            float ang_targX = 0f;
            float tempAng = 0f;
            float tempAngY = 0f;
            float ang_targY = 0f;

            // # Add more thumb DoFs. Perhaps change thumb joint type form revolute to spherical 
            if (driverJoints[i].name.Contains("thumb"))
            {
                // For the left and right hand thumb joint 0 the direction in one of the axis is reversed 
                if (handedness == Hand.Right)
                {
                    if (driverJoints[i].name.Contains("thumb0"))
                    {
                        // X-Axis joint control
                        tempAng = driverJoints[i].transform.localRotation.eulerAngles.z;
                        if (tempAng < 100f) { xTargetAngle = tempAng + 360f; }
                        else { xTargetAngle = tempAng; }
                        xTargetAngle = tempAng;
                        ang_targX = map(xTargetAngle, 365f, 300f, -29f, 29f); // angle;
                                                                              //infoText.text = "ProxyHand v0.2 \nAngles: " + xTargetAngle.ToString("F2");

                        // Z-Axis joint control
                        tempAngY = driverJoints[i].transform.localRotation.eulerAngles.y;
                        if (tempAngY < 100f) { yTargetAngle = tempAngY + 360f; }
                        else { yTargetAngle = tempAngY; }
                        yTargetAngle = tempAngY;
                        ang_targY = map(yTargetAngle, 300f, 285f, -15, 5f);
                        //infoText.text = "ProxyHand v0.2 \nAngles: " + ang_targY.ToString("F2");
                    }
                    else if (driverJoints[i].name.Contains("thumb1"))
                    {
                        tempAngY = driverJoints[i].transform.localRotation.eulerAngles.y;
                        if (tempAngY < 100f) { yTargetAngle = tempAngY + 360f; }
                        else { yTargetAngle = tempAngY; }
                        yTargetAngle = tempAngY;
                        ang_targY = map(yTargetAngle, 330f, 25f, -10f, 50f);
                        //infoText.text = "ProxyHand v0.1 \nAngles: " + ang_targY.ToString("F2");
                    }
                    else
                    {
                        tempAng = driverJoints[i].transform.localRotation.eulerAngles.z;
                        if (tempAng < 100f) { xTargetAngle = tempAng + 360f; }
                        else { xTargetAngle = tempAng; }

                        ang_targX = map(xTargetAngle, 380f, 300f, -40f, 80f);
                        //RotateTo(articulationBods[i], ang_targ);
                    }
                }
                else if (handedness == Hand.Left)
                {
                    if (driverJoints[i].name.Contains("thumb0"))
                    {
                        // X-Axis joint control
                        tempAng = driverJoints[i].transform.localRotation.eulerAngles.z;
                        if (tempAng < 100f) { xTargetAngle = tempAng + 360f; }
                        else { xTargetAngle = tempAng; }
                        xTargetAngle = tempAng;
                        ang_targX = map(xTargetAngle, 365f, 300f, -29f, 29f); // angle;
                        infoText.text = "ARiHand v0.2 \nAngles: " + xTargetAngle.ToString("F2");

                        // Z-Axis joint control
                        tempAngY = driverJoints[i].transform.localRotation.eulerAngles.y;
                        if (tempAngY < 100f) { yTargetAngle = tempAngY + 360f; }
                        else { yTargetAngle = tempAngY; }
                        yTargetAngle = tempAngY;
                        ang_targY = map(yTargetAngle, 300f, 285f, -15, 5f);
                        //infoText.text = "ProxyHand v0.2 \nAngles: " + ang_targY.ToString("F2");
                    }
                    else if (driverJoints[i].name.Contains("thumb1"))
                    {
                        tempAngY = driverJoints[i].transform.localRotation.eulerAngles.y;
                        if (tempAngY < 100f) { yTargetAngle = tempAngY + 360f; }
                        else { yTargetAngle = tempAngY; }
                        yTargetAngle = tempAngY;
                        ang_targY = map(yTargetAngle, 330f, 25f, -10f, 50f);
                        //infoText.text = "ProxyHand v0.1 \nAngles: " + ang_targY.ToString("F2");
                    }
                    else
                    {
                        tempAng = driverJoints[i].transform.localRotation.eulerAngles.z;
                        if (tempAng < 100f) { xTargetAngle = tempAng + 360f; }
                        else { xTargetAngle = tempAng; }

                        ang_targX = map(xTargetAngle, 380f, 300f, -40f, 80f);
                        //RotateTo(articulationBods[i], ang_targ);
                    }
                }

                    RotateTo(articulationBods[i], ang_targX, ang_targY);
            }
            else
            {
                tempAng = driverJoints[i].transform.localRotation.eulerAngles.z;
                if (tempAng < 100f) { xTargetAngle = tempAng + 360f; }
                else { xTargetAngle = tempAng; }
                ang_targX = map(xTargetAngle, 372f, 270f, -10f, 85f);
                //RotateTo(articulationBods[i], ang_targ);

                tempAngY = driverJoints[i].transform.localRotation.eulerAngles.x;
                //if (tempAngY < 100f) { yTargetAngle = tempAngY + 360f; }
                //else { yTargetAngle = tempAngY; }
                yTargetAngle = tempAngY;
                ang_targY = map(yTargetAngle, 10, 1f, 0f, 10f);
                //infoText.text = "ProxyHand v0.1 \nAngles: " + yTargetAngle.ToString("F2") + " Mapped Angle: " + ang_targY.ToString("F2");
                RotateTo(articulationBods[i], ang_targX, ang_targY);
            }
        }
    }

    // Coroutine is too slow 
    IEnumerator UpdateArtHand()
    {
        while (true)
        {
            //// Counter Gravity; force = mass * acceleration
            //_palmBody.AddForce(-Physics.gravity * _palmBody.mass);
            //foreach (ArticulationBody body in _articulationBodies)
            //{
            //    //int dofs = body.jointVelocity.dofCount;
            //    float velLimit = 1.75f;
            //    body.maxAngularVelocity = velLimit;
            //    body.maxDepenetrationVelocity = 3f;

            //    body.AddForce(-Physics.gravity * body.mass);
            //}

            // *******************************************************************************************
            // *******************************************************************************************
            // *******************************************************************************************

            #region Settings for Articulation Body 
            foreach (MeshCollider collider in _palmColliders)
            {
                collider.enabled = false;
                yield return null;
            }

            foreach (MeshCollider collider in _meshColliders)
            {
                collider.enabled = false;
                yield return null;
            }
            for (int a = 0; a < articulationBods.Length; a++)
            {
                articulationBods[a].jointVelocity = new ArticulationReducedSpace(0f, 0f, 0f);
                articulationBods[a].velocity = Vector3.zero;
                articulationBods[a].angularVelocity = Vector3.zero;
                yield return null;
            }
            foreach (MeshCollider collider in _palmColliders)
            {
                collider.enabled = true;
                yield return null;
            }
            foreach (MeshCollider collider in _meshColliders)
            {
                collider.enabled = true;
                yield return null;
            }
            #endregion

            // *******************************************************************************************
            // *******************************************************************************************
            // *******************************************************************************************

            Quaternion rotWithOffset = driverHandRoot.rotation * Quaternion.Euler(rotataionalOffset);
            thisArticulation.TeleportRoot(driverHandRoot.position, rotWithOffset);

            int i = 0;
            int j = 0;
            //Transform prevBone, bone; 
            foreach (ArticulationBody artBod in articulationBods)
            {


                Transform prevBone = driverJoints[i];
                print("i1: " + i.ToString());

                i++;
                j = i % (articulationBods.Length);
                //Debug.Log("j: " + j + " i: " + i);

                Transform bone = driverJoints[j];
                print("i2: " + j.ToString());

                //float xTargetAngle = AngleBetween(prevBone.position, bone.position);
                //VelocityDrive(artBod, xTargetAngle);


                RotateTo(artBod, xTargetAngle);

                xTargetAngle = Vector3.SignedAngle(
                            prevBone.localRotation * Vector3.right,
                            bone.transform.localRotation * Vector3.right,
                            prevBone.transform.localRotation * Vector3.up);

                infoText.text = "ProxyHand v0.1 \nAngles: " + xTargetAngle.ToString("F2");
                yield return null;
            }

            yield return null;
        }
    }

    void RotateTo(ArticulationBody body, float targetTor)
    {
        body.xDrive = new ArticulationDrive()
        {
            stiffness = body.xDrive.stiffness,
            forceLimit = body.xDrive.forceLimit,
            damping = body.xDrive.damping,
            lowerLimit = body.xDrive.lowerLimit,
            upperLimit = body.xDrive.upperLimit,
            target = targetTor
        };
    }

    void RotateTo(ArticulationBody body, float targetTorX, float targetTorY)
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
        body.zDrive = new ArticulationDrive()
        {
            stiffness = body.zDrive.stiffness,
            forceLimit = body.zDrive.forceLimit,
            damping = body.zDrive.damping,
            lowerLimit = body.zDrive.lowerLimit,
            upperLimit = body.zDrive.upperLimit,
            target = targetTorY
        };
    }

    void RotateTo(ArticulationBody articulation, Vector3 targetTor)
    {
        #region Approach 1
        //articulation.xDrive = new ArticulationDrive()
        //{
        //    target = targetTor.x
        //};

        //articulation.yDrive = new ArticulationDrive()
        //{
        //    target = targetTor.y
        //};

        //articulation.zDrive = new ArticulationDrive()
        //{
        //    target = targetTor.z
        //};
        #endregion

        #region Approach 2
        var driveX = articulation.xDrive;
        driveX.target = targetTor.x;
        articulation.xDrive = driveX;

        var driveY = articulation.yDrive;
        driveY.target = targetTor.y;
        articulation.yDrive = driveY;

        var driveZ = articulation.zDrive;
        driveZ.target = targetTor.z;
        articulation.zDrive = driveZ;
        #endregion  
    }

    void VelocityDrive(ArticulationBody body, float xTargetAngle)
    {
        body.xDrive = new ArticulationDrive()
        {
            stiffness = body.xDrive.stiffness,
            forceLimit = body.xDrive.forceLimit,
            damping = body.xDrive.damping,
            lowerLimit = body.xDrive.lowerLimit,
            upperLimit = body.xDrive.upperLimit,
            target = xTargetAngle
        };
    }

    float AngleBetween(Vector3 v1, Vector3 v2)
    {
        Vector3 v1_u = v1.normalized;
        Vector3 v2_u = v2.normalized;

        float dotProd = Vector3.Dot(v1_u, v2_u);
        dotProd = Mathf.Clamp01(dotProd);
        float mappedVal = map(dotProd, 0f, 1f, -1f, 1f);

        float radians = Mathf.Acos(mappedVal);
        float degrees = (180f / Mathf.PI) * radians;
        Debug.Log("degrees: " + degrees);

        return degrees;
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