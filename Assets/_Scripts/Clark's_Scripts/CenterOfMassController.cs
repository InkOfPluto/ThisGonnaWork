using UnityEngine;

[ExecuteAlways]
public class CenterOfMassController : MonoBehaviour
{
    [Header("目标物体（必须带 Rigidbody + MeshRenderer）")]
    public GameObject targetObject;

    [Header("选择要应用的重心编号（0 ~ 14）")]
    [Range(0, 14)]
    public int selectedCOMIndex = 0;

    [Header("重心坐标列表（默认15个，可在 Inspector 修改）")]
    public Vector3[] centerOfMassList = new Vector3[]
    {
        new Vector3( 0.000f,  0.000f,  0.000f),
        new Vector3( 0.081f,  0.047f, -0.039f),
        new Vector3(-0.093f, -0.019f,  0.088f),
        new Vector3( 0.014f,  0.097f, -0.074f),
        new Vector3(-0.078f,  0.065f,  0.022f),
        new Vector3( 0.058f, -0.091f, -0.067f),
        new Vector3(-0.006f,  0.030f,  0.096f),
        new Vector3( 0.087f, -0.058f,  0.079f),
        new Vector3(-0.091f, -0.031f, -0.092f),
        new Vector3( 0.025f,  0.098f, -0.005f),
        new Vector3( 0.050f,  0.010f, -0.030f),
        new Vector3(-0.070f,  0.080f,  0.040f),
        new Vector3( 0.020f, -0.040f,  0.060f),
        new Vector3(-0.030f,  0.020f, -0.070f),
        new Vector3( 0.060f, -0.020f,  0.090f)
    };

    private Rigidbody rb;
    private MeshRenderer meshRenderer;
    private int lastAppliedIndex = -1;

    public readonly Color[] colorList = new Color[15]
    {
        HexToColor("#00FFFF"), // 0
        HexToColor("#4169E1"), // 1
        HexToColor("#228B22"), // 2
        HexToColor("#FFD700"), // 3
        HexToColor("#FF8C00"), // 4
        HexToColor("#8B00FF"), // 5
        HexToColor("#FF1493"), // 6
        HexToColor("#DC143C"), // 7
        HexToColor("#D2691E"), // 8
        HexToColor("#6A5ACD"), // 9
        HexToColor("#6B8E23"), // 10
        HexToColor("#000000"), // 11
        HexToColor("#FF6347"), // 12
        HexToColor("#4B0082"), // 13
        HexToColor("#FA8072")  // 14
    };

    private bool _isPressed = false;

    void Update()
    {
        TryGetComponents();

        // 🎮 检测 Xbox 手柄 B 键（joystick button 1）
        if (!_isPressed && Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            _isPressed = true;
            CycleToNextCOM(); // 切换到下一个重心
        }

        if (_isPressed && Input.GetKeyUp(KeyCode.JoystickButton1))
        {
            _isPressed = false;
        }

        ApplyCenterOfMassAndColor();
    }

    void TryGetComponents()
    {
        if (targetObject == null) return;

        if (rb == null)
            rb = targetObject.GetComponent<Rigidbody>();

        if (meshRenderer == null)
            meshRenderer = targetObject.GetComponent<MeshRenderer>();
    }

    void ApplyCenterOfMassAndColor()
    {
        if (targetObject == null || rb == null || meshRenderer == null) return;

        if (selectedCOMIndex != lastAppliedIndex && selectedCOMIndex >= 0 && selectedCOMIndex < 15)
        {
            // 设置重心
            rb.centerOfMass = centerOfMassList[selectedCOMIndex];

            // 设置颜色（透明度固定为120/255）
            Color baseColor = colorList[selectedCOMIndex];
            baseColor.a = 120f / 255f;

            Material mat = meshRenderer.sharedMaterial;
            if (mat != null)
            {
                mat.color = baseColor;
            }

            lastAppliedIndex = selectedCOMIndex;
        }
    }

    void CycleToNextCOM()
    {
        selectedCOMIndex = (selectedCOMIndex + 1) % centerOfMassList.Length;
        Debug.Log($"🎮 Xbox B 键按下 → 切换到 COM_{selectedCOMIndex}");
    }

    static Color HexToColor(string hex)
    {
        Color color;
        if (ColorUtility.TryParseHtmlString(hex, out color))
        {
            return color;
        }
        Debug.LogError("颜色解析失败：" + hex);
        return Color.white;
    }
}
