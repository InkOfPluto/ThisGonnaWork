using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

public class UnusedScriptDetector : EditorWindow
{
    [MenuItem("Tools/Detect Unused Scripts in Clark's_Scripts")]
    public static void DetectUnusedScripts()
    {
        string targetFolder = "Assets/_Scripts/Clark's_Scripts";

        string[] csFiles = Directory.GetFiles(targetFolder, "*.cs", SearchOption.AllDirectories);

        // 获取当前场景中所有挂载的 MonoBehaviour 脚本
        MonoBehaviour[] allBehaviours = GameObject.FindObjectsOfType<MonoBehaviour>(true);
        HashSet<Type> usedTypes = new HashSet<Type>(allBehaviours.Select(b => b?.GetType()));

        List<string> unusedScripts = new List<string>();

        foreach (string csFile in csFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(csFile);
            // 尝试从程序集查找这个类
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == fileName && typeof(MonoBehaviour).IsAssignableFrom(t));

            if (type == null)
            {
                // 不是 MonoBehaviour 或者类名和文件名不匹配
                continue;
            }

            if (!usedTypes.Contains(type))
            {
                unusedScripts.Add(fileName);
            }
        }

        if (unusedScripts.Count == 0)
        {
            Debug.Log("✅ 所有脚本都已被使用！");
        }
        else
        {
            Debug.LogWarning($"⚠️ 共 {unusedScripts.Count} 个脚本未被任何组件挂载：");
            foreach (var script in unusedScripts)
            {
                Debug.Log($"🗃️ 未使用脚本: {script}");
            }
        }
    }
}
