// ThresholdCloser.cs
using UnityEngine;

public class ThresholdCloser : MonoBehaviour
{
    [Header("Tag of the Cylinder")]
    public string cylinderTag = "Cylinder"; // Բ�����Tag

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(cylinderTag))
        {
            Debug.Log("Cylinder exited Threshold �� Closing Threshold");
            gameObject.SetActive(false); // �ر� Threshold
        }
    }
}
