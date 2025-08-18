using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// 单层级、逐帧统一采样数据记录器（扁平化版本）
/// - 不修改其它脚本，仅通过 Inspector 引用与已公开的属性/事件读取
/// - 每帧采样，保证所有字段数组长度一致（缺引用则补默认值）
/// - JSON 扁平（单层字段名 → 值数组，所有 Vector3 拆为 x/y/z 三列）
/// - 保存键：6（主键盘/小键盘）；5 清空并重新开始一段
///
/// 本版修改点：
/// 1) Follow/UpDown/SlippingOneFinger 继续按 Behaviour.enabled 记录；
/// 2) Cylinder、CubeCenter 和五个 FingerCubes 按 GameObject.activeInHierarchy 记录，并对它们进行真正“边沿检测”，每次 on/off 都刷新 last_on/last_off；
/// 3) 阈值（Threshold）与 Goal 的进入/离开采用边沿检测（每次进入/离开都更新）；Goal 结果事件为“脉冲”，当帧为 true，随后复位；
/// 4) OutOfAttempt 以脉冲+时间戳记录（保持不变）；
/// 5) ★ 新增：记录 grasp_state 更改的时间戳。
/// 6) ★ 新增：记录 COM 的坐标、离中心的距离和角度。
/// </summary>
public class UnifiedDataLogger : MonoBehaviour
{
    [Header("保存设置")]
    [Tooltip("保存目录根路径（空则用 Application.persistentDataPath）")]
    public string rootPath = "";
    [Tooltip("是否使用后台线程写文件")]
    public bool threadedWrite = true;
    [Tooltip("文件名前缀")]
    public string filePrefix = "Session";

    [Header("外部脚本与对象（仅拖拽，不改它们）")]
    public ModeSwitch modeSwitch;                     // 参与者编号 & 当前模式
    public CenterOfMassController comController;      // 质心编号
    public Grasp_HandTracking graspHT;                // attempt 与抓取状态
    public VisualDisplay visual;                      // DistanceDA/SHI/.
    public SlippingOneFinger slipping;                // 串口输出（GetMotorSpeeds5）
    public GoalTriggerController goal;                // 目标触发事件
    public ThresholdLockAndReset thresholdLock;       // 判断是否在 Threshold（可选）

    [Header("对象与位姿（相对参照计算）")]
    public Transform cylinder;                // 圆柱体
    public Transform cubeCenter;              // CubeCenter
    public Transform handCenter;              // 手掌中心（注意：独立于 Wrist）
    public Transform rWristPalm;              // R_Wrist_Palm

    [Header("右手指尖（相对 handCenter）")]
    public Transform rThumbTip, rIndexTip, rMiddleTip, rRingTip, rLittleTip;

    [Header("五个指尖方块/可视化 Cube（相对 cylinder）")]
    public Transform fingerDA, fingerSHI, fingerZHONG, fingerWU, fingerXIAO;

    [Header("可选：功能脚本开关（用于记录 enabled 开/关时间）")]
    public Behaviour followHandTracking;     // Follow_HandTracking（依旧按脚本 enabled）
    public Behaviour updownHandTracking;     // UpDown_HandTracking（依旧按脚本 enabled）

    // —— 以下按 GameObject.activeInHierarchy 记录 —— 
    public GameObject fingerDA_Obj;
    public GameObject fingerSHI_Obj;
    public GameObject fingerZHONG_Obj;
    public GameObject fingerWU_Obj;
    public GameObject fingerXIAO_Obj;
    public GameObject cubeCenter_Obj;
    public GameObject cylinder_Obj;

    // ―― 单层级数据容器（全部键都在一层；Vector3 全部拆为 x/y/z）――
    [Serializable]
    public class FlatData
    {
        // 基本时间与元信息
        public List<float> time = new();
        public List<int> participantId = new();
        public List<string> currentMode = new();
        public List<float> mode_enter_time = new();

        public List<int> com_index = new();
        public List<float> com_enter_time = new();
        // ★ 新增：COM 位置、距离、角度
        public List<float> COM_pos_x = new();
        public List<float> COM_pos_y = new();
        public List<float> COM_pos_z = new();
        public List<float> COM_distance_from_center = new();
        public List<float> COM_angle_from_center = new();

        public List<int> attempt_index = new();
        public List<float> attempt_enter_time = new();

        public List<string> grasp_state = new();
        // ★ 新增：每帧记录“最近一次 grasp_state 发生变化”的时间戳
        public List<float> grasp_state_change_time = new();

        // VisualDisplay
        public List<float> DistanceDA = new();
        public List<float> DistanceSHI = new();
        public List<float> DistanceZHONG = new();
        public List<float> DistanceWU = new();
        public List<float> DistanceXIAO = new();

        // 串口与 Slipping
        public List<string> serial_out = new();
        public List<bool> SlippingOneFinger_enabled = new();
        public List<float> SlippingOneFinger_last_on_time = new();
        public List<float> SlippingOneFinger_last_off_time = new();

        // Cylinder & CubeCenter（世界位姿 + 额外指标）
        public List<bool> Cylinder_enabled = new();
        public List<float> Cylinder_last_on_time = new();
        public List<float> Cylinder_last_off_time = new();
        public List<float> Cylinder_world_pos_x = new();
        public List<float> Cylinder_world_pos_y = new();
        public List<float> Cylinder_world_pos_z = new();
        public List<float> Cylinder_world_euler_x = new();
        public List<float> Cylinder_world_euler_y = new();
        public List<float> Cylinder_world_euler_z = new();
        public List<float> Cylinder_height_from_init = new(); // y - 0.75f

        public List<bool> CubeCenter_enabled = new();
        public List<float> CubeCenter_last_on_time = new();
        public List<float> CubeCenter_last_off_time = new();
        public List<float> CubeCenter_world_pos_x = new();
        public List<float> CubeCenter_world_pos_y = new();
        public List<float> CubeCenter_world_pos_z = new();
        public List<float> CubeCenter_world_euler_x = new();
        public List<float> CubeCenter_world_euler_y = new();
        public List<float> CubeCenter_world_euler_z = new();
        public List<float> CubeCenter_pos_rel_cylinder_x = new();
        public List<float> CubeCenter_pos_rel_cylinder_y = new();
        public List<float> CubeCenter_pos_rel_cylinder_z = new();
        public List<float> CubeCenter_angle_rel_cylinder = new();

