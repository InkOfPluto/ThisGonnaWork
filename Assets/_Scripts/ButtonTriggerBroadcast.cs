using UnityEngine;

public class ButtonTriggerBroadcast : MonoBehaviour
{
    public MeshRenderer buttonMesh;
    public ResetCylinderPosition[] cylindersToReset;

    private int triggerCount = 0; // 👈 新增计数器

    Color origColor = Color.red;
    Color holdColor = new Color(0.2f, 0.2f, 0, 0.5f);

    private void Start()
    {
        buttonMesh.material.color = origColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("button"))
        {
            triggerCount++;
            buttonMesh.material.color = holdColor;

            if (cylindersToReset != null && cylindersToReset.Length > 0)
            {
                foreach (var cylinder in cylindersToReset)
                {
                    if (cylinder != null)
                        cylinder.ResetPosition();
                }
                Debug.Log("[✅] 调用了所有 ResetPosition()");
            }
            else
            {
                Debug.LogWarning("[❌] cylindersToReset 没有绑定！");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("button"))
        {
            triggerCount = Mathf.Max(0, triggerCount - 1); // 👈 防止负数

            if (triggerCount == 0)
            {
                buttonMesh.material.color = origColor;
            }
        }
    }
}
