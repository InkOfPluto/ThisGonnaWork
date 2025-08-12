using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;

// =========================
// Data containers
// =========================
[System.Serializable]
public class TrialDataJSON
{
    public List<string> trialInfo = new List<string>();
    public List<string> participantId = new List<string>();
    public List<string> mode = new List<string>();                 // Visual / Haptic / VisualHaptic
    public List<float> mode_enter_time = new List<float>();

    public List<COMChangeLog> comChangeLogs = new List<COMChangeLog>();
    public List<RotateButtonLog> rotateButtonLogs = new List<RotateButtonLog>();
    public List<RotateModeLog> rotateModeLogs = new List<RotateModeLog>();

    public List<GraspAttemptLog> graspAttemptLogs = new List<GraspAttemptLog>();

    public List<long> timestamp_ms = new List<long>();
    public List<float> cylinder_hover_time = new List<float>();
    public List<int> cylinder_off_table = new List<int>();
    public List<int> cylinder_off_threshold = new List<int>();
    public List<int> cylinder_off_cubetouching = new List<int>();
    public List<float> cube_center_distance = new List<float>();

    public List<Vector3[]> handtips_world = new List<Vector3[]>();
    public List<Vector3[]> handtips_local = new List<Vector3[]>();
    public List<Vector3[]> cubes_world = new List<Vector3[]>();
    public List<Vector3[]> cubes_localYCenter = new List<Vector3[]>();

    public List<Vector3> cylinder_world_pos = new List<Vector3>();
    public List<Vector3> cylinder_euler = new List<Vector3>();

    public List<float[]> slipping_distance = new List<float[]>();
    public List<float[]> motor_speeds = new List<float[]>();
    public List<float> slip_process_time = new List<float>();

    public void ClearData()
    {
        trialInfo.Clear(); participantId.Clear(); mode.Clear(); mode_enter_time.Clear();
        comChangeLogs.Clear(); rotateButtonLogs.Clear(); rotateModeLogs.Clear(); graspAttemptLogs.Clear();
        timestamp_ms.Clear(); cylinder_hover_time.Clear(); cylinder_off_table.Clear(); cylinder_off_threshold.Clear();
        cylinder_off_cubetouching.Clear(); cube_center_distance.Clear();
        handtips_world.Clear(); handtips_local.Clear(); cubes_world.Clear(); cubes_localYCenter.Clear();
        cylinder_world_pos.Clear(); cylinder_euler.Clear(); slipping_distance.Clear(); motor_speeds.Clear(); slip_process_time.Clear();
    }
}

[System.Serializable]
public class COMChangeLog
{
    public long timestamp_ms;
    public int com_index;
    public float com_enter_time;
    public string com_enter_timestamp;
    public int rotate_button_press_count;
    public Vector3 com_relative_pos;
    public List<float> rotate_enter_times = new List<float>();
    public int rotate_mode_entry_count;
}

[System.Serializable]
public class RotateButtonLog
{
    public long timestamp_ms;
    public int com_index;
    public int press_count;
}

[System.Serializable]
public class RotateModeLog
{
    public long timestamp_ms;
    public string type;                 // "enter" or "exit"
    public string source_or_reason;     // enter: source, exit: reason
    public float duration;              // only for exit
    public int com_index;

    public PoseSnapshot snapshot;
}

[System.Serializable]
public class GraspAttemptLog
{
    public long timestamp_ms;
    public int attempt_index;
    public PoseSnapshot snapshot;
}

[System.Serializable]
public class PoseSnapshot
{
    public Vector3[] handtips_world = new Vector3[5];
    public Vector3[] handtips_local = new Vector3[5];
    public Vector3[] cubes_world = new Vector3[5];
    public Vector3[] cubes_localYCenter = new Vector3[5];
}

// =========================
// Main behaviour
// =========================
public class ExperimentSaveData_JSON : MonoBehaviour
{
    public TrialDataJSON trialData;

    [Header("Session Meta")]
    public string participantNo = "P001";
    public string savePath = "Data/";   // 相对 Assets 的目录

    [Header("Live State (wired from your systems)")]
    public string currentMode = "Visual";
    public int currentCOMIndex = -1;
    public int rotateButtonPressCount = 0;

    [Header("References for auto sampling (optional)")]
    public Transform cylinder;
    public Transform[] handTips = new Transform[5];
    public Transform[] fingerCubes = new Transform[5];
    public Transform handCenter;
    public Transform localYCenterWorld;
    public Slipping slipping;  // optional

    [Header("Control")]
    public bool autoLogEveryFrame = true;
    public KeyCode saveKey = KeyCode.N; // ← 已改成 N

