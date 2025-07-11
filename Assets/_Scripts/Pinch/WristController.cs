using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WristController : MonoBehaviour
{
    public GameObject wrist;           // �������
    public GameObject hand;            // �ֶ���

    private float previousY;           // ��һ֡ wrist Y ֵ
    private float movementThreshold = 0.01f;  // ������ֵ

    private float minY = 0.8f;         // hand ����͸߶�
    private float maxY = 1.2f;         // hand ����߸߶�

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

        BigHandState moveState = MoveStateForDelta(deltaY, currentY);

        GripperDemoController controller = hand.GetComponent<GripperDemoController>();
        if (controller != null)
        {
            controller.moveState = moveState;
        }

        // ���� hand �� Y ֵ�� minY �� maxY ֮��
        Vector3 handPos = hand.transform.position;
        handPos.y = Mathf.Clamp(handPos.y, minY, maxY);
        hand.transform.position = handPos;

        previousY = currentY;
    }

    BigHandState MoveStateForDelta(float deltaY, float currentY)
    {
        if (currentY < 0.8f)
        {
            return BigHandState.MovingDown;
        }
        else if (currentY > 1.4f)
        {
            return BigHandState.MovingUp;
        }
        else if (deltaY > movementThreshold)
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
