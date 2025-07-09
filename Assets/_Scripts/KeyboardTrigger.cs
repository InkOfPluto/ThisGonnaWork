using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardTrigger : MonoBehaviour
{
    public ArticulationPrismDriverGripper gripper;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (gripper != null)
            {
                gripper.StartTrialFunction(0f); // ✅ 传入一个 float 参数，模拟按钮值
            }
            else
            {
                Debug.LogWarning("Gripper 没有被分配！");
            }
        }
    }
}
