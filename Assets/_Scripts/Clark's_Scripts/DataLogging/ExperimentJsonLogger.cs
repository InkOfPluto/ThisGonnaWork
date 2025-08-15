using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using System.Reflection;

// —— 1) 通用：一条记录一行的 JSONL 写入器 ——
public sealed class JsonLinesWriter : IDisposable
{
    private readonly StreamWriter _sw;
    private readonly JsonSerializer _serializer;
    private bool _disposed = false;

    public JsonLinesWriter(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        _sw = new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
        _serializer = new JsonSerializer
        {
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };
    }

    public void WriteRecord(object obj)
    {
        if (_disposed) return;
        
        try
        {
            using (var jw = new JsonTextWriter(_sw) { CloseOutput = false })
            {
                _serializer.Serialize(jw, obj);
            }
            _sw.Write('\n');
            _sw.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to write JSON record: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _sw?.Flush();
            _sw?.Dispose();
            _disposed = true;
        }
    }
}

// —— 2) 数据模型 ——
[Serializable] 
public struct FlowRecord
{
    public int participant_id;
    public string mode;
    public float mode_enter_time;
    public string timestamp_iso;
}

[Serializable] 
public struct ComStateRecord
{
    public int com_index;
    public float com_enter_time;
    public string com_enter_timestamp;
    public int rotate_button_press_count;
    public Vector3 com_relative_pos;
    public int rotate_enter_times_count;
    public int rotate_mode_entry_count;
    public float current_rotate_duration;
}

[Serializable] 
public struct PostureRecord
{
    public string evt;
    public int com_index;
    public float time;
    public Vector3[] handtips_world;
    public Vector3[] handtips_local;
    public Vector3[] cubes_world;
    public Vector3[] cubes_localYCenter;
}

[Serializable] 
public struct GraspAttemptRecord
{
    public int com_index;
    public int grasp_attempt_count;
    public float[] grasp_attempt_time;
    public Vector3[] handtips_world;
    public Vector3[] handtips_local;
    public Vector3[] cubes_world;
    public Vector3[] cubes_localYCenter;
}

[Serializable] 
public struct FrameRecord
{
    public long timestamp_ms;
    public float cylinder_hover_time;
    public bool cylinder_off_table;
    public bool cylinder_off_threshold;
    public bool cylinder_off_cubetouching;
    public float cube_center_distance;
    public Vector3[] handtips_world;
    public Vector3[] handtips_local;
    public Vector3[] cubes_world;
    public Vector3[] cubes_localYCenter;
    public Vector3 cylinder_world_pos;
    public Vector3 cylinder_euler;
    public float[] slipping_distance5;
    public float[] motor_speeds5;
    public float slip_process_time;
}

// —— 3) 采集器主体 —— 
public class ExperimentJsonLogger : MonoBehaviour
{
    [Header("Participant & Session")]
    public int participantId = 1;
    public string saveRoot = "Logs";
    
    [Header("Frame Recording Settings")]
    [Tooltip("每几帧记录一次（1=每帧，10=每10帧一次）")]
    public int frameRecordInterval = 1;
    private int frameCounter = 0;

    [Header("References")]
    public MonoBehaviour ModeSwitch;
    public MonoBehaviour CenterOfMassController;
    public MonoBehaviour VisualDisplay;
    public MonoBehaviour ButtonForRotateFingers;
    public MonoBehaviour Grasp_HandTracking;
    public MonoBehaviour Slipping;

    // Writers
    private JsonLinesWriter wFlow, wCom, wPosture, wGrasp, wFrame;

    // 状态缓存
    private string lastMode = "";
    private int lastCom = -1;
    private bool inRotate = false;
    private float rotateEnterTime = 0f;
    private int rotateEntryCountForCurrentCom = 0;
    private Dictionary<int, int> rotateEntryCountPerCom = new Dictionary<int, int>();

    // 常量
    const int FINGERS = 5;
    
    // 性能优化：缓存反射结果
    private Dictionary<string, FieldInfo> fieldCache = new Dictionary<string, FieldInfo>();
    private Dictionary<string, PropertyInfo> propertyCache = new Dictionary<string, PropertyInfo>();

    string SessionFolder
    {
        get
        {
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(Application.persistentDataPath, saveRoot, $"P{participantId:D3}_{ts}");
        }
    }

