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
/// 1) 除 Follow/UpDown/SlippingOneFinger 继续按脚本 Behaviour.enabled 记录外，
///    其余原“*_Beh”改为 GameObject，并按 GameObject.activeInHierarchy 记录激活/失活与 on/off 时间。
/// 2) 其它逻辑保持不变。
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

    // —— 以下改为 GameObject：按 activeInHierarchy 记录 ——
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

        public List<int> attempt_index = new();
        public List<float> attempt_enter_time = new();
        public List<string> grasp_state = new();

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
        public List<float> FingerZHONG_world_pos_x = new(); public List<float> FingerZHONG_world_pos_y = new(); public List<float> FingerZHONG_world_pos_z = new();
        public List<float> FingerZHONG_world_euler_x = new(); public List<float> FingerZHONG_world_euler_y = new(); public List<float> FingerZHONG_world_euler_z = new();
        public List<float> FingerZHONG_pos_rel_cylinder_x = new(); public List<float> FingerZHONG_pos_rel_cylinder_y = new(); public List<float> FingerZHONG_pos_rel_cylinder_z = new();
        public List<float> FingerZHONG_angle_rel_cylinder = new();

        public List<bool> FingerWU_enabled = new(); public List<float> FingerWU_last_on_time = new(); public List<float> FingerWU_last_off_time = new();
        public List<float> FingerWU_world_pos_x = new(); public List<float> FingerWU_world_pos_y = new(); public List<float> FingerWU_world_pos_z = new();
        public List<float> FingerWU_world_euler_x = new(); public List<float> FingerWU_world_euler_y = new(); public List<float> FingerWU_world_euler_z = new();
        public List<float> FingerWU_pos_rel_cylinder_x = new(); public List<float> FingerWU_pos_rel_cylinder_y = new(); public List<float> FingerWU_pos_rel_cylinder_z = new();
        public List<float> FingerWU_angle_rel_cylinder = new();

        public List<bool> FingerXIAO_enabled = new(); public List<float> FingerXIAO_last_on_time = new(); public List<float> FingerXIAO_last_off_time = new();
        public List<float> FingerXIAO_world_pos_x = new(); public List<float> FingerXIAO_world_pos_y = new(); public List<float> FingerXIAO_world_pos_z = new();
        public List<float> FingerXIAO_world_euler_x = new(); public List<float> FingerXIAO_world_euler_y = new(); public List<float> FingerXIAO_world_euler_z = new();
        public List<float> FingerXIAO_pos_rel_cylinder_x = new(); public List<float> FingerXIAO_pos_rel_cylinder_y = new(); public List<float> FingerXIAO_pos_rel_cylinder_z = new();
        public List<float> FingerXIAO_angle_rel_cylinder = new();

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

        // Goal 区域与事件
        public List<bool> Goal_above = new(); public List<float> Goal_enter_time = new(); public List<float> Goal_leave_time = new();
        public List<bool> Goal_OnHoldSucceeded = new(); public List<float> Goal_OnHoldSucceeded_time = new();
        public List<bool> Goal_OnHoldInterrupted = new(); public List<float> Goal_OnHoldInterrupted_time = new();
        public List<bool> Goal_OnHoldFailedTilt = new(); public List<float> Goal_OnHoldFailedTilt_time = new();

        // 采样完整性（调试用）
        public List<int> sample_ok = new();
    }

    private FlatData D = new();

    private float startTime;
    private float lastModeEnter = 0f, lastComEnter = 0f, lastAttemptEnter = 0f;

    // 边沿检测：记录开关的 last on/off 时间
    private float lastSlipOn = -1f, lastSlipOff = -1f;
    private float lastFollowOn = -1f, lastFollowOff = -1f, lastUpDownOn = -1f, lastUpDownOff = -1f;
    private float lastCylOn = -1f, lastCylOff = -1f, lastCubeOn = -1f, lastCubeOff = -1f;
    private bool prevFollow = false, prevUpDown = false, prevSlip = false, prevCyl = false, prevCube = false;

    // 桌面接触
    private bool cylinderOnTable = false; private float lastTouchTable = -1f, lastLeaveTable = -1f;

    // Threshold 进入/离开时间（边沿近似）
    private float lastThreshEnter = -1f, lastThreshExit = -1f;

    // Goal 区域与事件
    private float lastGoalEnter = -1f, lastGoalLeave = -1f;
    private bool goalSucceeded = false, goalInterrupted = false, goalFailedTilt = false;
    private float lastGoalSucceeded = -1f, lastGoalInterrupted = -1f, lastGoalFailedTilt = -1f;

    private string prevModeStr = "";
    private int prevCom = int.MinValue, prevAttempt = int.MinValue;

    private int fileIndex = 0;

    private void OnEnable()
    {
        // 圆柱体-桌面接触（静态事件）
        CylinderTableContact.OnCylinderTouchTable += HandleTouchTable;
        CylinderTableContact.OnCylinderLeaveTable += HandleLeaveTable;

        // Goal 事件
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

        // 监视开关边沿：
        EdgeWatch(ref prevFollow, followHandTracking, ref lastFollowOn, ref lastFollowOff, t);        // 脚本 enabled
        EdgeWatch(ref prevUpDown, updownHandTracking, ref lastUpDownOn, ref lastUpDownOff, t);        // 脚本 enabled
        EdgeWatch(ref prevSlip, slipping, ref lastSlipOn, ref lastSlipOff, t);                        // 脚本 enabled
        EdgeWatch(ref prevCyl, cylinder_Obj, ref lastCylOn, ref lastCylOff, t);                       // 物体 active
        EdgeWatch(ref prevCube, cubeCenter_Obj, ref lastCubeOn, ref lastCubeOff, t);                  // 物体 active

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

        int attempt = (graspHT != null) ? graspHT.AttemptCount : -1;
        D.attempt_index.Add(attempt);
        D.attempt_enter_time.Add(lastAttemptEnter);
        D.grasp_state.Add(GetGraspState(graspHT));

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

        // 五指可视化 CUBEs（enabled 改为按 GameObject.activeInHierarchy；last_on/off 沿用旧逻辑：写 -1）
        PushFingerBlockGO(
            fingerDA_Obj, fingerDA,
            D.FingerDA_enabled, D.FingerDA_last_on_time, D.FingerDA_last_off_time,
            D.FingerDA_world_pos_x, D.FingerDA_world_pos_y, D.FingerDA_world_pos_z,
            D.FingerDA_world_euler_x, D.FingerDA_world_euler_y, D.FingerDA_world_euler_z,
            D.FingerDA_pos_rel_cylinder_x, D.FingerDA_pos_rel_cylinder_y, D.FingerDA_pos_rel_cylinder_z,
            D.FingerDA_angle_rel_cylinder
        );
        PushFingerBlockGO(
            fingerSHI_Obj, fingerSHI,
            D.FingerSHI_enabled, D.FingerSHI_last_on_time, D.FingerSHI_last_off_time,
            D.FingerSHI_world_pos_x, D.FingerSHI_world_pos_y, D.FingerSHI_world_pos_z,
            D.FingerSHI_world_euler_x, D.FingerSHI_world_euler_y, D.FingerSHI_world_euler_z,
            D.FingerSHI_pos_rel_cylinder_x, D.FingerSHI_pos_rel_cylinder_y, D.FingerSHI_pos_rel_cylinder_z,
            D.FingerSHI_angle_rel_cylinder
        );
        PushFingerBlockGO(
            fingerZHONG_Obj, fingerZHONG,
            D.FingerZHONG_enabled, D.FingerZHONG_last_on_time, D.FingerZHONG_last_off_time,
            D.FingerZHONG_world_pos_x, D.FingerZHONG_world_pos_y, D.FingerZHONG_world_pos_z,
            D.FingerZHONG_world_euler_x, D.FingerZHONG_world_euler_y, D.FingerZHONG_world_euler_z,
            D.FingerZHONG_pos_rel_cylinder_x, D.FingerZHONG_pos_rel_cylinder_y, D.FingerZHONG_pos_rel_cylinder_z,
            D.FingerZHONG_angle_rel_cylinder
        );
        PushFingerBlockGO(
            fingerWU_Obj, fingerWU,
            D.FingerWU_enabled, D.FingerWU_last_on_time, D.FingerWU_last_off_time,
            D.FingerWU_world_pos_x, D.FingerWU_world_pos_y, D.FingerWU_world_pos_z,
            D.FingerWU_world_euler_x, D.FingerWU_world_euler_y, D.FingerWU_world_euler_z,
            D.FingerWU_pos_rel_cylinder_x, D.FingerWU_pos_rel_cylinder_y, D.FingerWU_pos_rel_cylinder_z,
            D.FingerWU_angle_rel_cylinder
        );
        PushFingerBlockGO(
            fingerXIAO_Obj, fingerXIAO,
            D.FingerXIAO_enabled, D.FingerXIAO_last_on_time, D.FingerXIAO_last_off_time,
            D.FingerXIAO_world_pos_x, D.FingerXIAO_world_pos_y, D.FingerXIAO_world_pos_z,
            D.FingerXIAO_world_euler_x, D.FingerXIAO_world_euler_y, D.FingerXIAO_world_euler_z,
            D.FingerXIAO_pos_rel_cylinder_x, D.FingerXIAO_pos_rel_cylinder_y, D.FingerXIAO_pos_rel_cylinder_z,
            D.FingerXIAO_angle_rel_cylinder
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

        // 桌面接触 & Threshold
        D.Cylinder_on_table.Add(cylinderOnTable);
        D.Cylinder_touch_time.Add(lastTouchTable);
        D.Cylinder_leave_time.Add(lastLeaveTable);

        bool inThresh = IsInThreshold();
        D.Cylinder_in_threshold.Add(inThresh);
        D.Threshold_last_enter_time.Add(lastThreshEnter);
        D.Threshold_last_exit_time.Add(lastThreshExit);

        // Goal 区域与事件
        bool above = (goal != null) ? goal.isCylinderAboveAndClear : false;
        D.Goal_above.Add(above);
        D.Goal_enter_time.Add(lastGoalEnter);
        D.Goal_leave_time.Add(lastGoalLeave);
        D.Goal_OnHoldSucceeded.Add(goalSucceeded); D.Goal_OnHoldSucceeded_time.Add(lastGoalSucceeded);
        D.Goal_OnHoldInterrupted.Add(goalInterrupted); D.Goal_OnHoldInterrupted_time.Add(lastGoalInterrupted);
        D.Goal_OnHoldFailedTilt.Add(goalFailedTilt); D.Goal_OnHoldFailedTilt_time.Add(lastGoalFailedTilt);

        D.sample_ok.Add(ok ? 1 : 0);

        // 进入时间的更新（模式/COM/attempt 变化边沿）
        TrackModeComAttemptEnterTimes();
    }

    // ―― 事件回调 ―― //
    private void HandleTouchTable() { cylinderOnTable = true; lastTouchTable = Now(); }
    private void HandleLeaveTable() { cylinderOnTable = false; lastLeaveTable = Now(); }
    private void OnGoalSucceeded(float tHold) { goalSucceeded = true; lastGoalSucceeded = Now(); }
    private void OnGoalInterrupted(float tHold) { goalInterrupted = true; lastGoalInterrupted = Now(); }
    private void OnGoalFailedTilt(float tHold, float tiltDeg) { goalFailedTilt = true; lastGoalFailedTilt = Now(); }

    // ―― 状态/进入时间跟踪 ―― //
    private void TrackModeComAttemptEnterTimes()
    {
        string modeNow = (modeSwitch != null) ? modeSwitch.currentMode.ToString() : "";
        if (modeNow != prevModeStr) { lastModeEnter = Now(); prevModeStr = modeNow; }

        int comNow = (comController != null) ? comController.selectedCOMIndex : int.MinValue;
        if (comNow != prevCom) { lastComEnter = Now(); prevCom = comNow; }

        int attemptNow = (graspHT != null) ? graspHT.AttemptCount : int.MinValue;
        if (attemptNow != prevAttempt) { lastAttemptEnter = Now(); prevAttempt = attemptNow; }

        // Goal 区域进入/离开粗略边沿
        bool above = (goal != null) ? goal.isCylinderAboveAndClear : false;
        if (above && lastGoalEnter < 0f) lastGoalEnter = Now();
        if (!above && lastGoalLeave < 0f) lastGoalLeave = Now();
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

        // 初始化 enabled/active 状态与时间基线
        prevFollow = followHandTracking != null && followHandTracking.enabled;
        prevUpDown = updownHandTracking != null && updownHandTracking.enabled;
        prevSlip = slipping != null && slipping.enabled;
        prevCyl = cylinder_Obj != null && cylinder_Obj.activeInHierarchy;
        prevCube = cubeCenter_Obj != null && cubeCenter_Obj.activeInHierarchy;

        lastFollowOn = prevFollow ? 0f : -1f; lastFollowOff = prevFollow ? -1f : 0f;
        lastUpDownOn = prevUpDown ? 0f : -1f; lastUpDownOff = prevUpDown ? -1f : 0f;
        lastSlipOn = prevSlip ? 0f : -1f; lastSlipOff = prevSlip ? -1f : 0f;
        lastCylOn = prevCyl ? 0f : -1f; lastCylOff = prevCyl ? -1f : 0f;
        lastCubeOn = prevCube ? 0f : -1f; lastCubeOff = prevCube ? -1f : 0f;

        lastModeEnter = 0f; lastComEnter = 0f; lastAttemptEnter = 0f;

        prevModeStr = (modeSwitch != null) ? modeSwitch.currentMode.ToString() : "";
        prevCom = (comController != null) ? comController.selectedCOMIndex : int.MinValue;
        prevAttempt = (graspHT != null) ? graspHT.AttemptCount : int.MinValue;

        // 状态清零
        cylinderOnTable = false; lastTouchTable = -1f; lastLeaveTable = -1f;
        lastThreshEnter = -1f; lastThreshExit = -1f;
        lastGoalEnter = -1f; lastGoalLeave = -1f;
        goalSucceeded = goalInterrupted = goalFailedTilt = false;
        lastGoalSucceeded = lastGoalInterrupted = lastGoalFailedTilt = -1f;
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

    // 指尖可视化 Cube：按 GameObject.activeInHierarchy；last_on/off 维持 -1（和旧版一致）
    private void PushFingerBlockGO(
        GameObject go, Transform tr,
        List<bool> en, List<float> ton, List<float> toff,
        List<float> wpos_x, List<float> wpos_y, List<float> wpos_z,
        List<float> weul_x, List<float> weul_y, List<float> weul_z,
        List<float> rel_x, List<float> rel_y, List<float> rel_z,
        List<float> ang)
    {
        bool active = (go != null) && go.activeInHierarchy;
        en.Add(active);
        ton.Add(-1f); // 旧逻辑未做边沿跟踪
        toff.Add(-1f);
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

    // ―― 阈值区域粗略检测（可选，若没有更好事件）――
    private bool IsInThreshold()
    {
        if (thresholdLock == null || cylinder == null) return false;
        var cylCol = cylinder.GetComponent<Collider>();
        if (cylCol == null) return false;

        var hits = Physics.OverlapBox(cylCol.bounds.center, cylCol.bounds.extents, cylinder.rotation,
                                      ~0, QueryTriggerInteraction.Collide);
        bool inside = false;
        foreach (var h in hits) { if (h.CompareTag("Threshold")) { inside = true; break; } }

        if (inside && lastThreshEnter < 0f) lastThreshEnter = Now();
        if (!inside && lastThreshExit < 0f) lastThreshExit = Now();
        return inside;
    }
}
