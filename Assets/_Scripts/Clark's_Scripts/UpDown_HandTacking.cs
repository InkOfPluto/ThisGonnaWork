using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ���հ� WristController������ Deadzone + Snap ���� hand ������
/// </summary>
public class UpDown_HandTracking : MonoBehaviour
{
    public GameObject wrist;   // �������
    public GameObject hand;    // �ֶ���

    public float handMoveSpeed = 0.5f;        // hand ƽ���ƶ��ٶ�
    public float handDeadZone = 0.002f;       // ��С�ƶ���ֵ���������̣��Ƽ�0.001~0.005��
    public float handSnapThreshold = 0.0005f; // ������ֵ����С��

    private float previousY;
    private float movementThreshold = 0.01f;  // ״̬�ж���ֵ
    private float accumulatedDeltaY = 0f;

    private float minY = 0.8f;
    private float maxY = 1.2f;

    void Start()
    {
        if (wrist != null)
        {
            previousY = wrist.transform.position.y;
        }
    }

    void Update()
    {
        if (wrist == null || hand == null) return;

        float currentY = wrist.transform.position.y;
        float deltaY = currentY - previousY;

        accumulatedDeltaY += deltaY;

        // �ж��ƶ�״̬
        BigHandState moveState = MoveStateForDelta(accumulatedDeltaY);

        if (Mathf.Abs(accumulatedDeltaY) >= movementThreshold)
        {
            accumulatedDeltaY = 0f;
        }

        // ��״̬��������
        GripperDemoController controller = hand.GetComponent<GripperDemoController>();
        if (controller != null)
        {
            controller.moveState = moveState;
        }

        // ���������߼�
        Vector3 handPos = hand.transform.position;
        float targetY = Mathf.Clamp(currentY, minY, maxY);
        float diffY = targetY - handPos.y;

        if (Mathf.Abs(diffY) > handDeadZone)
        {
            // ��ֵ�ƶ�
            handPos.y = Mathf.MoveTowards(handPos.y, targetY, handMoveSpeed * Time.deltaTime);
            hand.transform.position = handPos;
        }
        else if (Mathf.Abs(diffY) > handSnapThreshold)
        {
            // �ѷǳ��ӽ�Ŀ�꣬����û��ȫ���� ���� ֱ�����ϱ���ж�
            handPos.y = targetY;
            hand.transform.position = handPos;
        }
        // ���򲻶������ⶶ��

        previousY = currentY;
    }

    BigHandState MoveStateForDelta(float deltaY)
    {
        if (deltaY > movementThreshold)
        {
            return BigHandState.MovingUp;
        }
        else if (deltaY < -movementThreshold)
        {
            return BigHandState.MovingDown;
        }
        else
        {
            return BigHandState.Fixed;
        }
    }
}
