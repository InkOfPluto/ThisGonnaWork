using UnityEngine;

public class CenterOfMassVisualizer : MonoBehaviour
{
    public GameObject markerPrefab; // ���뷢��С��Prefab
    public float Size;
    private GameObject markerInstance;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // ʵ���������
        if (markerPrefab != null)
        {
            markerInstance = Instantiate(markerPrefab);
            markerInstance.transform.localScale = Vector3.one * Size; // �ɵ��ڴ�С
        }
    }

    void Update()
    {
        if (markerInstance != null && rb != null)
        {
            // ������λ�ø���Ϊ���ĵ���������
            markerInstance.transform.position = rb.worldCenterOfMass;
        }
    }
}
