using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotGripperManager : MonoBehaviour
{

    public GameObject GripperPrefab;

    GameObject gripperInstance; 

    private void Start()
    {
        gripperInstance = Instantiate(GripperPrefab);
    }

    private void Update()
    {
        if (gripperInstance.gameObject != null)
        {
            if (gripperInstance.transform.position.x > 100f)
            {
                Destroy(gripperInstance);
                gripperInstance = Instantiate(GripperPrefab);
            }
        }
    }


}
