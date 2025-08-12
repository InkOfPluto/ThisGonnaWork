using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

// ---------- �Զ��� ReadOnly ���� ----------
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    // Ϊÿ�����Ա������λ�ã��� propertyPath ���֣�
    private static readonly Dictionary<string, Vector2> ScrollPos = new Dictionary<string, Vector2>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool isString = property.propertyType == SerializedPropertyType.String;
        var textAreaAttr = fieldInfo != null
            ? (TextAreaAttribute)System.Attribute.GetCustomAttribute(fieldInfo, typeof(TextAreaAttribute))
            : null;

        if (isString && textAreaAttr != null)
        {
            // 1) �۵�/չ����
            var foldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                // �۵���Ԥ��һ��
                var preview = property.stringValue ?? "";
                if (preview.Length > 120) preview = preview.Substring(0, 117) + "...";
                using (new EditorGUI.DisabledScope(true))
                {
                    var previewRect = new Rect(
                        foldRect.x + EditorGUIUtility.labelWidth,
                        foldRect.y,
                        foldRect.width - EditorGUIUtility.labelWidth,
                        foldRect.height
                    );
                    EditorGUI.LabelField(previewRect, preview, EditorStyles.miniLabel);
                }
                return;
            }

            // 2) չ������������ + ֻ�� TextArea
            float textHeight = GetTextAreaHeight(textAreaAttr);
            var outerRect = new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                textHeight
            );

            // ȥ��ǰ׺��ǩ������ݾ���
            var contentRect = EditorGUI.PrefixLabel(outerRect, GUIContent.none);

            // ��ȡ�����ԵĹ���λ��
            string key = property.propertyPath;
            if (!ScrollPos.ContainsKey(key)) ScrollPos[key] = Vector2.zero;

            // ����������ʵ�߶ȣ����ھ�����ʱ���ֹ�����
            string text = property.stringValue ?? "";
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            float innerContentHeight = style.CalcHeight(new GUIContent(text), contentRect.width - 16f); // Ԥ�����������

            // viewRect ���� contentRect �Ż���ʾ������
            var viewRect = new Rect(0, 0, contentRect.width - 16f, innerContentHeight);

            // ��ʼ������ͼ����һ������ʾ�Ҳ��������
            ScrollPos[key] = GUI.BeginScrollView(contentRect, ScrollPos[key], viewRect, false, true);

            // �ڹ�����ͼ�ڲ����Ʋ��ɱ༭�� TextArea
            var taRect = new Rect(0, 0, viewRect.width, innerContentHeight);
            EditorGUI.BeginChangeCheck();
            string newVal = EditorGUI.TextArea(taRect, text, style); // ����״̬ �� �ɹ�������ѡ��
            if (EditorGUI.EndChangeCheck())
            {
                // �����û��޸ģ�����ֻ��
                property.stringValue = text;
            }
            else
            {
                property.stringValue = text;
            }

            GUI.EndScrollView();
        }
        else
        {
            // �������ͣ����û���
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        bool isString = property.propertyType == SerializedPropertyType.String;
        var textAreaAttr = fieldInfo != null
            ? (TextAreaAttribute)System.Attribute.GetCustomAttribute(fieldInfo, typeof(TextAreaAttribute))
            : null;

        if (isString && textAreaAttr != null)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float textHeight = GetTextAreaHeight(textAreaAttr);
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + textHeight;
        }

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    private float GetTextAreaHeight(TextAreaAttribute a)
    {
        int lines = Mathf.Clamp(a.maxLines, a.minLines, 200);
        // �õ��и߶Ƚ���һ�� TextArea �ĸ߶�
        return EditorGUIUtility.singleLineHeight * lines;
    }
}
#endif
