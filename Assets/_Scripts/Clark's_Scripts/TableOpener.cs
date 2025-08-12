// TableOpener.cs
using UnityEngine;

public class TableOpener : MonoBehaviour
{
    [Header("Reference to Threshold object")]
    public GameObject threshold; // 拖拽场景里的 Threshold
    [Header("Tag of the Cylinder")]
    public string cylinderTag = "Cylinder"; // 圆柱体的Tag

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag(cylinderTag))
        {
            Debug.Log("Cylinder touched Table → Reopening Threshold");
            if (threshold != null && !threshold.activeSelf)
            {
                threshold.SetActive(true); // 打开 Threshold
            }
        }
    }
}