        // 五个 FingerCube（相对 cylinder）
        public List<bool> FingerDA_enabled = new(); public List<float> FingerDA_last_on_time = new(); public List<float> FingerDA_last_off_time = new();
        public List<float> FingerDA_world_pos_x = new(); public List<float> FingerDA_world_pos_y = new(); public List<float> FingerDA_world_pos_z = new();
        public List<float> FingerDA_world_euler_x = new(); public List<float> FingerDA_world_euler_y = new(); public List<float> FingerDA_world_euler_z = new();
        public List<float> FingerDA_pos_rel_cylinder_x = new(); public List<float> FingerDA_pos_rel_cylinder_y = new(); public List<float> FingerDA_pos_rel_cylinder_z = new();
        public List<float> FingerDA_angle_rel_cylinder = new();

        public List<bool> FingerSHI_enabled = new(); public List<float> FingerSHI_last_on_time = new(); public List<float> FingerSHI_last_off_time = new();
        public List<float> FingerSHI_world_pos_x = new(); public List<float> FingerSHI_world_pos_y = new(); public List<float> FingerSHI_world_pos_z = new();
        public List<float> FingerSHI_world_euler_x = new(); public List<float> FingerSHI_world_euler_y = new(); public List<float> FingerSHI_world_euler_z = new();
        public List<float> FingerSHI_pos_rel_cylinder_x = new(); public List<float> FingerSHI_pos_rel_cylinder_y = new(); public List<float> FingerSHI_pos_rel_cylinder_z = new();
        public List<float> FingerSHI_angle_rel_cylinder = new();

        public List<bool> FingerZHONG_enabled = new(); public List<float> FingerZHONG_last_on_time = new(); public List<float> FingerZHONG_last_off_time = new();
        public List<float> FingerZHONG_world_pos_x = new(); public List<float> FingerZHONG_world_pos_y = new();
        public List<float> FingerZHONG_world_pos_z = new(); public List<float> FingerZHONG_world_euler_x = new();
        public List<float> FingerZHONG_world_euler_y = new(); public List<float> FingerZHONG_world_euler_z = new();
        public List<float> FingerZHONG_pos_rel_cylinder_x = new(); public List<float> FingerZHONG_pos_rel_cylinder_y = new();
        public List<float> FingerZHONG_pos_rel_cylinder_z = new(); public List<float> FingerZHONG_angle_rel_cylinder = new();

        public List<bool> FingerWU_enabled = new(); public List<float> FingerWU_last_on_time = new(); public List<float> FingerWU_last_off_time = new();
        public List<float> FingerWU_world_pos_x = new(); public List<float> FingerWU_world_pos_y = new();
        public List<float> FingerWU_world_pos_z = new(); public List<float> FingerWU_world_euler_x = new();
        public List<float> FingerWU_world_euler_y = new(); public List<float> FingerWU_world_euler_z = new();
        public List<float> FingerWU_pos_rel_cylinder_x = new(); public List<float> FingerWU_pos_rel_cylinder_y = new();
        public List<float> FingerWU_pos_rel_cylinder_z = new(); public List<float> FingerWU_angle_rel_cylinder = new();

        public List<bool> FingerXIAO_enabled = new(); public List<float> FingerXIAO_last_on_time = new(); public List<float> FingerXIAO_last_off_time = new();
        public List<float> FingerXIAO_world_pos_x = new(); public List<float> FingerXIAO_world_pos_y = new();
        public List<float> FingerXIAO_world_pos_z = new(); public List<float> FingerXIAO_world_euler_x = new();
        public List<float> FingerXIAO_world_euler_y = new(); public List<float> FingerXIAO_world_euler_z = new();
        public List<float> FingerXIAO_pos_rel_cylinder_x = new(); public List<float> FingerXIAO_pos_rel_cylinder_y = new();
        public List<float> FingerXIAO_pos_rel_cylinder_z = new(); public List<float> FingerXIAO_angle_rel_cylinder = new();

        // HandCenter（世界）
        public List<float> HandCenter_world_pos_x = new(); public List<float> HandCenter_world_pos_y = new(); public List<float> HandCenter_world_pos_z = new();
        public List<float> HandCenter_world_euler_x = new(); public List<float> HandCenter_world_euler_y = new(); public List<float> HandCenter_world_euler_z = new();

        // Wrist（世界）
        public List<float> R_Wrist_Palm_world_pos_x = new(); public List<float> R_Wrist_Palm_world_pos_y = new(); public List<float> R_Wrist_Palm_world_pos_z = new();
        public List<float> R_Wrist_Palm_world_euler_x = new(); public List<float> R_Wrist_Palm_world_euler_y = new(); public List<float> R_Wrist_Palm_world_euler_z = new();

        // 五指尖相对 handCenter（同时记录世界位姿）
        public List<float> R_ThumbTip_world_pos_x = new(); public List<float> R_ThumbTip_world_pos_y = new(); public List<float> R_ThumbTip_world_pos_z = new();
        public List<float> R_ThumbTip_world_euler_x = new(); public List<float> R_ThumbTip_world_euler_y = new(); public List<float> R_ThumbTip_world_euler_z = new();
        public List<float> R_ThumbTip_pos_rel_hand_x = new(); public List<float> R_ThumbTip_pos_rel_hand_y = new(); public List<float> R_ThumbTip_pos_rel_hand_z = new();
        public List<float> R_ThumbTip_angle_rel_hand = new();

        public List<float> R_IndexTip_world_pos_x = new(); public List<float> R_IndexTip_world_pos_y = new(); public List<float> R_IndexTip_world_pos_z = new();
        public List<float> R_IndexTip_world_euler_x = new(); public List<float> R_IndexTip_world_euler_y = new(); public List<float> R_IndexTip_world_euler_z = new();
        public List<float> R_IndexTip_pos_rel_hand_x = new(); public List<float> R_IndexTip_pos_rel_hand_y = new(); public List<float> R_IndexTip_pos_rel_hand_z = new();
        public List<float> R_IndexTip_angle_rel_hand = new();

