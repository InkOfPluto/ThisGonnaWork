using UnityEngine;
using TMPro;

public class CountingTextUpdater : MonoBehaviour
{
    [Header("����˵�� | Instructions")]
    [ReadOnly]
    [TextArea(3, 10)]
    public string instructions =
        "���ܣ���ģ���е�ռλ���滻����ʾ�� TextMeshProUGUI ��\n" +
        "ռλ����{mode}��{currentCOM}��{totalCOMs}��{attempts}\n" +
        "Inspector���� ModeSwitch��CenterOfMassController��Grasp_HandTracking��TextMeshProUGUI\n" +
        "����ʱ��ÿ֡���� modeSwitch.currentMode��comController.selectedCOMIndex �� attemptCounter.AttemptCount �����ı�\n";

    [Header("References | �������")]
    public ModeSwitch modeSwitch;                    // ģʽ������
    public CenterOfMassController comController;     // ���Ĺ�����
    public Grasp_HandTracking attemptCounter;        // ץ�մ���ͳ��
    public TextMeshProUGUI textUI;                   // TextMeshPro ���

    [Header("Text Template | �ı�ģ�壨֧��ռλ����")]
    [TextArea(3, 5)]
    public string template =
        "Hello! You are currently in the {mode} block.\n" +
        "Now at COM {currentCOM}/{totalCOMs}.\n" +
        "You have tried {attempts} times for this COM."; 

    void Update()
    {
        if (textUI == null || modeSwitch == null || comController == null || attemptCounter == null)
            return;

        // ��ȡģʽ����
        string modeName = modeSwitch.currentMode switch
        {
            ModeSwitch.ExperimentMode.Visual => "Visual",
            ModeSwitch.ExperimentMode.Haptic => "Haptic",
            ModeSwitch.ExperimentMode.VisualHaptic => "Visual + Haptic",
            _ => "Unknown"
        };

        // ��ȡ���Ľ��ȣ�ֱ�Ӵ� 0 ��ʼ������
        int currentCOM = comController.selectedCOMIndex;
        int totalCOMs = comController.centerOfMassList.Length;

        // ��ȡ���Դ���
        int attempts = attemptCounter.AttemptCount;

        // ռλ���滻
        string output = template
            .Replace("{mode}", modeName)
            .Replace("{currentCOM}", currentCOM.ToString())
            .Replace("{totalCOMs}", totalCOMs.ToString())
            .Replace("{attempts}", attempts.ToString());

        textUI.text = output;
    }
}