    // Internals
    private Coroutine saveDataCoroutine;
    private int trialNumber = 0;
    private float trialStartTime;
    private float modeEnterTime = 0f;

    // COM state cache
    private float comEnterTime = 0f;
    private string comEnterTimestamp = "";
    private readonly List<float> rotateEnterTimesCache = new List<float>();
    private bool inRotateMode = false;
    private float rotateModeEnterTime = 0f;
    private int graspAttemptCount = 0;

    // 新增：会话文件夹（按参与者+时间戳）
    private string sessionFolderPath = null;
    private string sessionFolderStamp = null;

    void Awake()
    {
        trialData = new TrialDataJSON();
    }

    void Start()
    {
        trialStartTime = Time.time;
        // 会话文件夹采用“首次保存时”懒创建，以当前 participantNo + 时间戳 命名
    }

    void Update()
    {
        if (autoLogEveryFrame) LogFrameNow();

        if (Input.GetKeyDown(saveKey))
        {
            if (saveDataCoroutine != null) StopCoroutine(saveDataCoroutine);
            saveDataCoroutine = StartCoroutine(SaveFile());
            trialNumber++;
            trialStartTime = Time.time;
            graspAttemptCount = 0;
        }
    }

    // =========================
    // Public API
    // =========================
    public void SetParticipant(string pid)
    {
        participantNo = SanitizeForPath(pid);
        // 参与者改变，重置会话文件夹以便为新参与者新建文件夹
        sessionFolderPath = null;
        sessionFolderStamp = null;
    }

    public void OnModeChanged(string modeString)
    {
        currentMode = modeString;
        modeEnterTime = Time.time;
        trialData.mode.Add(currentMode);
        trialData.mode_enter_time.Add(modeEnterTime);
    }

    public void OnCOMChanged(int newComIndex, Vector3 relativePos)
    {
        currentCOMIndex = newComIndex;
        rotateButtonPressCount = 0;
        rotateEnterTimesCache.Clear();
        graspAttemptCount = 0;

        comEnterTime = Time.time;
        comEnterTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        var entry = new COMChangeLog
        {
            timestamp_ms = NowMs(),
            com_index = currentCOMIndex,
            com_enter_time = comEnterTime,
            com_enter_timestamp = comEnterTimestamp,
            rotate_button_press_count = rotateButtonPressCount,
            com_relative_pos = relativePos,
            rotate_enter_times = new List<float>(rotateEnterTimesCache),
            rotate_mode_entry_count = 0
        };
        trialData.comChangeLogs.Add(entry);
    }

    public void OnRotateButtonPressed()
    {
        rotateButtonPressCount++;
        trialData.rotateButtonLogs.Add(new RotateButtonLog
        {
            timestamp_ms = NowMs(),
            com_index = currentCOMIndex,
            press_count = rotateButtonPressCount
        });
    }

    public void EnterRotateMode(string source = "button_or_com_change")
    {
        inRotateMode = true;
        rotateModeEnterTime = Time.time;
        rotateEnterTimesCache.Add(rotateModeEnterTime);

        if (trialData.comChangeLogs.Count > 0)
        {
            var last = trialData.comChangeLogs[trialData.comChangeLogs.Count - 1];
            last.rotate_enter_times = new List<float>(rotateEnterTimesCache);
            last.rotate_mode_entry_count = rotateEnterTimesCache.Count;
            trialData.comChangeLogs[trialData.comChangeLogs.Count - 1] = last;
        }

        trialData.rotateModeLogs.Add(new RotateModeLog
        {
            timestamp_ms = NowMs(),
            type = "enter",
            source_or_reason = source,
            duration = 0f,
            com_index = currentCOMIndex,
            snapshot = CapturePoseSnapshot()
        });
    }

    public void ExitRotateMode(string reason = "user_exit")
    {
        if (!inRotateMode) return;
        inRotateMode = false;
        float dur = Time.time - rotateModeEnterTime;
        trialData.rotateModeLogs.Add(new RotateModeLog
        {
            timestamp_ms = NowMs(),
            type = "exit",
            source_or_reason = reason,
            duration = dur,
            com_index = currentCOMIndex,
            snapshot = CapturePoseSnapshot()
        });
    }

    public void OnGraspAttempt()
    {
        int idx = graspAttemptCount++;
        trialData.graspAttemptLogs.Add(new GraspAttemptLog
        {
            timestamp_ms = NowMs(),
            attempt_index = idx,
            snapshot = CapturePoseSnapshot()
        });
    }

