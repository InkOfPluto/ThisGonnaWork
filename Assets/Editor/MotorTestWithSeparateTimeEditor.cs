//// Assets/Scripts/Editor/MotorTestWithSeparateTimeEditor.cs
//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;

//[CustomEditor(typeof(MotorTestWithSeparateTime))]
//public class MotorTestWithSeparateTimeEditor : Editor
//{
//    public override void OnInspectorGUI()
//    {
//        // 顶部 HelpBox：操作说明
//        EditorGUILayout.HelpBox(
//            "操作说明 / Instructions\n" +
//            "• 按 - ：全部 Down（负速度）\n" +
//            "• 按 = ：全部 Up（正速度）\n" +
//            "• 按 0 ：发送 s 急停\n" +
//            "• 指令：t±NNN,i±NNN,m±NNN,r±NNN,p±NNN（-255..255）\n" +
//            "• 串口：在 Inspector 中设置 portName（默认 COM11）",
//            MessageType.Info
//        );

//        // 画出原有字段
//        DrawDefaultInspector();
//    }
//}
//#endif