    void Awake()
    {
        var folder = SessionFolder;
        Directory.CreateDirectory(folder);
        
        Debug.Log($"Experiment data will be saved to: {folder}");

        try
        {
            wFlow = new JsonLinesWriter(Path.Combine(folder, "flow.jsonl"));
            wCom = new JsonLinesWriter(Path.Combine(folder, "com_state.jsonl"));
            wPosture = new JsonLinesWriter(Path.Combine(folder, "posture.jsonl"));
            wGrasp = new JsonLinesWriter(Path.Combine(folder, "grasp.jsonl"));
            wFrame = new JsonLinesWriter(Path.Combine(folder, "frames.jsonl"));

            // 初始记录
            RecordInitialState();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize data loggers: {ex.Message}");
        }
    }

    void RecordInitialState()
    {
        var mode = GetMode();
        var com = GetComIndex();
        
        wFlow.WriteRecord(new FlowRecord {
            participant_id = participantId,
            mode = mode,
            mode_enter_time = Time.time,
            timestamp_iso = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        });
        
        lastMode = mode;
        lastCom = com;
    }

    void OnDestroy()
    {
        wFlow?.Dispose();
        wCom?.Dispose();
        wPosture?.Dispose();
        wGrasp?.Dispose();
        wFrame?.Dispose();
        
        Debug.Log("Experiment data logging stopped and files closed.");
    }

    void Update()
    {
        try
        {
            // 1) 监控模式切换
            CheckModeChange();

            // 2) 监控 COM 切换
            CheckComChange();

            // 3) 监控旋转模式进入/退出
            CheckRotateModeChange();

            // 4) 帧数据记录（可选频率控制）
            RecordFrameData();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in Update loop: {ex.Message}");
        }
    }

    void CheckModeChange()
    {
        var mode = GetMode();
        if (mode != lastMode)
        {
            wFlow.WriteRecord(new FlowRecord {
                participant_id = participantId,
                mode = mode,
                mode_enter_time = Time.time,
                timestamp_iso = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            });
            lastMode = mode;
            Debug.Log($"Mode changed to: {mode}");
        }
    }

    void CheckComChange()
    {
        int com = GetComIndex();
        if (com != lastCom)
        {
            WriteComRecord(com);
            lastCom = com;
            
            // 重置当前COM的旋转进入计数
            rotateEntryCountForCurrentCom = rotateEntryCountPerCom.ContainsKey(com) ? 
                rotateEntryCountPerCom[com] : 0;
            
            Debug.Log($"COM changed to: {com}");
        }
    }

    void CheckRotateModeChange()
    {
        bool nowRotate = IsInRotateMode();
        int currentCom = GetComIndex();
        
        if (nowRotate && !inRotate)
        {
            inRotate = true;
            rotateEnterTime = Time.time;
            rotateEntryCountForCurrentCom++;
            
            // 更新全局计数
            if (!rotateEntryCountPerCom.ContainsKey(currentCom))
                rotateEntryCountPerCom[currentCom] = 0;
            rotateEntryCountPerCom[currentCom]++;
            
            WritePostureRecord("enter", currentCom);
            Debug.Log($"Entered rotate mode for COM {currentCom}");
        }
        else if (!nowRotate && inRotate)
        {
            inRotate = false;
            WritePostureRecord("exit", currentCom);
            Debug.Log($"Exited rotate mode for COM {currentCom}");
        }
    }

    void RecordFrameData()
    {
        frameCounter++;
        if (frameCounter >= frameRecordInterval)
        {
            WriteFrameRecord();
            frameCounter = 0;
        }
    }

    void WriteComRecord(int comIndex)
    {
        var rec = new ComStateRecord {
            com_index = comIndex,
            com_enter_time = Time.time,
            com_enter_timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            rotate_button_press_count = GetRotateButtonPressCount(),
            com_relative_pos = GetComRelativePos(comIndex),
            rotate_enter_times_count = GetRotateEnterTimesCount(),
            rotate_mode_entry_count = rotateEntryCountForCurrentCom,
            current_rotate_duration = inRotate ? (Time.time - rotateEnterTime) : 0f
        };
        wCom.WriteRecord(rec);
    }

