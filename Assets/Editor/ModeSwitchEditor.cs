using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModeSwitch))]
public class ModeSwitchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ��ȡĿ��ű�
        ModeSwitch script = (ModeSwitch)target;

        // ���� Instructions��Ĭ����Ⱦ��
        DrawDefaultInspectorExcept("currentMode", "participantID");

        EditorGUILayout.Space();

        // �ҵ� currentMode���κ�ʱ��ֻ����
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.EnumPopup(new GUIContent("Current Mode (ֻ��)"), script.currentMode);
        EditorGUI.EndDisabledGroup();

        // ����ģʽ�ҵ� participantID
        EditorGUI.BeginDisabledGroup(Application.isPlaying);
        script.participantID = EditorGUILayout.IntSlider(new GUIContent("Participant ID"), script.participantID, 1, 30);
        EditorGUI.EndDisabledGroup();

        // �����޸ģ���ֹ�༭������¼�䶯��
        if (GUI.changed)
        {
            EditorUtility.SetDirty(script);
        }
    }

    /// <summary>
    /// ���Ƴ�ָ���ֶ���� Inspector Ĭ������
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
