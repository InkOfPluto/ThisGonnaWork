using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraspController : MonoBehaviour
{
    public PincherController pinchController;  // 引用 Pincher 控制器
    public Transform[] fingerTips;            // 手指指尖对象数组
    public Transform thumbtip;                // 拇指指尖对象
    public float OpenDistance;
    public float CloseDistance;

    void Start()
    {
        // 自动查找场景中的 PincherController 脚本
        pinchController = FindAnyObjectByType<PincherController>();
    }

    void Update()
    {
        float fingertipDist = 0f;

        // 计算所有手指指尖到拇指的平均距离
        foreach (Transform t in fingerTips)
        {
            fingertipDist += Vector3.Distance(thumbtip.position, t.position);
        }
        float avfingertipDist = fingertipDist / fingerTips.Length;

        // Debug.Log("当前指尖平均距离: " + avfingertipDist.ToString("F3"));

        // 根据距离设置抓握状态
        if (avfingertipDist >= OpenDistance)
        {
            //Debug.Log("你张开收手了");
            // 如果完全张开：手指打开
            pinchController.gripState = GripState.Opening;
            //Debug.Log("夹子打开");
        }
        else if (avfingertipDist <= CloseDistance)
        {
            //Debug.Log("你手指合拢了");
            // 如果完全闭合：手指持续合拢
            pinchController.gripState = GripState.Closing;
            //Debug.Log("夹子关闭");
        }
        else
        {
         
            // 其他情况：保持手指位置不变
            pinchController.gripState = GripState.Fixed;
        }
    }
}
