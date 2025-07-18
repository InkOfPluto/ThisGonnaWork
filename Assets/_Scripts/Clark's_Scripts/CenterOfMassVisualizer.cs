using UnityEngine;

public class CenterOfMassVisualizer : MonoBehaviour
{
    public GameObject markerPrefab; // 拖入发光小球Prefab
    public float Size;
    private GameObject markerInstance;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 实例化标记球
        if (markerPrefab != null)
        {
            markerInstance = Instantiate(markerPrefab);
            markerInstance.transform.localScale = Vector3.one * Size; // 可调节大小
        }
    }

    void Update()
    {
        if (markerInstance != null && rb != null)
        {
            // 将球体位置更新为质心的世界坐标
            markerInstance.transform.position = rb.worldCenterOfMass;
        }
    }
}
