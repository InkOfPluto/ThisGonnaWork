//using System.Collections.Generic;
//using UnityEngine;

//public class HandtrackingRotateFinger : MonoBehaviour
//{
//    [Header("中心点设置 | Center Points")]
//    public Transform CubeCenter;
//    public Transform handCenter;

//    [Header("手指指尖位置 | Handtracking Fingertips")]
//    public Transform rThumbTip;
//    public Transform rIndexTip;
//    public Transform rMiddleTip;
//    public Transform rRingTip;
//    public Transform rLittleTip;

//    [Header("对应的立方体 | Finger Cubes")]
//    public Transform fingerDA;
//    public Transform fingerSHI;
//    public Transform fingerZHONG;
//    public Transform fingerWU;
//    public Transform fingerXIAO;

//    [Header("设置 | Settings")]
//    public float minFingerDistance = 0.02f;  // 立方体最小允许距离

//    // 所有 Cube 的引用集合
//    private List<Transform> allCubes;
//    private List<Transform> allTips;

//    void Start()
//    {
//        allCubes = new List<Transform> { fingerDA, fingerSHI, fingerZHONG, fingerWU, fingerXIAO };
//        allTips = new List<Transform> { rThumbTip, rIndexTip, rMiddleTip, rRingTip, rLittleTip };
//    }

//    void Update()
//    {
//        for (int i = 0; i < allCubes.Count; i++)
//        {
//            RotateAroundHandPreventOverlap(i);
//        }
//    }

//    void RotateAroundHandPreventOverlap(int index)
//    {
//        if (index >= allCubes.Count || index >= allTips.Count) return;

//        Transform cube = allCubes[index];
//        Transform tip = allTips[index];

//        if (cube == null || tip == null || CubeCenter == null || handCenter == null)
//            return;

//        Vector3 handPos = CubeCenter.position;

//        // 当前偏移量（xz）
//        Vector3 currentOffset = cube.position - handPos;
//        Vector3 currentDirXZ = new Vector3(currentOffset.x, 0, currentOffset.z);
//        float radius = currentDirXZ.magnitude;

//        if (radius < 0.0001f) return;

//        // 目标方向（handCenter → fingertip）
//        Vector3 desiredDir = tip.position - handCenter.position;
//        Vector3 desiredDirXZ = new Vector3(desiredDir.x, 0, desiredDir.z);

//        if (desiredDirXZ.magnitude < minFingerDistance) return;
//        desiredDirXZ.Normalize();

//        // 计算模拟位置（绕中心旋转，并固定在水平面）
//        Vector3 simulatedPos = new Vector3(
//            handPos.x + desiredDirXZ.x * radius,
//            handPos.y,
//            handPos.z + desiredDirXZ.z * radius
//        );

//        // 检查是否与其他 Cube 太近
//        for (int i = 0; i < allCubes.Count; i++)
//        {
//            if (i == index) continue;
//            if (allCubes[i] == null) continue;

//            float dist = Vector3.Distance(simulatedPos, allCubes[i].position);
//            if (dist < minFingerDistance)
//            {
//                Vector3 dir = (simulatedPos - allCubes[i].position).normalized;

//                // 将 simulatedPos 推回到 minFingerDistance 的边界
//                simulatedPos = allCubes[i].position + dir * minFingerDistance;

//                Debug.LogWarning($"⚠ {cube.name} 移动过近，已自动调整至安全距离边缘 ({minFingerDistance:F3} m) 与 {allCubes[i].name} 分离。");

//                break; // 跳出循环，只处理最近一次碰撞
//            }
//        }

//        // ✅ 允许移动
//        cube.position = simulatedPos;
//        cube.rotation = Quaternion.LookRotation(desiredDirXZ, Vector3.up);

//        Debug.Log($"✅ {cube.name} 成功绕中心移动，方向对齐 {tip.name}");
//    }
//}