    // =========================
    // Per-frame logging
    // =========================
    public void LogFrameNow()
    {
        long tms = NowMs();
        trialData.timestamp_ms.Add(tms);

        trialData.trialInfo.Add($"Trial {trialNumber}");
        trialData.participantId.Add(participantNo);
        trialData.mode.Add(currentMode);
        trialData.mode_enter_time.Add(modeEnterTime);

        if (slipping)
        {
            trialData.motor_speeds.Add(slipping.GetMotorSpeeds5());
            trialData.slip_process_time.Add(slipping.GetSlipProcessTime());
        }
        else
        {
            trialData.motor_speeds.Add(new float[5]);
            trialData.slip_process_time.Add(0f);
        }

        trialData.cylinder_world_pos.Add(cylinder ? cylinder.position : Vector3.zero);
        trialData.cylinder_euler.Add(cylinder ? cylinder.eulerAngles : Vector3.zero);

        trialData.handtips_world.Add(CapturePositions(handTips));
        trialData.handtips_local.Add(CaptureLocalPositions(handTips, handCenter));
        trialData.cubes_world.Add(CapturePositions(fingerCubes));
        trialData.cubes_localYCenter.Add(CaptureLocalPositions(fingerCubes, localYCenterWorld));
    }

    // =========================
    // Save
    // =========================
    private IEnumerator SaveFile()
    {
        // 根目录（Assets/savePath）
        string root = Path.Combine(Application.dataPath, savePath);
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);

        // 懒创建会话文件夹：ParticipantID_{ID}_{Stamp}
        if (string.IsNullOrEmpty(sessionFolderPath))
        {
            sessionFolderStamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folderName = $"ParticipantID_{participantNo}_{sessionFolderStamp}";
            sessionFolderPath = Path.Combine(root, folderName);
            Directory.CreateDirectory(sessionFolderPath);
            Debug.Log($"[Save] 创建会话文件夹: {sessionFolderPath}");
        }

        string fileBase = $"Trial_{trialNumber}_P{participantNo}_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        string fileName = Path.Combine(sessionFolderPath, fileBase + ".json");

        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented
        };

        string jsonString = JsonConvert.SerializeObject(trialData, settings);
        File.WriteAllText(fileName, jsonString);
        Debug.Log($"JSON saved: {fileName}");

        // 下一次试验准备
        trialData.ClearData();
        modeEnterTime = Time.time;
        yield return null;
    }

    // =========================
    // Helpers
    // =========================
    private PoseSnapshot CapturePoseSnapshot()
    {
        var snap = new PoseSnapshot();
        snap.handtips_world = CapturePositions(handTips);
        snap.handtips_local = CaptureLocalPositions(handTips, handCenter);
        snap.cubes_world = CapturePositions(fingerCubes);
        snap.cubes_localYCenter = CaptureLocalPositions(fingerCubes, localYCenterWorld);
        return snap;
    }

    private Vector3[] CapturePositions(Transform[] trs)
    {
        var arr = new Vector3[5];
        for (int i = 0; i < arr.Length && i < (trs?.Length ?? 0); i++)
            arr[i] = trs[i] ? trs[i].position : Vector3.zero;
        return arr;
    }

    private Vector3[] CaptureLocalPositions(Transform[] trs, Transform reference)
    {
        var arr = new Vector3[5];
        for (int i = 0; i < arr.Length && i < (trs?.Length ?? 0); i++)
        {
            if (trs[i]) arr[i] = reference ? reference.InverseTransformPoint(trs[i].position) : trs[i].localPosition;
            else arr[i] = Vector3.zero;
        }
        return arr;
    }

    private static long NowMs() => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);

    // 路径安全化（去掉非法字符）
    private static string SanitizeForPath(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Unknown";
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c.ToString(), "_");
        foreach (var c in Path.GetInvalidPathChars())
            s = s.Replace(c.ToString(), "_");
        return s.Trim();
    }
}

/* =========================
Integration guide (match your scripts):

// ModeSwitch
//   saveJSON.SetParticipant(participantID.ToString());
//   saveJSON.OnModeChanged(currentMode.ToString());

// CenterOfMassController
//   saveJSON.OnCOMChanged(selectedCOMIndex, centerOfMassList[selectedCOMIndex]);

// ButtonForRotateFingers
//   saveJSON.OnRotateButtonPressed();
//   saveJSON.EnterRotateMode("button");
//   saveJSON.ExitRotateMode("button_release");

// Rotate mode enter via COM change
//   saveJSON.EnterRotateMode("com_change");

// Grasp_HandTracking (on Closing detected)
///  saveJSON.OnGraspAttempt();

// Auto per-frame logging
//   set ExperimentSaveData_JSON.autoLogEveryFrame = true and assign references in Inspector

// 保存热键：按 N 保存到当前会话文件夹
*/