    void WritePostureRecord(string evt, int comIndex)
    {
        var rec = new PostureRecord {
            evt = evt,
            com_index = comIndex,
            time = Time.time,
            handtips_world = GetHandtipsWorld(),
            handtips_local = GetHandtipsLocal(),
            cubes_world = GetCubesWorld(),
            cubes_localYCenter = GetCubesLocalYCenter()
        };
        
        // 确保数组长度正确
        FixArrayLength(ref rec.handtips_world);
        FixArrayLength(ref rec.handtips_local);
        FixArrayLength(ref rec.cubes_world);
        FixArrayLength(ref rec.cubes_localYCenter);
        
        wPosture.WriteRecord(rec);
    }

    // 公共方法：供其他脚本调用记录抓取尝试
    public void RecordGraspAttempt()
    {
        try
        {
            var rec = new GraspAttemptRecord {
                com_index = GetComIndex(),
                grasp_attempt_count = GetGraspAttemptCount(),
                grasp_attempt_time = GetGraspAttemptTimes(),
                handtips_world = GetHandtipsWorld(),
                handtips_local = GetHandtipsLocal(),
                cubes_world = GetCubesWorld(),
                cubes_localYCenter = GetCubesLocalYCenter()
            };
            
            FixArrayLength(ref rec.handtips_world);
            FixArrayLength(ref rec.handtips_local);
            FixArrayLength(ref rec.cubes_world);
            FixArrayLength(ref rec.cubes_localYCenter);
            
            wGrasp.WriteRecord(rec);
            Debug.Log($"Recorded grasp attempt #{rec.grasp_attempt_count}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to record grasp attempt: {ex.Message}");
        }
    }

    void WriteFrameRecord()
    {
        var rec = new FrameRecord {
            timestamp_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            cylinder_hover_time = GetCylinderHoverTime(),
            cylinder_off_table = IsCylinderOffTable(),
            cylinder_off_threshold = IsCylinderOffThreshold(),
            cylinder_off_cubetouching = IsCylinderOffCubeTouching(),
            cube_center_distance = GetCubeCenterDistance(),
            handtips_world = GetHandtipsWorld(),
            handtips_local = GetHandtipsLocal(),
            cubes_world = GetCubesWorld(),
            cubes_localYCenter = GetCubesLocalYCenter(),
            cylinder_world_pos = GetCylinderWorldPos(),
            cylinder_euler = GetCylinderEuler(),
            slipping_distance5 = GetSlippingDistance5(),
            motor_speeds5 = GetMotorSpeeds5(),
            slip_process_time = GetSlipProcessTime()
        };
        
        FixArrayLength(ref rec.handtips_world);
        FixArrayLength(ref rec.handtips_local);
        FixArrayLength(ref rec.cubes_world);
        FixArrayLength(ref rec.cubes_localYCenter);
        FixArrayLength(ref rec.slipping_distance5);
        FixArrayLength(ref rec.motor_speeds5);
        
        wFrame.WriteRecord(rec);
    }

    // —— 数组长度修复工具 —— 
    void FixArrayLength(ref Vector3[] arr)
    {
        if (arr == null) arr = new Vector3[FINGERS];
        if (arr.Length != FINGERS)
        {
            var tmp = new Vector3[FINGERS];
            int copyLength = Mathf.Min(FINGERS, arr.Length);
            Array.Copy(arr, tmp, copyLength);
            arr = tmp;
        }
    }
    
    void FixArrayLength(ref float[] arr)
    {
        if (arr == null) arr = new float[FINGERS];
        if (arr.Length != FINGERS)
        {
            var tmp = new float[FINGERS];
            int copyLength = Mathf.Min(FINGERS, arr.Length);
            Array.Copy(arr, tmp, copyLength);
            arr = tmp;
        }
    }

    // —— 数据获取方法（桥接层）——
    // 注意：这些字段名需要与你的实际脚本字段名匹配！
    string GetMode() => SafeGet<string>(ModeSwitch, "currentMode") ?? "Unknown";
    int GetComIndex() => SafeGet<int>(CenterOfMassController, "selectedCOMIndex", 0);
    
    int GetRotateButtonPressCount() => SafeGet<int>(ButtonForRotateFingers, "pressCount", 0);
    
    Vector3 GetComRelativePos(int comIndex)
    {
        // 尝试获取COM列表中特定索引的位置
        var comList = SafeGet<object[]>(CenterOfMassController, "centerOfMassList", null);
        if (comList != null && comIndex >= 0 && comIndex < comList.Length)
        {
            var comObject = comList[comIndex];
            if (comObject != null)
            {
                return SafeGetFromObject<Vector3>(comObject, "relativePos", Vector3.zero);
            }
        }
        return Vector3.zero;
    }
    
