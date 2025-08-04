using UnityEngine;
using UnityEditor;

public class PointVisualizer : MonoBehaviour
{
    [Header("Game Objects | ��Ϸ����")]
    public GameObject[] pointObjects = new GameObject[5]; // ��Ĵָ��СĴָ
    public GameObject cycleCylinder;
    public GameObject mass2;

    [Header("Gizmos Settings | ���ӻ�����")]
    public float pointRadius = 0.001f;

    [Header("Colors | ��ɫ����")]
    public Color[] fingerColors = new Color[5]
    {
        Color.red,                        // ��Ĵָ
        new Color(1f, 0.5f, 0f),          // ʳָ����ɫ��
        Color.yellow,                     // ��ָ
        new Color(1f, 0.4f, 0.7f),        // ����ָ����ɫ��
        new Color(0.6f, 0.4f, 1f)         // СĴָ����ɫ��
    };

    [Header("Extra Line Color | ����������ɫ")]
    public Color massToCylinderColor = Color.blue;

    [Header("X-Axis Projections | ͶӰ��Cylinder�ľֲ�X��")]
    public float ProjectorXThumb;
    public float ProjectorXIndex;
    public float ProjectorXMiddle;
    public float ProjectorXRing;
    public float ProjectorXPinky;

    [Header("Y-Axis Projections | ͶӰ��Cylinder�ľֲ�Y�� (������)")]
    public float ProjectorYThumb;
    public float ProjectorYIndex;
    public float ProjectorYMiddle;
    public float ProjectorYRing;
    public float ProjectorYPinky;

    [Header("Z-Axis Projections | ͶӰ��Cylinder�ľֲ�Z��")]
    public float ProjectorZThumb;
    public float ProjectorZIndex;
    public float ProjectorZMiddle;
    public float ProjectorZRing;
    public float ProjectorZPinky;

    private void OnDrawGizmos()
    {
        if (pointObjects == null || pointObjects.Length != 5 || cycleCylinder == null)
            return;

        Vector3 cylPos = cycleCylinder.transform.position;
        Vector3 cylRight = cycleCylinder.transform.right;
        Vector3 cylUp = cycleCylinder.transform.up;
        Vector3 cylForward = cycleCylinder.transform.forward;

        for (int i = 0; i < pointObjects.Length; i++)
        {
            if (pointObjects[i] == null)
                continue;

            Vector3 pointPos = pointObjects[i].transform.position;
            Vector3 toCylinder = cylPos - pointPos;

            // ԭʼ���ߺ�����
            Gizmos.color = fingerColors[i];
            Gizmos.DrawSphere(pointPos, pointRadius);
            Gizmos.DrawLine(pointPos, cylPos);

            // X��ͶӰ
            float projectionX = Vector3.Dot(toCylinder, cylRight);
            Vector3 projectedXPoint = pointPos + cylRight.normalized * projectionX;
            Color xColor = new Color(fingerColors[i].r, fingerColors[i].g, fingerColors[i].b, 0.3f);
            Gizmos.color = xColor;
            Gizmos.DrawLine(pointPos, projectedXPoint);

            // Y��ͶӰ�������ã���ע�ͣ�
            // float projectionY = Vector3.Dot(toCylinder, cylUp);
            // Vector3 projectedYPoint = pointPos + cylUp.normalized * projectionY;
            // Color yColor = new Color(fingerColors[i].r, fingerColors[i].g, fingerColors[i].b, 0.15f);
            // Gizmos.color = yColor;
            // Gizmos.DrawLine(pointPos, projectedYPoint);

            // Z��ͶӰ
            float projectionZ = Vector3.Dot(toCylinder, cylForward);
            Vector3 projectedZPoint = pointPos + cylForward.normalized * projectionZ;
            Color zColor = new Color(fingerColors[i].r, fingerColors[i].g, fingerColors[i].b, 0.15f);
            Gizmos.color = zColor;
            Gizmos.DrawLine(pointPos, projectedZPoint);

#if UNITY_EDITOR
            //// ���Ʊ�ǩ��������ʾX��Z��
            //Vector3 labelOffset = new Vector3(0, 0.005f, 0);
            //string label = $"X: {projectionX:F4}\nZ: {projectionZ:F4}";
            //Handles.Label(pointPos + labelOffset, label);
#endif

            // �洢ֵ
            switch (i)
            {
                case 0:
                    ProjectorXThumb = projectionX;
                    // ProjectorYThumb = projectionY;
                    ProjectorZThumb = projectionZ;
                    break;
                case 1:
                    ProjectorXIndex = projectionX;
                    // ProjectorYIndex = projectionY;
                    ProjectorZIndex = projectionZ;
                    break;
                case 2:
                    ProjectorXMiddle = projectionX;
                    // ProjectorYMiddle = projectionY;
                    ProjectorZMiddle = projectionZ;
                    break;
                case 3:
                    ProjectorXRing = projectionX;
                    // ProjectorYRing = projectionY;
                    ProjectorZRing = projectionZ;
                    break;
                case 4:
                    ProjectorXPinky = projectionX;
                    // ProjectorYPinky = projectionY;
                    ProjectorZPinky = projectionZ;
                    break;
            }
        }

        // ���ӻ� Mass2 �� Cylinder ������
        if (mass2 != null)
        {
            Gizmos.color = massToCylinderColor;
            Gizmos.DrawLine(mass2.transform.position, cylPos);
        }
    }
}