using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PincherFingerController : MonoBehaviour
{
    public float closedZ;
    Vector3 openPosition;
    ArticulationBody articulation;

    private bool hasInitialized = false; // ✅ 防止多次初始化

    // INIT
    void Start()
    {
        articulation = GetComponent<ArticulationBody>();
        // 不在这里设置 openPosition，让 Button 控制设置
    }

    void SetLimits()
    {
        float openZTarget = ZDriveTarget(0.0f);
        float closedZTarget = ZDriveTarget(1.0f);
        float min = Mathf.Min(openZTarget, closedZTarget);
        float max = Mathf.Max(openZTarget, closedZTarget);

        var drive = articulation.zDrive;
        drive.lowerLimit = min;
        drive.upperLimit = max;
        articulation.zDrive = drive;
    }

    // ✅ 新增：仅在首次调用时初始化 openPosition
    public void InitializeOpenPositionFromCurrent()
    {
        if (hasInitialized) return;
        openPosition = transform.localPosition;
        SetLimits(); // 重新设置上下限
        hasInitialized = true;
        Debug.Log($"{name} openPosition initialized to {openPosition}");
    }

    // READ
    public float CurrentGrip()
    {
        float grip = Mathf.InverseLerp(openPosition.z, closedZ, transform.localPosition.z);
        return grip;
    }

    public Vector3 GetOpenPosition()
    {
        return openPosition;
    }

    // CONTROL
    public void UpdateGrip(float grip)
    {
        float targetZ = ZDriveTarget(grip);
        var drive = articulation.zDrive;
        drive.target = targetZ;
        articulation.zDrive = drive;
    }

    public void ForceOpen(Transform transform)
    {
        transform.localPosition = openPosition;
        UpdateGrip(0.0f);
    }

    // HELPERS
    float ZDriveTarget(float grip)
    {
        float zPosition = Mathf.Lerp(openPosition.z, closedZ, grip);
        float targetZ = (zPosition - openPosition.z) * transform.parent.localScale.z;
        return targetZ;
    }
}
