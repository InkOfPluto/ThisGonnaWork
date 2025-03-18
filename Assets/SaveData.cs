using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
[System.Serializable]
public class DataClass2
{
    public string trialInfo;
    public List<float> xPosition = new List<float>();
    public List<float> yPosition = new List<float>();
    public List<float> zPosition = new List<float>();
    public List<float> xRotation = new List<float>();
    public List<float> yRotation = new List<float>();
    public List<float> zRotation = new List<float>();
    public List<float> time = new List<float>();

    public void ClearData()
    {
        xPosition.Clear();
        yPosition.Clear();
        zPosition.Clear();
        xRotation.Clear();
        yRotation.Clear();
        zRotation.Clear();
        time.Clear();
    }

}


public class SaveData : MonoBehaviour
{
    public DataClass2 dataClass;

    public bool threaded = true;

    public string path;
    private Coroutine saveDataCoroutine;
    private int trialNumber = 0;
    private float startTime;
    private float elapsedTime = 0f;

    void Start()
    {
        startTime = Time.time;
    }

    void Update()
    {

        elapsedTime = Time.time - startTime;

        // Get the position and rotation of the object
        dataClass.xPosition.Add(transform.position.x);
        dataClass.yPosition.Add(transform.position.y);
        dataClass.zPosition.Add(transform.position.z);
        dataClass.xRotation.Add(transform.rotation.x);
        dataClass.yRotation.Add(transform.rotation.y);
        dataClass.zRotation.Add(transform.rotation.z);
        dataClass.time.Add(elapsedTime);

        if (Input.GetKeyDown(KeyCode.S))
        {
            if (saveDataCoroutine != null)
            {
                StopCoroutine(saveDataCoroutine);
            }
            saveDataCoroutine = StartCoroutine(SaveFile());
            trialNumber++;
            startTime = Time.time;
        }
    }


    private IEnumerator SaveFile()
    {
        // Convert to json and send to another site on the server
        dataClass.trialInfo = "Trial " + trialNumber.ToString();
        string jsonString = JsonConvert.SerializeObject(dataClass, Formatting.Indented);

        // Save the data to a file
        if (!threaded)
        {
            File.WriteAllText(path + "/Data/" + dataClass.trialInfo + ".json", jsonString);
        }
        else
        {
            // create new thread to save the data to a file (only operation that can be done in background)
            new System.Threading.Thread(() =>
            {
                File.WriteAllText(path + "/Data/" + dataClass.trialInfo + ".json", jsonString);
            }).Start();
        }

        // Empty text fields for next trials (potential for issues with next trial)
        dataClass.ClearData();

        yield return null;
    }

}