using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

// ---------- 自定义 ReadOnly 属性 ----------
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    // 为每个属性保存滚动位置（按 propertyPath 区分）
    private static readonly Dictionary<string, Vector2> ScrollPos = new Dictionary<string, Vector2>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool isString = property.propertyType == SerializedPropertyType.String;
        var textAreaAttr = fieldInfo != null
            ? (TextAreaAttribute)System.Attribute.GetCustomAttribute(fieldInfo, typeof(TextAreaAttribute))
            : null;

        if (isString && textAreaAttr != null)
        {
            // 1) 折叠/展开行
            var foldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                // 折叠：预览一行
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

            // 2) 展开：滚动区域 + 只读 TextArea
            float textHeight = GetTextAreaHeight(textAreaAttr);
            var outerRect = new Rect(
                position.x,
                position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                textHeight
            );

            // 去掉前缀标签后的内容矩形
            var contentRect = EditorGUI.PrefixLabel(outerRect, GUIContent.none);

            // 获取本属性的滚动位置
            string key = property.propertyPath;
            if (!ScrollPos.ContainsKey(key)) ScrollPos[key] = Vector2.zero;

            // 计算内容真实高度，用于决定何时出现滚动条
            string text = property.stringValue ?? "";
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            float innerContentHeight = style.CalcHeight(new GUIContent(text), contentRect.width - 16f); // 预留滚动条宽度

            // viewRect 高于 contentRect 才会显示滚动条
            var viewRect = new Rect(0, 0, contentRect.width - 16f, innerContentHeight);

            // 开始滚动视图（这一步会显示右侧滚动条）
            ScrollPos[key] = GUI.BeginScrollView(contentRect, ScrollPos[key], viewRect, false, true);

            // 在滚动视图内部绘制不可编辑的 TextArea
            var taRect = new Rect(0, 0, viewRect.width, innerContentHeight);
            EditorGUI.BeginChangeCheck();
            string newVal = EditorGUI.TextArea(taRect, text, style); // 启用状态 → 可滚动、可选择
            if (EditorGUI.EndChangeCheck())
            {
                // 丢弃用户修改，保持只读
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
            // 其它类型：禁用绘制
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
        // 用单行高度近似一行 TextArea 的高度
        return EditorGUIUtility.singleLineHeight * lines;
    }
}
#endif
