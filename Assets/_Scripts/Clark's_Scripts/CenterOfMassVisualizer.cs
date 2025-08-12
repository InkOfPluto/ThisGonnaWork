using UnityEngine;

[ExecuteAlways] // 即使不运行游戏，也能在 Scene 视图更新
public class CenterOfMassVisualizer : MonoBehaviour
{
    [Header("操作说明 | Instructions")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
        "功能：在 Scene 视图和运行时绘制 Rigidbody 的质心位置（球形 Gizmo）\n" +
        "Inspector：size 控制球的半径\n" +
        "Inspector：gizmoColor 控制 Gizmo 的颜色\n" +
        "要求：对象必须有 Rigidbody 组件，否则不显示\n" +
        "运行/编辑模式：都会实时显示质心位置\n";

    [Header("Gizmo Settings | Gizmo 参数")]
    public float size = 0.01f;                 // 球的半径
    public Color gizmoColor = Color.yellow;    // 球的颜色

    private Rigidbody rb;

    void OnDrawGizmos()
    {
        // 获取 Rigidbody（编辑器模式也可以）
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // 画球
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(rb.worldCenterOfMass, size);
        }
    }
}
