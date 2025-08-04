using UnityEngine;

public class CenterOfMassController : MonoBehaviour
{
    [Header("指定目标物体（必须带 Rigidbody）")]
    public GameObject targetObject;

    private Rigidbody rb;

    private Vector3[] centerOfMassList = new Vector3[]
    {
        new Vector3( 0.000f,  0.000f,  0.000f), // 1
        new Vector3( 0.081f,  0.047f, -0.039f), // 2
        new Vector3(-0.093f, -0.019f,  0.088f), // 3
        new Vector3( 0.014f,  0.097f, -0.074f), // 4
        new Vector3(-0.078f,  0.065f,  0.022f), // 5
        new Vector3( 0.058f, -0.091f, -0.067f), // 6
        new Vector3(-0.006f,  0.030f,  0.096f), // 7
        new Vector3( 0.087f, -0.058f,  0.079f), // 8
        new Vector3(-0.091f, -0.031f, -0.092f), // 9
        new Vector3( 0.025f,  0.098f, -0.005f)  // 0
    };

    void Start()
    {
        if (targetObject == null)
        {
            Debug.LogError("请在 Inspector 中指定目标物体！");
            return;
        }

        rb = targetObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("指定的目标物体上没有 Rigidbody 组件！");
        }
    }

    void Update()
    {
        if (rb == null) return;

        for (int i = 0; i < 10; i++)
        {
            KeyCode key = (i < 9) ? KeyCode.Alpha1 + i : KeyCode.Alpha0;
            if (Input.GetKeyDown(key))
            {
                rb.centerOfMass = centerOfMassList[i];
                Debug.Log($"按下 {(i + 1) % 10} 键，设置 Center of Mass 为 {centerOfMassList[i]}");
            }
        }
    }
}