        public List<float> R_MiddleTip_world_pos_x = new(); public List<float> R_MiddleTip_world_pos_y = new(); public List<float> R_MiddleTip_world_pos_z = new();
        public List<float> R_MiddleTip_world_euler_x = new(); public List<float> R_MiddleTip_world_euler_y = new(); public List<float> R_MiddleTip_world_euler_z = new();
        public List<float> R_MiddleTip_pos_rel_hand_x = new(); public List<float> R_MiddleTip_pos_rel_hand_y = new(); public List<float> R_MiddleTip_pos_rel_hand_z = new();
        public List<float> R_MiddleTip_angle_rel_hand = new();

        public List<float> R_RingTip_world_pos_x = new(); public List<float> R_RingTip_world_pos_y = new(); public List<float> R_RingTip_world_pos_z = new();
        public List<float> R_RingTip_world_euler_x = new(); public List<float> R_RingTip_world_euler_y = new(); public List<float> R_RingTip_world_euler_z = new();
        public List<float> R_RingTip_pos_rel_hand_x = new(); public List<float> R_RingTip_pos_rel_hand_y = new(); public List<float> R_RingTip_pos_rel_hand_z = new();
        public List<float> R_RingTip_angle_rel_hand = new();

        public List<float> R_LittleTip_world_pos_x = new(); public List<float> R_LittleTip_world_pos_y = new(); public List<float> R_LittleTip_world_pos_z = new();
        public List<float> R_LittleTip_world_euler_x = new(); public List<float> R_LittleTip_world_euler_y = new(); public List<float> R_LittleTip_world_euler_z = new();
        public List<float> R_LittleTip_pos_rel_hand_x = new(); public List<float> R_LittleTip_pos_rel_hand_y = new(); public List<float> R_LittleTip_pos_rel_hand_z = new();
        public List<float> R_LittleTip_angle_rel_hand = new();

        // Follow / UpDown 功能脚本开关
        public List<bool> Follow_enabled = new(); public List<float> Follow_last_on_time = new(); public List<float> Follow_last_off_time = new();
        public List<bool> UpDown_enabled = new(); public List<float> UpDown_last_on_time = new(); public List<float> UpDown_last_off_time = new();

        // 桌面接触 & Threshold
        public List<bool> Cylinder_on_table = new(); public List<float> Cylinder_touch_time = new(); public List<float> Cylinder_leave_time = new();
        public List<bool> Cylinder_in_threshold = new(); public List<float> Threshold_last_enter_time = new(); public List<float> Threshold_last_exit_time = new();

        // Goal 区域与事件（结果事件为脉冲）
        public List<bool> Goal_above = new(); public List<float> Goal_enter_time = new(); public List<float> Goal_leave_time = new();
        public List<bool> Goal_OnHoldSucceeded = new(); public List<float> Goal_OnHoldSucceeded_time = new();
        public List<bool> Goal_OnHoldInterrupted = new(); public List<float> Goal_OnHoldInterrupted_time = new();
        public List<bool> Goal_OnHoldFailedTilt = new(); public List<float> Goal_OnHoldFailedTilt_time = new();

        // OutOfAttempt（脉冲）
        public List<bool> OutOfAttempt_trigger = new();
        public List<float> OutOfAttempt_time = new();

        // 采样完整性（调试用）
        public List<int> sample_ok = new();
    }

    private FlatData D = new();

    private float startTime;
    private float lastModeEnter = 0f, lastComEnter = 0f, lastAttemptEnter = 0f;

    // 边沿检测：记录开关的 last on/off 时间（脚本）
    private float lastSlipOn = -1f, lastSlipOff = -1f;
    private float lastFollowOn = -1f, lastFollowOff = -1f, lastUpDownOn = -1f, lastUpDownOff = -1f;

    // Cylinder / CubeCenter（GameObject）
    private float lastCylOn = -1f, lastCylOff = -1f, lastCubeOn = -1f, lastCubeOff = -1f;
    private bool prevCyl = false, prevCube = false;

    // 五个 FingerCube（GameObject）
    private bool prevFingerDA = false, prevFingerSHI = false, prevFingerZHONG = false, prevFingerWU = false, prevFingerXIAO = false;
    private float lastFingerDAOn = -1f, lastFingerDAOff = -1f;
    private float lastFingerSHIOn = -1f, lastFingerSHIOff = -1f;
    private float lastFingerZHONGOn = -1f, lastFingerZHONGOff = -1f;
    private float lastFingerWUOn = -1f, lastFingerWUOff = -1f;
    private float lastFingerXIAOOn = -1f, lastFingerXIAOOff = -1f;

    // Follow/UpDown/Slipping（脚本）
    private bool prevFollow = false, prevUpDown = false, prevSlip = false;

    // 桌面接触
    private bool cylinderOnTable = false; private float lastTouchTable = -1f, lastLeaveTable = -1f;

    // Threshold 进入/离开（多次边沿）
    private float lastThreshEnter = -1f, lastThreshExit = -1f;
    private bool prevInThreshold = false;

    // Goal 区域进入/离开（多次边沿）
    private float lastGoalEnter = -1f, lastGoalLeave = -1f;
    private bool prevGoalAbove = false;

    // Goal 结果事件（脉冲）
    private bool goalSucceededPulse = false, goalInterruptedPulse = false, goalFailedTiltPulse = false;
    private float lastGoalSucceeded = -1f, lastGoalInterrupted = -1f, lastGoalFailedTilt = -1f;

    private string prevModeStr = "";
    private int prevCom = int.MinValue, prevAttempt = int.MinValue;

    private int fileIndex = 0;

    // OutOfAttempt 状态
    private bool outOfAttemptPulse = false;
    private float lastOutOfAttemptTime = -1f;
    private bool reachedAttemptLimit = false;

    // ★ 新增：grasp_state 更改时间戳跟踪
    private string prevGraspStateStr = "";
    private float lastGraspStateChange = -1f;

    private void OnEnable()
    {
        // 圆柱体-桌面接触
        CylinderTableContact.OnCylinderTouchTable += HandleTouchTable;
        CylinderTableContact.OnCylinderLeaveTable += HandleLeaveTable;

        // Goal 事件（脉冲）
        if (goal != null)
        {
            goal.OnHoldSucceeded += OnGoalSucceeded;
            goal.OnHoldInterrupted += OnGoalInterrupted;
            goal.OnHoldFailedTilt += OnGoalFailedTilt;
        }
    }

