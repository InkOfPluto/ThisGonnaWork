using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GripState { Fixed = 0, Opening = -1, Closing = 1 };

public class PincherController : MonoBehaviour
{
    [Tooltip("添加 1 到 5 个手指 GameObject")]
    public List<GameObject> fingers = new List<GameObject>();

    [Tooltip("每个手指对应的旋转中心（例如 HandE）")]
    public List<Transform> rotationCenters = new List<Transform>();

    [Tooltip("手指之间最小允许距离（米），防止重叠")]
    public float minFingerDistance = 0.05f;

    private List<PincherFingerController> fingerControllers = new List<PincherFingerController>();

    public float grip;
    public float gripSpeed = 3.0f;
    public GripState gripState = GripState.Fixed;

    private List<int> fingersToRotateCW = new List<int>();
    private List<int> fingersToRotateCCW = new List<int>();
    private const float rotationAngle = 10f;

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

    void Update()
    {
        CheckFingerRotationInput();
    }

    void LateUpdate()
    {
        foreach (int i in fingersToRotateCW)
        {
            RotateFingerAroundCenter(fingers[i], rotationCenters[i], rotationAngle);
        }

        foreach (int i in fingersToRotateCCW)
        {
            RotateFingerAroundCenter(fingers[i], rotationCenters[i], -rotationAngle);
        }

        fingersToRotateCW.Clear();
        fingersToRotateCCW.Clear();
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

    void CheckFingerRotationInput()
    {
        // 每个手指的逆/顺时针键
        KeyCode[] ccwKeys = { KeyCode.Y, KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P };
        KeyCode[] cwKeys = { KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon };

        for (int i = 0; i < fingers.Count && i < rotationCenters.Count; i++)
        {
            if (i < ccwKeys.Length && Input.GetKeyDown(ccwKeys[i]))
            {
                if (!fingersToRotateCCW.Contains(i))
                    fingersToRotateCCW.Add(i);
            }

            if (i < cwKeys.Length && Input.GetKeyDown(cwKeys[i]))
            {
                if (!fingersToRotateCW.Contains(i))
                    fingersToRotateCW.Add(i);
            }
        }
    }

    void RotateFingerAroundCenter(GameObject finger, Transform center, float degrees)
    {
        Vector3 axis = Vector3.up;
        Vector3 centerPos = center.position;
        Vector3 currentPos = finger.transform.position;

        // 模拟旋转后的位置
        Quaternion rotation = Quaternion.AngleAxis(degrees, axis);
        Vector3 simulatedPos = rotation * (currentPos - centerPos) + centerPos;

        // 与其他手指位置比较
        for (int i = 0; i < fingers.Count; i++)
        {
            GameObject other = fingers[i];
            if (other == finger) continue;

            float dist = Vector3.Distance(simulatedPos, other.transform.position);
            if (dist < minFingerDistance)
            {
                Debug.LogWarning($"❌ {finger.name} 旋转会与 {other.name} 太近（{dist:F3} < {minFingerDistance}），取消旋转。");
                return;
            }
        }

        // 允许旋转
        ArticulationBody ab = finger.GetComponent<ArticulationBody>();
        bool wasEnabled = ab != null && ab.enabled;
        if (ab != null) ab.enabled = false;

        finger.transform.RotateAround(centerPos, axis, degrees);

        if (ab != null) ab.enabled = wasEnabled;

        Debug.Log($"[✅] {finger.name} 成功绕 {center.name} {(degrees > 0 ? "顺" : "逆")}时针旋转 {Mathf.Abs(degrees)} 度");
    }
    public void RotateFingerExternally(int index, float angle)
    {
        if (index >= 0 && index < fingers.Count && index < rotationCenters.Count)
        {
            RotateFingerAroundCenter(fingers[index], rotationCenters[index], angle);
        }
    }

}
