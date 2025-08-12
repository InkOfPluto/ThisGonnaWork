using UnityEngine;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class BackgroundAutoFit : MonoBehaviour
{
    [Header("Target Text | Ҫ������ı�")]
    public TextMeshProUGUI targetText;

    [Header("Extra Padding | ����߾� (x=ˮƽ, y=��ֱ)")]
    public Vector2 extraPadding = new Vector2(10f, 10f);

    private RectTransform backgroundRect;

    void Awake()
    {
        backgroundRect = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        if (targetText == null) return;

        // ��ȡ�ı���ʵ����Ⱦ�ߴ�
        Vector2 textSize = targetText.GetPreferredValues();

        // ���ñ�����С = �ı���С + ����߾�
        backgroundRect.sizeDelta = new Vector2(
            textSize.x + extraPadding.x,
            textSize.y + extraPadding.y
        );

        // ����λ�ö����ı�
        backgroundRect.position = targetText.rectTransform.position;
        backgroundRect.pivot = targetText.rectTransform.pivot;
    }
}
