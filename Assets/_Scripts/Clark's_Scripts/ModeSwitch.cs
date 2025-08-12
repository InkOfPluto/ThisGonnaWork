using UnityEngine;

public class ModeSwitch : MonoBehaviour
{
    public enum ExperimentMode
    {
        Visual,
        Haptic,
        VisualHaptic
    }

    [Header("Instructions | 操作说明")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
        "Inspector 设置 Participant ID（1-30）决定模式序列；播放后按 Q 循环切换模式。\n" +
        "与 CenterOfMassController 联动：切到新模式时重置到 COM_0，恢复显示对象与Counting文本，关闭ChangingMode文本。";

    [Header("Mode Settings | 模式设定（只读展示）")]
    public ExperimentMode currentMode = ExperimentMode.Visual;

    [Range(1, 30)]
    public int participantID = 1;

    [Header("References | 引用组件")]
    public MonoBehaviour slippingScript;
    ExperimentSaveData_JSON saveJSON;

    [Header("CenterOfMass  | 控制器（用于模式切换时重置）")]
    public CenterOfMassController comController;

    private int initialParticipantID;
    private bool idLocked = false;
    private bool modeLocked = false;
    private ExperimentMode[] cachedSequence = null;
    private ExperimentMode runtimeMode;

    private void Start()
    {
        saveJSON = FindObjectOfType<ExperimentSaveData_JSON>();

        if (!TryGetSequenceByParticipant(participantID, out cachedSequence))
        {
            Debug.LogWarning("Participant ID out of range (1–30) | 实验者编号超出范围，应为 1-30");
            ApplyMode();
            return;
        }

        initialParticipantID = participantID;
        idLocked = true;

        runtimeMode = cachedSequence[0];
        currentMode = runtimeMode;
        modeLocked = true;

        ApplyMode();

        if (saveJSON != null)
        {
            saveJSON.SetParticipant(participantID.ToString());
            saveJSON.OnModeChanged(currentMode.ToString());
        }

        if (comController != null)
            comController.ResetForNewMode();
        else
            Debug.LogWarning("ModeSwitch: comController 未绑定，无法在模式切换时重置 COM/UI。");

        Debug.Log($"[ModeSwitch] Locked ID & Mode. Participant={initialParticipantID}, InitialMode={runtimeMode}");
    }

    private void Update()
    {
        if (idLocked && Application.isPlaying && participantID != initialParticipantID)
        {
            participantID = initialParticipantID;
            Debug.LogWarning($"Participant ID is locked during Play. Reverting to {initialParticipantID} | 播放中编号已锁定，已恢复为 {initialParticipantID}");
        }

        if (Application.isPlaying && Input.GetKeyDown(KeyCode.Q))
        {
            CycleModeLockedSequence();
        }
    }

    private void CycleModeLockedSequence()
    {
        if (cachedSequence == null || cachedSequence.Length == 0)
        {
            Debug.LogWarning("Sequence not initialized. Set a valid Participant ID (1–30) before Play.");
            return;
        }

        int currentIndex = System.Array.IndexOf(cachedSequence, runtimeMode);
        if (currentIndex < 0) currentIndex = -1;

        if (currentIndex >= cachedSequence.Length - 1)
        {
            Debug.Log("All modes completed. No further switching. | 所有模式已完成，不再切换。");
            return;
        }

        int nextIndex = currentIndex + 1;
        runtimeMode = cachedSequence[nextIndex];
        currentMode = runtimeMode;

        var logger = FindObjectOfType<ExperimentSaveData_JSON>();
        if (logger != null)
        {
            logger.OnModeChanged(currentMode.ToString());
        }

        ApplyMode();

        if (comController != null)
            comController.ResetForNewMode();

        Debug.Log("Mode switched to: " + runtimeMode + " | 当前模式已切换为：" + runtimeMode);
    }

    private bool TryGetSequenceByParticipant(int id, out ExperimentMode[] sequence)
    {
        if (id >= 1 && id <= 10)
        {
            sequence = new ExperimentMode[] { ExperimentMode.Visual, ExperimentMode.Haptic, ExperimentMode.VisualHaptic };
            return true;
        }
        if (id >= 11 && id <= 20)
        {
            sequence = new ExperimentMode[] { ExperimentMode.Haptic, ExperimentMode.VisualHaptic, ExperimentMode.Visual };
            return true;
        }
        if (id >= 21 && id <= 30)
        {
            sequence = new ExperimentMode[] { ExperimentMode.VisualHaptic, ExperimentMode.Visual, ExperimentMode.Haptic };
            return true;
        }

        sequence = null;
        return false;
    }

    private void ApplyMode()
    {
        if (slippingScript == null)
        {
            Debug.LogWarning("Bind Slipping script in Inspector | 请在 Inspector 中正确绑定！");
            return;
        }

        switch (runtimeMode)
        {
            case ExperimentMode.Visual:
                slippingScript.enabled = false;
                break;

            case ExperimentMode.Haptic:
                slippingScript.enabled = true;
                break;

            case ExperimentMode.VisualHaptic:
                slippingScript.enabled = true;
                break;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (TryGetSequenceByParticipant(participantID, out var seq))
            {
                var first = seq[0];
                if (currentMode != first)
                {
                    currentMode = first;
                }
                runtimeMode = first;
            }
            else
            {
                Debug.LogWarning("Participant ID out of range (1–30) | 实验者编号超出范围，应为 1-30");
            }
        }
        else
        {
            if (idLocked && participantID != initialParticipantID)
            {
                participantID = initialParticipantID;
            }
            if (modeLocked && currentMode != runtimeMode)
            {
                currentMode = runtimeMode;
                Debug.LogWarning("Mode is locked during Play. Use Q to switch. | 播放中模式已锁定，请用 Q 键切换。");
            }
        }

        ApplyMode();
    }
#endif
}
