using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public enum SliderControl
{
    None,
    frame_rate,
    solver_iteration,
    velocity_iteration,
    contact_offset,
    angular_speed,
    wrist_squeeze,
};


public class SliderScript : MonoBehaviour
{
    public bool setDefault = false, recallDefaults = false;

    public SliderControl sliderControl; 

    public TMP_Text text_Slider;
    public Transform slider_Handle;
    
    private Vector2 valueRange;
    private float minSVal = 1f, maxSVal = 0f, sliderVal = 0f;
    private float mappedVals;
    private string sliderDisplay = "";

    private Coroutine slierRecallRoutine;
    private ArticulationBody sliderArtBod;

    public UDP_Comms udpComms; 

    private void Start()
    {
        Time.fixedDeltaTime = 1f /90f;

        sliderArtBod = slider_Handle.gameObject.GetComponent<ArticulationBody>();
        sliderArtBod.AddForce(new Vector3(0f,0f,-10f));

        if (slierRecallRoutine != null)
            StopCoroutine(slierRecallRoutine);
        slierRecallRoutine = StartCoroutine(RecallSliderDefaults());
    }

    void Update()
    {

        #region Map slider values between 0 and 1
        sliderVal = slider_Handle.position.z;
        float mappedSliderVal = map(sliderVal, minSVal, maxSVal, 0f, 1f);
        if (sliderVal < minSVal)
            minSVal = sliderVal;
        if (sliderVal > maxSVal)
            maxSVal = sliderVal;
        #endregion

        #region Map new mapped slider values to selected physics range
        if (sliderControl == SliderControl.frame_rate)
        {
            valueRange = new Vector2(30,150); // Hand typed 
            mappedVals = Mathf.Round(map(mappedSliderVal, 0f, 1f, valueRange.x, valueRange.y));
            float vals = 1f / mappedVals;
            Time.fixedDeltaTime = vals;
            sliderDisplay = "FPS";
            if(setDefault)
            {
                PlayerPrefs.SetFloat("sliderVal", sliderVal);
                PlayerPrefs.SetFloat("mappedVal", mappedVals);
                PlayerPrefs.SetFloat("minSVal", minSVal);
                PlayerPrefs.SetFloat("maxSVal", maxSVal);
                setDefault = false; 
            }
        }
        else if (sliderControl == SliderControl.solver_iteration)
        {
            valueRange = new Vector2(6, 30); // Hand typed 
            mappedVals = map(mappedSliderVal, 0f, 1f, valueRange.x, valueRange.y);
            Physics.defaultSolverIterations = Mathf.RoundToInt(mappedVals);
            sliderDisplay = "Solver";
            if (setDefault)
            {
                PlayerPrefs.SetFloat("sliderVal", sliderVal);
                PlayerPrefs.SetFloat("mappedVal", mappedVals);
                PlayerPrefs.SetFloat("minSVal", minSVal);
                PlayerPrefs.SetFloat("maxSVal", maxSVal);
                setDefault = false;
            }
        }
        else if (sliderControl == SliderControl.velocity_iteration)
        {
            valueRange = new Vector2(2, 40); // Hand typed 
            mappedVals = map(mappedSliderVal, 0f, 1f, valueRange.x, valueRange.y);
            Physics.defaultSolverVelocityIterations = Mathf.RoundToInt(mappedVals);
            sliderDisplay = "Velocity";
            if (setDefault)
            {
                PlayerPrefs.SetFloat("sliderVal", sliderVal);
                PlayerPrefs.SetFloat("mappedVal", mappedVals);
                PlayerPrefs.SetFloat("minSVal", minSVal);
                PlayerPrefs.SetFloat("maxSVal", maxSVal);
                setDefault = false;
            }
        }
        else if (sliderControl == SliderControl.contact_offset)
        {
            valueRange = new Vector2(0.001f, 0.01f); // Hand typed 
            mappedVals = map(mappedSliderVal, 0f, 1f, valueRange.x, valueRange.y);
            Physics.defaultContactOffset = mappedVals;
            sliderDisplay = "Offset";
            if (setDefault)
            {
                PlayerPrefs.SetFloat("sliderVal", sliderVal);
                PlayerPrefs.SetFloat("mappedVal", mappedVals);
                PlayerPrefs.SetFloat("minSVal", minSVal);
                PlayerPrefs.SetFloat("maxSVal", maxSVal);
                setDefault = false;
            }
        }
        else if (sliderControl == SliderControl.angular_speed)
        {
            valueRange = new Vector2(2, 20); // Hand typed 
            mappedVals = map(mappedSliderVal, 0f, 1f, valueRange.x, valueRange.y);
            Physics.defaultMaxAngularSpeed = Mathf.RoundToInt(mappedVals);
            sliderDisplay = "Ang. Speed";
            if (setDefault)
            {
                PlayerPrefs.SetFloat("sliderVal", sliderVal);
                PlayerPrefs.SetFloat("mappedVal", mappedVals);
                PlayerPrefs.SetFloat("minSVal", minSVal);
                PlayerPrefs.SetFloat("maxSVal", maxSVal);
                setDefault = false;
            }
        }
        else if (sliderControl == SliderControl.wrist_squeeze) // Set default values 
        {
            print("I am in squeeze mode!!!");

            valueRange = new Vector2(0, 180); // Hand typed 
            mappedVals = map(mappedSliderVal, 0f, 1f, valueRange.x, valueRange.y);
            //Physics.defaultMaxAngularSpeed = Mathf.RoundToInt(mappedVals);

            udpComms.SendMsgtoWrist(mappedVals);

            sliderDisplay = "Sqz. Force";
            if (setDefault)
            {
                PlayerPrefs.SetFloat("sliderVal", sliderVal);
                PlayerPrefs.SetFloat("mappedVal", mappedVals);
                PlayerPrefs.SetFloat("minSVal", minSVal);
                PlayerPrefs.SetFloat("maxSVal", maxSVal);
                setDefault = false;
            }
        }
        else if (sliderControl == SliderControl.None) // Set default values 
        {
            Time.fixedDeltaTime = 0.011f;
            Physics.defaultSolverIterations = 20;
            Physics.defaultSolverVelocityIterations = 5;
            Physics.defaultContactOffset = 0.001f;
            Physics.defaultMaxAngularSpeed = 7;
        }


        text_Slider.text = sliderDisplay + "\n" + mappedVals.ToString("F3");
        #endregion

        #region Set previously created default values, if they exist
        if(recallDefaults)
        {
            if (slierRecallRoutine != null)
                StopCoroutine(slierRecallRoutine);
            slierRecallRoutine = StartCoroutine(RecallSliderDefaults());
            recallDefaults = false;
        }
        #endregion
    }

    IEnumerator RecallSliderDefaults()
    {
        float defaultSliderVal = PlayerPrefs.GetFloat("sliderVal");
        mappedVals = PlayerPrefs.GetFloat("mappedVal");
        minSVal = PlayerPrefs.GetFloat("minSVal");
        maxSVal = PlayerPrefs.GetFloat("maxSVal");

        for (int i = 0; i < 100; i++)
        {
            slider_Handle.position = new Vector3(slider_Handle.position.x,
                                                   slider_Handle.position.y,
                                                   defaultSliderVal);
            yield return null;
        }
    }

    public static float map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
    {
        return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
    }
}
