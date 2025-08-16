using UnityEngine;
using System;

public class CylinderTableContact : MonoBehaviour
{
    // 静态事件：别的脚本可以用 CylinderTableContact.OnCylinderTouchTable += 方法 来订阅
    public static event Action OnCylinderTouchTable;

    // 新增：离开事件
    public static event Action OnCylinderLeaveTable;

    [SerializeField] private string tableTag = "Table"; // Table 的 Tag

    private void OnCollisionEnter(Collision collision)
    {
        // 检查是否碰到 Table
        if (collision.gameObject.CompareTag(tableTag))
        {
            Debug.Log("Cylinder 与 Table 接触");
            OnCylinderTouchTable?.Invoke(); // 触发接触事件
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        // 检查是否是离开 Table
        if (collision.gameObject.CompareTag(tableTag))
        {
            Debug.Log("Cylinder 离开 Table");
            OnCylinderLeaveTable?.Invoke(); // 触发离开事件
        }
    }
}
