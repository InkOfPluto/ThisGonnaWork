using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GripState { Fixed = 0, Opening = -1, Closing = 1 };

public class PincherController : MonoBehaviour
{
    public List<GameObject> fingers = new List<GameObject>();

    private List<PincherFingerController> fingerControllers = new List<PincherFingerController>();

    public float grip;
    public float gripSpeed = 3.0f;
    public GripState gripState = GripState.Fixed;


    void Start()
    {
        foreach (GameObject finger in fingers)
        {
            var controller = finger.GetComponent<PincherFingerController>();
            if (controller != null)
            {
                fingerControllers.Add(controller);
            }
            else
            {
                Debug.LogWarning($"手指对象 {finger.name} 上缺少 PincherFingerController 脚本！");
            }
        }
    }

    void FixedUpdate()
    {
        UpdateGrip();
        UpdateFingersForGrip();
    }



    public float CurrentGrip()
    {
        if (fingerControllers.Count == 0) return 0.0f;

        float sum = 0f;
        foreach (var controller in fingerControllers)
        {
            sum += controller.CurrentGrip();
        }
        return sum / fingerControllers.Count;
    }

    public Vector3 CurrentGraspCenter()
    {
        if (fingerControllers.Count == 0) return transform.position;

        Vector3 sum = Vector3.zero;
        foreach (var controller in fingerControllers)
        {
            sum += controller.GetOpenPosition();
        }
        Vector3 localCenter = sum / fingerControllers.Count;
        return transform.TransformPoint(localCenter);
    }

    public void ResetGripToOpen()
    {
        grip = 0.0f;
        foreach (var controller in fingerControllers)
        {
            controller.ForceOpen(transform);
        }
        gripState = GripState.Fixed;
    }

    void UpdateGrip()
    {
        if (gripState != GripState.Fixed)
        {
            float gripChange = (float)gripState * gripSpeed * Time.fixedDeltaTime;
            float gripGoal = CurrentGrip() + gripChange;
            grip = Mathf.Clamp01(gripGoal);
            Debug.Log("[🖐️] GripState: " + gripState + " | Grip: " + grip);
            Debug.Log("Grip val: " + grip.ToString());
        }
    }

    void UpdateFingersForGrip()
    {
        foreach (var controller in fingerControllers)
        {
            controller.UpdateGrip(grip);
        }
    }


}