using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonActions : MonoBehaviour
{

    public int ButtonID = 0;

    private SkinnedMeshRenderer[] handMeshes;
    private GameObject[] interactables;
    private Vector3[] initPos;
    private Quaternion[] initRot;

    void Start()
    {
        GameObject[] driverHandMesh = GameObject.FindGameObjectsWithTag("DriverHandMesh");
        handMeshes = new SkinnedMeshRenderer[driverHandMesh.Length];
        handMeshes[0] = driverHandMesh[0].GetComponent<SkinnedMeshRenderer>();
        handMeshes[1] = driverHandMesh[1].GetComponent<SkinnedMeshRenderer>();

        GameObject[] tempInteractables = GameObject.FindGameObjectsWithTag("Interactable");
        interactables = new GameObject[tempInteractables.Length];
        interactables = tempInteractables;

        Invoke("Init", 3f);
    }

    void Init()
    {
        initPos = new Vector3[interactables.Length + 1];
        initRot = new Quaternion[interactables.Length + 1];

        int i = 0;
        foreach (GameObject obj in interactables)
        {
            initPos[i] = obj.transform.position;
            initRot[i] = obj.transform.rotation;
            i++;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ButtonID == 0) // Reset objects to original place 
        {
            int i = 0;
            foreach (GameObject obj in interactables)
            {
                obj.transform.position = initPos[i];
                obj.transform.rotation = initRot[i];
                i++;
                //obj.SetActive(false);
            }
            //foreach (GameObject obj in interactables)
            //{
            //    obj.SetActive(true);
            //}
        }

        if (ButtonID == 1) // Show tracked hand 
        {
            foreach (SkinnedMeshRenderer hmesh in handMeshes)
            {
                if (hmesh.enabled)
                { hmesh.enabled = false; }
                else if (!hmesh.enabled)
                { hmesh.enabled = true;}
            }
        }
    }
}