    int GetRotateEnterTimesCount()
    {
        var times = SafeGet<float[]>(ButtonForRotateFingers, "EnterRotateModeFromCOMChange", null);
        return times?.Length ?? 0;
    }

    bool IsInRotateMode()
    {
        var mode = GetMode();
        return mode.IndexOf("rotate", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    Vector3[] GetHandtipsWorld() => SafeGet<Vector3[]>(VisualDisplay, "handtipsWorld", new Vector3[FINGERS]);
    Vector3[] GetHandtipsLocal() => SafeGet<Vector3[]>(VisualDisplay, "handtipsLocal", new Vector3[FINGERS]);
    Vector3[] GetCubesWorld() => SafeGet<Vector3[]>(VisualDisplay, "fingerCubesWorld", new Vector3[FINGERS]);
    Vector3[] GetCubesLocalYCenter() => SafeGet<Vector3[]>(VisualDisplay, "cubesLocalYCenter", new Vector3[FINGERS]);

    int GetGraspAttemptCount() => SafeGet<int>(Grasp_HandTracking, "AttemptCount", 0);
    float[] GetGraspAttemptTimes() => SafeGet<float[]>(Grasp_HandTracking, "AttemptTimes", new float[0]);

    float GetCylinderHoverTime() => SafeGet<float>(VisualDisplay, "CylinderHoverTime", 0f);
    bool IsCylinderOffTable() => SafeGet<bool>(VisualDisplay, "CylinderOffTable", false);
    bool IsCylinderOffThreshold() => SafeGet<bool>(VisualDisplay, "CylinderOffThreshold", false);
    bool IsCylinderOffCubeTouching() => SafeGet<bool>(VisualDisplay, "CylinderOffCubeTouching", false);
    float GetCubeCenterDistance() => SafeGet<float>(VisualDisplay, "CubeCenterDistance", 0f);

    Vector3 GetCylinderWorldPos() => SafeGet<Vector3>(VisualDisplay, "CylinderWorldPos", Vector3.zero);
    Vector3 GetCylinderEuler() => SafeGet<Vector3>(VisualDisplay, "CylinderEuler", Vector3.zero);

    float[] GetSlippingDistance5() => SafeGet<float[]>(VisualDisplay, "SlippingDistance5", new float[FINGERS]);
    float[] GetMotorSpeeds5() => SafeGet<float[]>(Slipping, "motor_speeds", new float[FINGERS]);
    float GetSlipProcessTime() => SafeGet<float>(Slipping, "slip_process_time", 0f);

    // —— 优化的反射安全读取 ——
    T SafeGet<T>(MonoBehaviour obj, string fieldName, T fallback = default(T))
    {
        if (obj == null) return fallback;
        
        try
        {
            var type = obj.GetType();
            var key = $"{type.Name}.{fieldName}";
            
            // 尝试从缓存获取字段信息
            if (!fieldCache.ContainsKey(key))
            {
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                fieldCache[key] = field;
            }
            
            if (fieldCache[key] != null)
            {
                var value = fieldCache[key].GetValue(obj);
                return value is T ? (T)value : fallback;
            }
            
            // 尝试属性
            if (!propertyCache.ContainsKey(key))
            {
                var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                propertyCache[key] = property;
            }
            
            if (propertyCache[key] != null)
            {
                var value = propertyCache[key].GetValue(obj);
                return value is T ? (T)value : fallback;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to get {fieldName} from {obj.GetType().Name}: {ex.Message}");
        }
        
        return fallback;
    }
    
    // 从任意对象获取字段值
    T SafeGetFromObject<T>(object obj, string fieldName, T fallback = default(T))
    {
        if (obj == null) return fallback;
        
        try
        {
            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var value = field.GetValue(obj);
                return value is T ? (T)value : fallback;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to get {fieldName} from object: {ex.Message}");
        }
        
        return fallback;
    }
    
    // 公共方法：手动强制保存所有数据
    [ContextMenu("Force Save All Data")]
    public void ForceSaveAllData()
    {
        try
        {
            wFlow?.Dispose();
            wCom?.Dispose();
            wPosture?.Dispose();
            wGrasp?.Dispose();
            wFrame?.Dispose();
            
            Debug.Log("All data forcibly saved and files closed.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during force save: {ex.Message}");
        }
    }
}