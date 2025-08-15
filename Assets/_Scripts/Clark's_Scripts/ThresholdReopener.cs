// ThresholdReopener.cs
using UnityEngine;

public class ThresholdReopener : MonoBehaviour
{
    [Header("References")]
    [Tooltip("���ر�/�򿪵� Threshold������ ThresholdCloser ���Ǹ�����")]
    public GameObject threshold;

    [Tooltip("Cylinder �� Transform")]
    public Transform cylinder;

    [Header("Settings")]
    [Tooltip("�� Cylinder ���������� Y < reopenHeight ʱ�����´� Threshold")]
    public float reopenHeight = 0.775f;

    // �ڲ�״̬������ʶ�𡰸ձ��رա����¼�����Ϊ ThresholdCloser ������
    private bool sawCloseEvent = false;
    private bool lastThresholdActive = true;

    private void Awake()
    {
        if (threshold != null)
            lastThresholdActive = threshold.activeSelf;
    }

    private void Update()
    {
        if (threshold == null || cylinder == null) return;

        // ��⡰�ر��¼�������֡�Ǽ����֡��δ���� �� ��Ϊ�� ThresholdCloser �ոմ�����
        if (lastThresholdActive && !threshold.activeSelf)
        {
            sawCloseEvent = true;
        }
        lastThresholdActive = threshold.activeSelf;

        // ����Ѽ�⵽�ر��¼������ڸ߶���������ʱ���´�
        if (sawCloseEvent)
        {
            if (cylinder.position.y < reopenHeight)
            {
                threshold.SetActive(true);   // ���´� Threshold
                sawCloseEvent = false;       // ��գ��ȴ���һ�� ThresholdCloser �ٴδ���
                lastThresholdActive = true;  // ͬ��״̬����������
                Debug.Log($"[ThresholdReopener] Reopened because Cylinder.y={cylinder.position.y:F3} < {reopenHeight}");
            }
        }
    }
}
