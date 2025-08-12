using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModeSwitch))]
public class ModeSwitchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 获取目标脚本
        ModeSwitch script = (ModeSwitch)target;

        // 绘制 Instructions（默认渲染）
        DrawDefaultInspectorExcept("currentMode", "participantID");

        EditorGUILayout.Space();

        // 灰掉 currentMode（任何时候只读）
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.EnumPopup(new GUIContent("Current Mode (只读)"), script.currentMode);
        EditorGUI.EndDisabledGroup();

        // 播放模式灰掉 participantID
        EditorGUI.BeginDisabledGroup(Application.isPlaying);
        script.participantID = EditorGUILayout.IntSlider(new GUIContent("Participant ID"), script.participantID, 1, 30);
        EditorGUI.EndDisabledGroup();

        // 保存修改（防止编辑器不记录变动）
        if (GUI.changed)
        {
            EditorUtility.SetDirty(script);
        }
    }

    /// <summary>
    /// 绘制除指定字段外的 Inspector 默认内容
    /// </summary>
    private void DrawDefaultInspectorExcept(params string[] exclude)
    {
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            bool skip = false;
            foreach (string e in exclude)
            {
                if (prop.name == e) { skip = true; break; }
            }
            if (!skip)
                EditorGUILayout.PropertyField(prop, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
