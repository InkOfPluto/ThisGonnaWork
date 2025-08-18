using UnityEngine;
using TMPro;

public class CountingTextUpdater : MonoBehaviour
{
    [Header("操作说明 | Instructions")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
        "功能：将模板中的占位符替换后显示在 TextMeshProUGUI 上\n" +
        "占位符：{mode}、{currentCOM}、{totalCOMs}、{attempts}\n" +
        "Inspector：绑定 ModeSwitch、CenterOfMassController、Grasp_HandTracking、TextMeshProUGUI\n" +
        "运行时：每帧根据 modeSwitch.currentMode、comController.comProgressCounter 和 attemptCounter.AttemptCount 更新文本\n";

    [Header("References | 引用组件")]
    public ModeSwitch modeSwitch;
    public CenterOfMassController comController;
    public Grasp_HandTracking attemptCounter;
    public TextMeshProUGUI textUI;

    [Header("Text Template | 文本模板（支持占位符）")]
    [TextArea(3, 5)]
    public string template =
        "Hello! You are currently in the {mode} block.\n" +
        "Now at COM {currentCOM}/{totalCOMs}.\n" +
        "You have tried {attempts} times for this COM.";

    void Update()
    {
        if (textUI == null || modeSwitch == null || comController == null || attemptCounter == null)
            return;

        string modeName = modeSwitch.currentMode switch
        {
            ModeSwitch.ExperimentMode.Visual => "Visual",
            ModeSwitch.ExperimentMode.Haptic => "Haptic",
            ModeSwitch.ExperimentMode.VisualHaptic => "Visual + Haptic",
            _ => "Unknown"
        };

        // ✅ 使用累计计数器（排除编号0，显示 /15）
        int currentCOM = comController.comProgressCounter;
        int totalCOMs = comController.centerOfMassList.Length - 1; // 16-1 = 15

        int attempts = attemptCounter.AttemptCount;

        string output = template
            .Replace("{mode}", modeName)
            .Replace("{currentCOM}", currentCOM.ToString())
            .Replace("{totalCOMs}", totalCOMs.ToString())
            .Replace("{attempts}", attempts.ToString());

        textUI.text = output;
    }
}
