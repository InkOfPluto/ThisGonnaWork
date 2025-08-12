// ThresholdCloser.cs
using UnityEngine;

public class ThresholdCloser : MonoBehaviour
{
    [Header("Tag of the Cylinder")]
    public string cylinderTag = "Cylinder"; // Ô²ÖùÌåµÄTag

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(cylinderTag))
        {
            Debug.Log("Cylinder exited Threshold ¡ú Closing Threshold");
            gameObject.SetActive(false); // ¹Ø±Õ Threshold
        }
    }
}
