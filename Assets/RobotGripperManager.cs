using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotGripperManager : MonoBehaviour
{

    public GameObject GripperPrefab;

    GameObject gripperInstance;
    public Transform spawnPoint;
    Vector3 spawnPointOffset; 

    private void Start()
    {
        spawnPointOffset = new Vector3(spawnPoint.position.x, spawnPoint.position.y + 0.25f, spawnPoint.position.z);
        gripperInstance = Instantiate(GripperPrefab, spawnPointOffset, Quaternion.identity);
    }

    private void Update()
    {
        if (gripperInstance.gameObject != null)
        {
            if (gripperInstance.transform.position.x > 100f)
            {
                Destroy(gripperInstance);
                gripperInstance = Instantiate(GripperPrefab, spawnPointOffset, Quaternion.identity);
            }
            if (gripperInstance.transform.position.y > 100f)
            {
                Destroy(gripperInstance);
                gripperInstance = Instantiate(GripperPrefab, spawnPointOffset, Quaternion.identity);
            }
            if (gripperInstance.transform.position.z > 100f)
            {
                Destroy(gripperInstance);
                gripperInstance = Instantiate(GripperPrefab, spawnPointOffset, Quaternion.identity);
            }
        }
    }


}