    private void OnDisable()
    {
        CylinderTableContact.OnCylinderTouchTable -= HandleTouchTable;
        CylinderTableContact.OnCylinderLeaveTable -= HandleLeaveTable;

        if (goal != null)
        {
            goal.OnHoldSucceeded -= OnGoalSucceeded;
            goal.OnHoldInterrupted -= OnGoalInterrupted;
            goal.OnHoldFailedTilt -= OnGoalFailedTilt;
        }
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(rootPath)) rootPath = Application.persistentDataPath;
        Directory.CreateDirectory(Path.Combine(rootPath, "Data"));
        InitSegmentState(); // 统一用这个初始化首段
    }

    private void Update()
    {
        // ―― 键位：5 清空并开始新段；6 保存 ―― //
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
        {
            ClearDataForNextSegment();
            Debug.Log("[UnifiedDataLogger] Cleared data and started a new segment.");
        }
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
        {
            SaveNow();
        }

        float t = Now();
        bool ok = true;

        // 行为脚本（Behaviour.enabled）边沿
        EdgeWatch(ref prevFollow, followHandTracking, ref lastFollowOn, ref lastFollowOff, t);
        EdgeWatch(ref prevUpDown, updownHandTracking, ref lastUpDownOn, ref lastUpDownOff, t);
        EdgeWatch(ref prevSlip, slipping, ref lastSlipOn, ref lastSlipOff, t);

        // GameObject（activeInHierarchy）边沿：Cylinder / CubeCenter / 五指方块
        EdgeWatch(ref prevCyl, cylinder_Obj, ref lastCylOn, ref lastCylOff, t);
        EdgeWatch(ref prevCube, cubeCenter_Obj, ref lastCubeOn, ref lastCubeOff, t);
        EdgeWatch(ref prevFingerDA, fingerDA_Obj, ref lastFingerDAOn, ref lastFingerDAOff, t);
        EdgeWatch(ref prevFingerSHI, fingerSHI_Obj, ref lastFingerSHIOn, ref lastFingerSHIOff, t);
        EdgeWatch(ref prevFingerZHONG, fingerZHONG_Obj, ref lastFingerZHONGOn, ref lastFingerZHONGOff, t);
        EdgeWatch(ref prevFingerWU, fingerWU_Obj, ref lastFingerWUOn, ref lastFingerWUOff, t);
        EdgeWatch(ref prevFingerXIAO, fingerXIAO_Obj, ref lastFingerXIAOOn, ref lastFingerXIAOOff, t);

        // 基本时间与元信息
        D.time.Add(t);

        int pid = (modeSwitch != null) ? modeSwitch.participantID : -1;
        D.participantId.Add(pid);

        string modeStr = (modeSwitch != null) ? modeSwitch.currentMode.ToString() : "Unknown";
        D.currentMode.Add(modeStr);
        D.mode_enter_time.Add(lastModeEnter);

        int comIdx = (comController != null) ? comController.selectedCOMIndex : -1;
        D.com_index.Add(comIdx);
        D.com_enter_time.Add(lastComEnter);

        if (comController != null && comIdx >= 0 && comIdx < comController.centerOfMassList.Length)
        {
            Vector3 local = comController.centerOfMassList[comIdx];
            Vector3 world = comController.targetObject.transform.TransformPoint(local);
            D.COM_pos_x.Add(world.x);
            D.COM_pos_y.Add(world.y);
            D.COM_pos_z.Add(world.z);

            Vector3 refPos = (comController.cylinderCenter != null) ? comController.cylinderCenter.position : (cylinder != null ? cylinder.position : Vector3.zero);
            Vector3 diff = world - refPos;
            float dist = new Vector2(diff.x, diff.z).magnitude;
            float ang = Mathf.Atan2(diff.z, diff.x) * Mathf.Rad2Deg; // X 轴为 0°
            D.COM_distance_from_center.Add(dist);
            D.COM_angle_from_center.Add(ang);
        }
        else
        {
            D.COM_pos_x.Add(0f);
            D.COM_pos_y.Add(0f);
            D.COM_pos_z.Add(0f);
            D.COM_distance_from_center.Add(0f);
            D.COM_angle_from_center.Add(0f);
        }

        int attempt = (graspHT != null) ? graspHT.AttemptCount : -1;
        D.attempt_index.Add(attempt);
        D.attempt_enter_time.Add(lastAttemptEnter);

        // —— 抓握状态 + ★ 边沿时间戳 —— //
        string gs = GetGraspState(graspHT);
        if (gs != prevGraspStateStr)
        {
            lastGraspStateChange = t;         // ★ 记录本次状态改变的时刻
            prevGraspStateStr = gs;           // ★ 更新前态
        }
        D.grasp_state.Add(gs);
        D.grasp_state_change_time.Add(lastGraspStateChange); // ★ 每帧写入“最近一次变更时间”

        // —— OutOfAttempt 侦测与记录（保持不变）——
        int limit = (graspHT != null) ? graspHT.attemptLimit : int.MaxValue;

        // 达到/超过上限 → 进入“武装态”
        if (!reachedAttemptLimit && attempt >= 0 && limit != int.MaxValue && attempt >= limit)
        {
            reachedAttemptLimit = true;
        }

        // 两种“真正触发”条件（择一）：
        bool comChangedThisFrame = (comIdx != prevCom && prevCom != int.MinValue);
        bool attemptResetFromLimit = (prevAttempt >= limit && attempt >= 0 && attempt <= 1);

        if (reachedAttemptLimit && (comChangedThisFrame || attemptResetFromLimit))
        {
            outOfAttemptPulse = true;
            lastOutOfAttemptTime = t;
            reachedAttemptLimit = false; // 解除武装
        }

        // 写入 OutOfAttempt 脉冲与时间戳
        D.OutOfAttempt_trigger.Add(outOfAttemptPulse);
        D.OutOfAttempt_time.Add(lastOutOfAttemptTime);
        outOfAttemptPulse = false;

        // VisualDisplay
        if (visual != null)
        {
            D.DistanceDA.Add(visual.DistanceDA);
            D.DistanceSHI.Add(visual.DistanceSHI);
            D.DistanceZHONG.Add(visual.DistanceZHONG);
            D.DistanceWU.Add(visual.DistanceWU);
            D.DistanceXIAO.Add(visual.DistanceXIAO);
        }
        else { ok = false; FillZeros(D.DistanceDA, D.DistanceSHI, D.DistanceZHONG, D.DistanceWU, D.DistanceXIAO); }

        // 串口输出/Slipping（脚本 enabled）
        if (slipping != null)
        {
            float[] speeds = slipping.GetMotorSpeeds5(); // 假定存在
            string token = $"t:{speeds[0]},i:{speeds[1]},m:{speeds[2]},r:{speeds[3]},l:{speeds[4]}"; // l=Little
            D.serial_out.Add(token);
            D.SlippingOneFinger_enabled.Add(slipping.enabled);
            D.SlippingOneFinger_last_on_time.Add(lastSlipOn);
            D.SlippingOneFinger_last_off_time.Add(lastSlipOff);
        }
        else { ok = false; D.serial_out.Add(""); D.SlippingOneFinger_enabled.Add(false); D.SlippingOneFinger_last_on_time.Add(lastSlipOn); D.SlippingOneFinger_last_off_time.Add(lastSlipOff); }

        // Cylinder（世界位姿 & 高度）——按 GameObject.activeInHierarchy
        PushObjStateGO(
            D.Cylinder_enabled, D.Cylinder_last_on_time, D.Cylinder_last_off_time,
            D.Cylinder_world_pos_x, D.Cylinder_world_pos_y, D.Cylinder_world_pos_z,
            D.Cylinder_world_euler_x, D.Cylinder_world_euler_y, D.Cylinder_world_euler_z,
            cylinder_Obj, cylinder,
            lastCylOn, lastCylOff
        );
        float h = (cylinder != null) ? (cylinder.position.y - 0.75f) : 0f;
        D.Cylinder_height_from_init.Add(h);

        // CubeCenter（世界位姿 & 相对 cylinder）——按 GameObject.activeInHierarchy
        PushObjStateGO(
            D.CubeCenter_enabled, D.CubeCenter_last_on_time, D.CubeCenter_last_off_time,
            D.CubeCenter_world_pos_x, D.CubeCenter_world_pos_y, D.CubeCenter_world_pos_z,
            D.CubeCenter_world_euler_x, D.CubeCenter_world_euler_y, D.CubeCenter_world_euler_z,
            cubeCenter_Obj, cubeCenter,
            lastCubeOn, lastCubeOff
        );
        PushRelPos(cubeCenter, cylinder, D.CubeCenter_pos_rel_cylinder_x, D.CubeCenter_pos_rel_cylinder_y, D.CubeCenter_pos_rel_cylinder_z);
        D.CubeCenter_angle_rel_cylinder.Add(AngleXZ(cubeCenter, cylinder));

        // 五指可视化 CUBEs（现在也用真正边沿 last_on/off）
        PushFingerBlockGO(
            fingerDA_Obj, fingerDA,
            D.FingerDA_enabled, D.FingerDA_last_on_time, D.FingerDA_last_off_time,
            D.FingerDA_world_pos_x, D.FingerDA_world_pos_y, D.FingerDA_world_pos_z,
            D.FingerDA_world_euler_x, D.FingerDA_world_euler_y, D.FingerDA_world_euler_z,
            D.FingerDA_pos_rel_cylinder_x, D.FingerDA_pos_rel_cylinder_y, D.FingerDA_pos_rel_cylinder_z,
            D.FingerDA_angle_rel_cylinder,
            lastFingerDAOn, lastFingerDAOff
        );
        PushFingerBlockGO(
            fingerSHI_Obj, fingerSHI,
            D.FingerSHI_enabled, D.FingerSHI_last_on_time, D.FingerSHI_last_off_time,
            D.FingerSHI_world_pos_x, D.FingerSHI_world_pos_y, D.FingerSHI_world_pos_z,
            D.FingerSHI_world_euler_x, D.FingerSHI_world_euler_y, D.FingerSHI_world_euler_z,
            D.FingerSHI_pos_rel_cylinder_x, D.FingerSHI_pos_rel_cylinder_y, D.FingerSHI_pos_rel_cylinder_z,
            D.FingerSHI_angle_rel_cylinder,
            lastFingerSHIOn, lastFingerSHIOff
        );
        PushFingerBlockGO(
            fingerZHONG_Obj, fingerZHONG,
            D.FingerZHONG_enabled, D.FingerZHONG_last_on_time, D.FingerZHONG_last_off_time,
            D.FingerZHONG_world_pos_x, D.FingerZHONG_world_pos_y, D.FingerZHONG_world_pos_z,
            D.FingerZHONG_world_euler_x, D.FingerZHONG_world_euler_y, D.FingerZHONG_world_euler_z,
            D.FingerZHONG_pos_rel_cylinder_x, D.FingerZHONG_pos_rel_cylinder_y, D.FingerZHONG_pos_rel_cylinder_z,
            D.FingerZHONG_angle_rel_cylinder,
            lastFingerZHONGOn, lastFingerZHONGOff
        );
        PushFingerBlockGO(
            fingerWU_Obj, fingerWU,
            D.FingerWU_enabled, D.FingerWU_last_on_time, D.FingerWU_last_off_time,
            D.FingerWU_world_pos_x, D.FingerWU_world_pos_y, D.FingerWU_world_pos_z,
            D.FingerWU_world_euler_x, D.FingerWU_world_euler_y, D.FingerWU_world_euler_z,
            D.FingerWU_pos_rel_cylinder_x, D.FingerWU_pos_rel_cylinder_y, D.FingerWU_pos_rel_cylinder_z,
            D.FingerWU_angle_rel_cylinder,
            lastFingerWUOn, lastFingerWUOff
        );
        PushFingerBlockGO(
            fingerXIAO_Obj, fingerXIAO,
            D.FingerXIAO_enabled, D.FingerXIAO_last_on_time, D.FingerXIAO_last_off_time,
            D.FingerXIAO_world_pos_x, D.FingerXIAO_world_pos_y, D.FingerXIAO_world_pos_z,
            D.FingerXIAO_world_euler_x, D.FingerXIAO_world_euler_y, D.FingerXIAO_world_euler_z,
            D.FingerXIAO_pos_rel_cylinder_x, D.FingerXIAO_pos_rel_cylinder_y, D.FingerXIAO_pos_rel_cylinder_z,
            D.FingerXIAO_angle_rel_cylinder,
            lastFingerXIAOOn, lastFingerXIAOOff
        );

        // HandCenter（世界）
        PushWorldPose(handCenter,
            D.HandCenter_world_pos_x, D.HandCenter_world_pos_y, D.HandCenter_world_pos_z,
            D.HandCenter_world_euler_x, D.HandCenter_world_euler_y, D.HandCenter_world_euler_z
        );

        // Wrist（世界）
        PushWorldPose(rWristPalm,
            D.R_Wrist_Palm_world_pos_x, D.R_Wrist_Palm_world_pos_y, D.R_Wrist_Palm_world_pos_z,
            D.R_Wrist_Palm_world_euler_x, D.R_Wrist_Palm_world_euler_y, D.R_Wrist_Palm_world_euler_z
        );

        // 五指骨骼（相对 handCenter）
        PushHandPoint(
            rThumbTip,
            D.R_ThumbTip_world_pos_x, D.R_ThumbTip_world_pos_y, D.R_ThumbTip_world_pos_z,
            D.R_ThumbTip_world_euler_x, D.R_ThumbTip_world_euler_y, D.R_ThumbTip_world_euler_z,
            D.R_ThumbTip_pos_rel_hand_x, D.R_ThumbTip_pos_rel_hand_y, D.R_ThumbTip_pos_rel_hand_z,
            D.R_ThumbTip_angle_rel_hand
        );
        PushHandPoint(
            rIndexTip,
            D.R_IndexTip_world_pos_x, D.R_IndexTip_world_pos_y, D.R_IndexTip_world_pos_z,
            D.R_IndexTip_world_euler_x, D.R_IndexTip_world_euler_y, D.R_IndexTip_world_euler_z,
            D.R_IndexTip_pos_rel_hand_x, D.R_IndexTip_pos_rel_hand_y, D.R_IndexTip_pos_rel_hand_z,
            D.R_IndexTip_angle_rel_hand
        );
        PushHandPoint(
            rMiddleTip,
            D.R_MiddleTip_world_pos_x, D.R_MiddleTip_world_pos_y, D.R_MiddleTip_world_pos_z,
            D.R_MiddleTip_world_euler_x, D.R_MiddleTip_world_euler_y, D.R_MiddleTip_world_euler_z,
            D.R_MiddleTip_pos_rel_hand_x, D.R_MiddleTip_pos_rel_hand_y, D.R_MiddleTip_pos_rel_hand_z,
            D.R_MiddleTip_angle_rel_hand
        );
        PushHandPoint(
            rRingTip,
            D.R_RingTip_world_pos_x, D.R_RingTip_world_pos_y, D.R_RingTip_world_pos_z,
            D.R_RingTip_world_euler_x, D.R_RingTip_world_euler_y, D.R_RingTip_world_euler_z,
            D.R_RingTip_pos_rel_hand_x, D.R_RingTip_pos_rel_hand_y, D.R_RingTip_pos_rel_hand_z,
            D.R_RingTip_angle_rel_hand
        );
        PushHandPoint(
            rLittleTip,
            D.R_LittleTip_world_pos_x, D.R_LittleTip_world_pos_y, D.R_LittleTip_world_pos_z,
            D.R_LittleTip_world_euler_x, D.R_LittleTip_world_euler_y, D.R_LittleTip_world_euler_z,
            D.R_LittleTip_pos_rel_hand_x, D.R_LittleTip_pos_rel_hand_y, D.R_LittleTip_pos_rel_hand_z,
            D.R_LittleTip_angle_rel_hand
        );

        // Follow / UpDown（脚本 enabled）
        D.Follow_enabled.Add(followHandTracking != null && followHandTracking.enabled);
        D.Follow_last_on_time.Add(lastFollowOn);
        D.Follow_last_off_time.Add(lastFollowOff);

        D.UpDown_enabled.Add(updownHandTracking != null && updownHandTracking.enabled);
        D.UpDown_last_on_time.Add(lastUpDownOn);
        D.UpDown_last_off_time.Add(lastUpDownOff);

        // 桌面接触
        D.Cylinder_on_table.Add(cylinderOnTable);
        D.Cylinder_touch_time.Add(lastTouchTable);
        D.Cylinder_leave_time.Add(lastLeaveTable);

        // —— 阈值边沿检测（每次进入/离开都更新时间）——
        bool inThresh = GetThresholdInside();
        if (inThresh != prevInThreshold)
        {
            if (inThresh) lastThreshEnter = t;
            else lastThreshExit = t;
            prevInThreshold = inThresh;
        }
        D.Cylinder_in_threshold.Add(inThresh);
        D.Threshold_last_enter_time.Add(lastThreshEnter);
        D.Threshold_last_exit_time.Add(lastThreshExit);

        // —— Goal 区域 above 的进入/离开边沿（每次都记录）——
        bool above = (goal != null) ? goal.isCylinderAboveAndClear : false;
        if (above != prevGoalAbove)
        {
            if (above) lastGoalEnter = t;
            else lastGoalLeave = t;
            prevGoalAbove = above;
        }
        D.Goal_above.Add(above);
        D.Goal_enter_time.Add(lastGoalEnter);
        D.Goal_leave_time.Add(lastGoalLeave);

        // —— Goal 结果事件（脉冲：当帧为 true，随后复位）——
        D.Goal_OnHoldSucceeded.Add(goalSucceededPulse); D.Goal_OnHoldSucceeded_time.Add(lastGoalSucceeded);
        D.Goal_OnHoldInterrupted.Add(goalInterruptedPulse); D.Goal_OnHoldInterrupted_time.Add(lastGoalInterrupted);
        D.Goal_OnHoldFailedTilt.Add(goalFailedTiltPulse); D.Goal_OnHoldFailedTilt_time.Add(lastGoalFailedTilt);
        goalSucceededPulse = goalInterruptedPulse = goalFailedTiltPulse = false;

        D.sample_ok.Add(ok ? 1 : 0);

        // 模式/COM/attempt 变化进入时间
        TrackModeComAttemptEnterTimes();
    }

    // ―― 事件回调 ―― //
    private void HandleTouchTable() { cylinderOnTable = true; lastTouchTable = Now(); }
    private void HandleLeaveTable() { cylinderOnTable = false; lastLeaveTable = Now(); }

    // Goal 结果事件：设置脉冲并更新时间戳
    private void OnGoalSucceeded(float tHold) { goalSucceededPulse = true; lastGoalSucceeded = Now(); }
    private void OnGoalInterrupted(float tHold) { goalInterruptedPulse = true; lastGoalInterrupted = Now(); }
    private void OnGoalFailedTilt(float tHold, float tiltDeg) { goalFailedTiltPulse = true; lastGoalFailedTilt = Now(); }

    // ―― 状态/进入时间跟踪（模式/COM/attempt）―― //
    private void TrackModeComAttemptEnterTimes()
    {
        string modeNow = (modeSwitch != null) ? modeSwitch.currentMode.ToString() : "";
        if (modeNow != prevModeStr) { lastModeEnter = Now(); prevModeStr = modeNow; }

        int comNow = (comController != null) ? comController.selectedCOMIndex : int.MinValue;
        if (comNow != prevCom) { lastComEnter = Now(); prevCom = comNow; }

        int attemptNow = (graspHT != null) ? graspHT.AttemptCount : int.MinValue;
        if (attemptNow != prevAttempt) { lastAttemptEnter = Now(); prevAttempt = attemptNow; }
    }

    // ―― 保存 ―― //
    private void SaveNow()
    {
        string dir = Path.Combine(rootPath, "Data");
        Directory.CreateDirectory(dir);
        string fname = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{fileIndex++}.json";
        string path = Path.Combine(dir, fname);

        var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
        string json = JsonConvert.SerializeObject(D, settings);

        if (!threadedWrite)
        {
            File.WriteAllText(path, json);
            Debug.Log($"[UnifiedDataLogger] Saved: {path}");
        }
        else
        {
            string capturedPath = path; string capturedJson = json;
            new Thread(() => File.WriteAllText(capturedPath, capturedJson)).Start();
            Debug.Log($"[UnifiedDataLogger] Saving (threaded): {path}");
        }
        // 如需“分段一文件”，可在此清空下一段
        // ClearDataForNextSegment();
    }

    // ―― 段初始化（首段与清空后复用）――
    private void InitSegmentState()
    {
        startTime = Time.time;

        // 初始化脚本 enabled 的前态
        prevFollow = followHandTracking != null && followHandTracking.enabled;
        prevUpDown = updownHandTracking != null && updownHandTracking.enabled;
        prevSlip = slipping != null && slipping.enabled;

        lastFollowOn = prevFollow ? 0f : -1f; lastFollowOff = prevFollow ? -1f : 0f;
        lastUpDownOn = prevUpDown ? 0f : -1f; lastUpDownOff = prevUpDown ? -1f : 0f;
        lastSlipOn = prevSlip ? 0f : -1f; lastSlipOff = prevSlip ? -1f : 0f;

        // 初始化 GameObject active 的前态：Cylinder / CubeCenter / 五指方块
        prevCyl = cylinder_Obj != null && cylinder_Obj.activeInHierarchy;
        prevCube = cubeCenter_Obj != null && cubeCenter_Obj.activeInHierarchy;

        prevFingerDA = fingerDA_Obj != null && fingerDA_Obj.activeInHierarchy;
        prevFingerSHI = fingerSHI_Obj != null && fingerSHI_Obj.activeInHierarchy;
        prevFingerZHONG = fingerZHONG_Obj != null && fingerZHONG_Obj.activeInHierarchy;
        prevFingerWU = fingerWU_Obj != null && fingerWU_Obj.activeInHierarchy;
        prevFingerXIAO = fingerXIAO_Obj != null && fingerXIAO_Obj.activeInHierarchy;

        // 基于初始状态设定 last_on/off（on→last_on=0；off→last_off=0）
        lastCylOn = prevCyl ? 0f : -1f; lastCylOff = prevCyl ? -1f : 0f;
        lastCubeOn = prevCube ? 0f : -1f; lastCubeOff = prevCube ? -1f : 0f;

        lastFingerDAOn = prevFingerDA ? 0f : -1f; lastFingerDAOff = prevFingerDA ? -1f : 0f;
        lastFingerSHIOn = prevFingerSHI ? 0f : -1f; lastFingerSHIOff = prevFingerSHI ? -1f : 0f;
        lastFingerZHONGOn = prevFingerZHONG ? 0f : -1f; lastFingerZHONGOff = prevFingerZHONG ? -1f : 0f;
        lastFingerWUOn = prevFingerWU ? 0f : -1f; lastFingerWUOff = prevFingerWU ? -1f : 0f;
        lastFingerXIAOOn = prevFingerXIAO ? 0f : -1f; lastFingerXIAOOff = prevFingerXIAO ? -1f : 0f;

        lastModeEnter = 0f; lastComEnter = 0f; lastAttemptEnter = 0f;

        prevModeStr = (modeSwitch != null) ? modeSwitch.currentMode.ToString() : "";
        prevCom = (comController != null) ? comController.selectedCOMIndex : int.MinValue;
        prevAttempt = (graspHT != null) ? graspHT.AttemptCount : int.MinValue;

        // 状态清零
        cylinderOnTable = false; lastTouchTable = -1f; lastLeaveTable = -1f;

        // 初始化 Threshold/Goal 的前一帧状态
        prevInThreshold = GetThresholdInside();
        lastThreshEnter = -1f; lastThreshExit = -1f;

        prevGoalAbove = (goal != null) ? goal.isCylinderAboveAndClear : false;
        lastGoalEnter = -1f; lastGoalLeave = -1f;

        // 结果事件脉冲复位
        goalSucceededPulse = goalInterruptedPulse = goalFailedTiltPulse = false;
        lastGoalSucceeded = lastGoalInterrupted = lastGoalFailedTilt = -1f;

        // OutOfAttempt 初始化
        outOfAttemptPulse = false;
        lastOutOfAttemptTime = -1f;
        reachedAttemptLimit = false;

        // ★ 初始化 grasp_state 变更追踪
        prevGraspStateStr = GetGraspState(graspHT);
        lastGraspStateChange = 0f;   // 段开始视作“基线时间”
    }

    private void ClearDataForNextSegment()
    {
        D = new FlatData();
        InitSegmentState(); // 清空后完整重置基线，避免虚假边沿
    }

    // ―― 工具函数 ―― //
    private float Now() => Time.time - startTime;

    // 行为脚本（Behaviour.enabled）边沿监听
    private void EdgeWatch(ref bool prev, Behaviour beh, ref float lastOn, ref float lastOff, float tNow)
    {
        bool cur = (beh != null) && beh.enabled;
        if (cur != prev) { if (cur) lastOn = tNow; else lastOff = tNow; prev = cur; }
    }
    // GameObject（activeInHierarchy）边沿监听
    private void EdgeWatch(ref bool prev, GameObject go, ref float lastOn, ref float lastOff, float tNow)
    {
        bool cur = (go != null) && go.activeInHierarchy;
        if (cur != prev) { if (cur) lastOn = tNow; else lastOff = tNow; prev = cur; }
    }

    // 世界位姿写入
    private void PushWorldPose(Transform tr,
        List<float> pos_x, List<float> pos_y, List<float> pos_z,
        List<float> eul_x, List<float> eul_y, List<float> eul_z)
    {
        if (tr != null)
        {
            var p = tr.position; var e = tr.rotation.eulerAngles;
            pos_x.Add(p.x); pos_y.Add(p.y); pos_z.Add(p.z);
            eul_x.Add(e.x); eul_y.Add(e.y); eul_z.Add(e.z);
        }
        else
        {
            pos_x.Add(0f); pos_y.Add(0f); pos_z.Add(0f);
            eul_x.Add(0f); eul_y.Add(0f); eul_z.Add(0f);
        }
    }

    // Cylinder/CubeCenter：按 GameObject.activeInHierarchy + 记录 lastOn/Off（来自边沿监听）
    private void PushObjStateGO(
        List<bool> en, List<float> ton, List<float> toff,
        List<float> pos_x, List<float> pos_y, List<float> pos_z,
        List<float> eul_x, List<float> eul_y, List<float> eul_z,
        GameObject go, Transform tr,
        float lastOn, float lastOff)
    {
        bool active = (go != null) && go.activeInHierarchy;
        en.Add(active);
        ton.Add(lastOn);
        toff.Add(lastOff);
        PushWorldPose(tr, pos_x, pos_y, pos_z, eul_x, eul_y, eul_z);
    }

    private void PushRelPos(Transform a, Transform refTr,
        List<float> rx, List<float> ry, List<float> rz)
    {
        if (a != null && refTr != null)
        {
            Vector3 r = refTr.InverseTransformPoint(a.position);
            rx.Add(r.x); ry.Add(r.y); rz.Add(r.z);
        }
        else { rx.Add(0f); ry.Add(0f); rz.Add(0f); }
    }

    private float AngleXZ(Transform a, Transform center)
    {
        if (a == null || center == null) return 0f;
        Vector3 v = a.position - center.position; v.y = 0f;
        if (v.sqrMagnitude < 1e-8f) return 0f;
        return Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg; // 以 X 轴为 0°
    }

    // 指尖可视化 Cube：按 GameObject.activeInHierarchy + 真正边沿 last_on/off
    private void PushFingerBlockGO(
        GameObject go, Transform tr,
        List<bool> en, List<float> ton, List<float> toff,
        List<float> wpos_x, List<float> wpos_y, List<float> wpos_z,
        List<float> weul_x, List<float> weul_y, List<float> weul_z,
        List<float> rel_x, List<float> rel_y, List<float> rel_z,
        List<float> ang,
        float lastOn, float lastOff)
    {
        bool active = (go != null) && go.activeInHierarchy;
        en.Add(active);
        ton.Add(lastOn);
        toff.Add(lastOff);
        PushWorldPose(tr, wpos_x, wpos_y, wpos_z, weul_x, weul_y, weul_z);
        PushRelPos(tr, cylinder, rel_x, rel_y, rel_z);
        ang.Add(AngleXZ(tr, cylinder));
    }

    private void PushHandPoint(
        Transform tr,
        List<float> wpos_x, List<float> wpos_y, List<float> wpos_z,
        List<float> weul_x, List<float> weul_y, List<float> weul_z,
        List<float> rel_x, List<float> rel_y, List<float> rel_z,
        List<float> ang)
    {
        PushWorldPose(tr, wpos_x, wpos_y, wpos_z, weul_x, weul_y, weul_z);
        if (tr != null && handCenter != null)
        {
            Vector3 r = handCenter.InverseTransformPoint(tr.position);
            rel_x.Add(r.x); rel_y.Add(r.y); rel_z.Add(r.z);
            ang.Add(AngleXZ(tr, handCenter));
        }
        else { rel_x.Add(0f); rel_y.Add(0f); rel_z.Add(0f); ang.Add(0f); }
    }

    private void FillZeros(params List<float>[] lists) { foreach (var L in lists) L.Add(0f); }

    private string GetGraspState(Grasp_HandTracking ght)
    {
        try
        {
            var handGO = (ght != null) ? ght.hand : null;
            if (handGO != null)
            {
                var pincher = handGO.GetComponent<PincherController>();
                if (pincher != null) return pincher.gripState.ToString();
            }
        }
        catch { }
        return "Unknown";
    }

    // —— 纯查询阈值内外，不做时间写入（避免重复逻辑）——
    private bool GetThresholdInside()
    {
        if (thresholdLock == null || cylinder == null) return false;
        var cylCol = cylinder.GetComponent<Collider>();
        if (cylCol == null) return false;

        var hits = Physics.OverlapBox(cylCol.bounds.center, cylCol.bounds.extents, cylinder.rotation,
                                      ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            if (h.CompareTag("Threshold")) return true;
        }
        return false;
    }
}
