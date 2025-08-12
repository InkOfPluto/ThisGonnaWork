using UnityEngine;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class BackgroundAutoFit : MonoBehaviour
{
    [Header("Target Text | 要跟随的文本")]
    public TextMeshProUGUI targetText;

    [Header("Extra Padding | 额外边距 (x=水平, y=垂直)")]
    public Vector2 extraPadding = new Vector2(10f, 10f);

    private RectTransform backgroundRect;

    void Awake()
    {
        backgroundRect = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        if (targetText == null) return;

        // 获取文本的实际渲染尺寸
        Vector2 textSize = targetText.GetPreferredValues();

        // 设置背景大小 = 文本大小 + 额外边距
        backgroundRect.sizeDelta = new Vector2(
            textSize.x + extraPadding.x,
            textSize.y + extraPadding.y
        );

        // 背景位置对齐文本
        backgroundRect.position = targetText.rectTransform.position;
        backgroundRect.pivot = targetText.rectTransform.pivot;
    }
}
