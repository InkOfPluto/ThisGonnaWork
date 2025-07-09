using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraspController : MonoBehaviour
{

    public PincherController pinchController;
    public Transform[] fingerTips;
    public Transform thumbtip;
    public float minVal;
    public float maxVal;

    void Start()
    {
        pinchController = FindAnyObjectByType<PincherController>();
    }

    // Update is called once per frame
    void Update()
    {

        float fingertipDist = 0f;

        foreach (Transform t in fingerTips)
        {
            fingertipDist += Vector3.Distance(thumbtip.position, t.position);  
        }
        float avfingertipDist = fingertipDist / fingerTips.Length;

        //Debug.Log("AVERAGE DIST: " + avfingertipDist.ToString());

        //float clampedFingerDist = Mathf.Clamp01(avfingertipDist);
        float clampedFingerDist = mapvals(avfingertipDist); 
        //Debug.Log("Clamedr DIST: " + clampedFingerDist.ToString());

        //pinchController.grip = clampedFingerDist; 
        //pinchController.grip = 0.1f;
    }

    float mapvals(float input_val)
    {
        float outputval = 0f;

        outputval = (input_val - minVal) / (maxVal - minVal);
        //.,,
        return outputval; 
    }
}
