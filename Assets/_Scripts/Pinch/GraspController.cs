using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraspController : MonoBehaviour
{
    public PincherController pinchController;  // ���� Pincher ������
    public Transform[] fingerTips;            // ��ָָ���������
    public Transform thumbtip;                // Ĵָָ�����
    public float OpenDistance;
    public float CloseDistance;

    void Start()
    {
        // �Զ����ҳ����е� PincherController �ű�
        pinchController = FindAnyObjectByType<PincherController>();
    }

    void Update()
    {
        float fingertipDist = 0f;

        // ����������ָָ�⵽Ĵָ��ƽ������
        foreach (Transform t in fingerTips)
        {
            fingertipDist += Vector3.Distance(thumbtip.position, t.position);
        }
        float avfingertipDist = fingertipDist / fingerTips.Length;

        // Debug.Log("��ǰָ��ƽ������: " + avfingertipDist.ToString("F3"));

        // ���ݾ�������ץ��״̬
        if (avfingertipDist >= OpenDistance)
        {
            //Debug.Log("���ſ�������");
            // �����ȫ�ſ�����ָ��
            pinchController.gripState = GripState.Opening;
            //Debug.Log("���Ӵ�");
        }
        else if (avfingertipDist <= CloseDistance)
        {
            //Debug.Log("����ָ��£��");
            // �����ȫ�պϣ���ָ������£
            pinchController.gripState = GripState.Closing;
            //Debug.Log("���ӹر�");
        }
        else
        {
         
            // ���������������ָλ�ò���
            pinchController.gripState = GripState.Fixed;
        }
    }
}
