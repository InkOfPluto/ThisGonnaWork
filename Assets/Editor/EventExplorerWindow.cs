// Assets/Editor/EventExplorerWindow.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class EventExplorerWindow : EditorWindow
{
    private Vector2 _scroll;
    private bool _scanCSharpEvents = true;
    private bool _scanUnityEvents = true;
    private bool _scanAnimationEvents = true;
    private bool _includeInactive = true;

    // ✅ 新增：隐藏 XR 相关事件的开关
    private bool _hideXREvents = false;

    private string _csvPath = "EventReport.csv";

    // 我们保留“全部结果”和“显示结果”两份，便于即时过滤
    private readonly List<Row> _allRows = new();
    private readonly List<Row> _rows = new();

    // XR 前缀/关键词（可按需扩展）
    private static readonly string[] XR_PREFIXES = new[]
    {
        "UnityEngine.XR",
        "Unity.XR",
        "UnityEngine.InputSystem.XR",
        "UnityEditor.XR",
        "UnityEngine.XR.Interaction.Toolkit",
        "Unity.XRInteractionToolkit"
    };

    private static readonly string[] XR_KEYWORDS = new[]
    {
        "XRInteractionToolkit",
        ".XR.",           // 类型/命名空间中包含 XR 分段
        "XRRayInteractor",
        "XRDirectInteractor",
        "XRRig",
        "XR Origin"
    };

    [MenuItem("Tools/Event Explorer")]
    public static void ShowWindow()
    {
        GetWindow<EventExplorerWindow>("Event Explorer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Event Explorer（项目事件总览）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "建议在 Play 模式点 Refresh，这样可以抓到运行时订阅的 C# 事件（+=）。\n" +
            "UnityEvent 的持久化监听无需 Play 也能读取；Animation Events 会从 Assets 的所有 AnimationClip 读取。",
            MessageType.Info);

        EditorGUILayout.Space();
        _scanCSharpEvents = EditorGUILayout.ToggleLeft("扫描 C# 事件（event/Action/Func）", _scanCSharpEvents);
        _scanUnityEvents = EditorGUILayout.ToggleLeft("扫描 UnityEvent（Inspector 配置）", _scanUnityEvents);
        _scanAnimationEvents = EditorGUILayout.ToggleLeft("扫描 Animation Events（AnimationClip）", _scanAnimationEvents);
        _includeInactive = EditorGUILayout.ToggleLeft("包含未激活对象（场景）", _includeInactive);

        // ✅ 新增：XR 过滤开关（即时生效）
        bool newHideXr = EditorGUILayout.ToggleLeft("隐藏 XR 相关事件（按命名空间/类型/常见关键词）", _hideXREvents);
        if (newHideXr != _hideXREvents)
        {
            _hideXREvents = newHideXr;
            ApplyFilter(); // 无需重新扫描
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh 扫描", GUILayout.Height(30)))
        {
            ScanAll();
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        _csvPath = EditorGUILayout.TextField("导出 CSV 路径", _csvPath);
        if (GUILayout.Button("Export CSV", GUILayout.Width(120)))
        {
            ExportCsv(_csvPath); // 导出当前“显示结果”（会受 XR 过滤影响）
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"结果 {_rows.Count} 条：", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var r in _rows)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"类别: {r.Category}");
            EditorGUILayout.LabelField($"对象: {r.ObjectPath}");
            EditorGUILayout.LabelField($"组件: {r.ComponentType}");
            EditorGUILayout.LabelField($"字段/事件: {r.MemberName}");
            if (!string.IsNullOrEmpty(r.TargetInfo))
                EditorGUILayout.LabelField($"目标/方法: {r.TargetInfo}");
            if (!string.IsNullOrEmpty(r.Details))
                EditorGUILayout.LabelField($"细节: {r.Details}");
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void ScanAll()
    {
        _allRows.Clear();

        // 1) 扫描场景里的对象
        var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go =>
            {
                bool isAsset = EditorUtility.IsPersistent(go);
                if (isAsset) return false; // 只看场景实例（避免 Project 里的 prefab 预览）
                if (!_includeInactive && !go.activeInHierarchy) return false;
                if (go.hideFlags != HideFlags.None && go.hideFlags != HideFlags.HideInHierarchy) return false;
                return true;
            }).ToArray();

        foreach (var go in sceneObjects)
        {
            var comps = go.GetComponents<MonoBehaviour>();
            foreach (var comp in comps)
            {
                if (comp == null) continue;

                var type = comp.GetType();

                if (_scanCSharpEvents)
                    ScanCSharpEventsOnComponent(go, comp, type);

                if (_scanUnityEvents)
                    ScanUnityEventsOnComponent(go, comp, type);
            }
        }

        // 2) 扫描 AnimationClip（全工程）
        if (_scanAnimationEvents)
            ScanAnimationEventsInAssets();

        ApplyFilter(); // 扫描后应用一次过滤
        Repaint();
    }

    private void ApplyFilter()
    {
        _rows.Clear();
        if (!_hideXREvents)
        {
            _rows.AddRange(_allRows);
            return;
        }

        foreach (var r in _allRows)
        {
            if (IsXRRow(r)) continue; // 过滤掉 XR
            _rows.Add(r);
        }
    }

    // —— C# 事件（含 event/Action/Func）——
    private void ScanCSharpEventsOnComponent(GameObject go, MonoBehaviour comp, Type type)
    {
        // 2.1 event 关键字声明的事件
        var events = type.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        foreach (var ev in events)
        {
            // 尝试拿到编译器生成的委托字段（同名私有字段）
            var field = type.GetField(ev.Name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Delegate del = null;
            if (field != null && typeof(Delegate).IsAssignableFrom(field.FieldType))
            {
                del = field.GetValue(comp) as Delegate;
            }

            if (del == null)
            {
                AddRow("C# Event", go, comp, ev.Name, "", "（无订阅或为自定义 add/remove，运行时可能才有）");
                continue;
            }
            foreach (var d in del.GetInvocationList())
            {
                string targetInfo = $"{SafeType(d.Target)}.{d.Method.Name}()";
                AddRow("C# Event", go, comp, ev.Name, targetInfo, "");
            }
        }

        // 2.2 Action/Func/Delegate 字段（非 event）
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            if (!typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
            var del = f.GetValue(comp) as Delegate;
            if (del == null) continue;

            foreach (var d in del.GetInvocationList())
            {
                string targetInfo = $"{SafeType(d.Target)}.{d.Method.Name}()";
                AddRow("C# Delegate(Field)", go, comp, f.Name, targetInfo, "");
            }
        }
    }

    // —— UnityEvent（Inspector 配置）——
    private void ScanUnityEventsOnComponent(GameObject go, MonoBehaviour comp, Type type)
    {
        // 找所有字段中继承 UnityEventBase 的
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            if (!typeof(UnityEventBase).IsAssignableFrom(f.FieldType)) continue;
            var ueb = f.GetValue(comp) as UnityEventBase;
            if (ueb == null) continue;

            int count = ueb.GetPersistentEventCount();
            if (count == 0)
            {
                AddRow("UnityEvent", go, comp, f.Name, "", "（无持久化监听）");
                continue;
            }
            for (int i = 0; i < count; i++)
            {
                var target = ueb.GetPersistentTarget(i);
                var method = ueb.GetPersistentMethodName(i);
                string targetInfo = $"{(target != null ? target.name : "null")} -> {method}";
                AddRow("UnityEvent", go, comp, f.Name, targetInfo, "PersistentCall");
            }
        }
    }

    // —— Animation Events —— 
    private void ScanAnimationEventsInAssets()
    {
        var guids = AssetDatabase.FindAssets("t:AnimationClip");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            foreach (var ev in AnimationUtility.GetAnimationEvents(clip))
            {
                string details = $"time={ev.time:F3}, func={ev.functionName}";
                AddRow("AnimationEvent", null, null, clip.name, "", details + (string.IsNullOrEmpty(path) ? "" : $" | {path}"));
            }
        }
    }

    private string SafeType(object o)
    {
        if (o == null) return "null";
        var t = o.GetType();
        return t.FullName ?? t.Name;
    }

    private void AddRow(string category, GameObject go, MonoBehaviour comp, string memberName, string targetInfo, string details)
    {
        _allRows.Add(new Row
        {
            Category = category,
            ObjectPath = go != null ? GetHierarchyPath(go) : "(Asset)",
            ComponentType = comp != null ? comp.GetType().FullName : "-",
            MemberName = memberName,
            TargetInfo = targetInfo,
            Details = details
        });
    }

    private string GetHierarchyPath(GameObject go)
    {
        if (go == null) return "";
        var stack = new Stack<string>();
        var t = go.transform;
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }

    private void ExportCsv(string path)
    {
        try
        {
            var lines = new List<string>();
            lines.Add("Category,ObjectPath,ComponentType,Member,Target/Method,Details");
            foreach (var r in _rows) // 导出当前显示结果（已过滤）
            {
                string line = string.Join(",",
                    Escape(r.Category),
                    Escape(r.ObjectPath),
                    Escape(r.ComponentType),
                    Escape(r.MemberName),
                    Escape(r.TargetInfo),
                    Escape(r.Details));
                lines.Add(line);
            }

            var fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), path);
            File.WriteAllLines(fullPath, lines);
            EditorUtility.RevealInFinder(fullPath);
            Debug.Log($"✅ 导出完成：{fullPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"导出失败：{ex.Message}");
        }
    }

    private string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\"", "\"\"");
        if (s.Contains(",") || s.Contains("\n")) return $"\"{s}\"";
        return s;
    }

    // ✅ 判断某行是否属于 XR（命名空间/类型/关键词）
    private bool IsXRRow(Row r)
    {
        string hay = $"{r.ComponentType} | {r.TargetInfo} | {r.ObjectPath} | {r.MemberName} | {r.Details}";
        // 大小写不敏感
        string lower = hay.ToLowerInvariant();

        // 前缀匹配
        foreach (var p in XR_PREFIXES)
        {
            if (lower.Contains(p.ToLowerInvariant())) return true;
        }
        // 关键词匹配
        foreach (var k in XR_KEYWORDS)
        {
            if (lower.Contains(k.ToLowerInvariant())) return true;
        }
        return false;
    }

    private class Row
    {
        public string Category;
        public string ObjectPath;
        public string ComponentType;
        public string MemberName;
        public string TargetInfo;
        public string Details;
    }
}
#endif
