using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ButtonTriggerBroadcast : MonoBehaviour
{
    public UnityEvent<float> buttonEvent = new UnityEvent<float>();
    public AudioSource buttonPress; 

    public MeshRenderer buttonMesh;
    Color origColor;
    Color holdColor = new Color(0.2f, 0.2f, 0, 0.5f);
    Color pressedColor = new Color(0f, 1f, 0, 1f);

    private void Start()
    {
        origColor = buttonMesh.material.color; 
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.tag == "button")
        {
            buttonEvent.Invoke(0.0f);
            buttonPress.Play(); 
        }
    }

    private void OnTriggerStay(Collider other)
    {
        buttonMesh.material.color = holdColor;
    }
    private void OnTriggerExit(Collider other)
    {
        buttonMesh.material.color = pressedColor;        
        Invoke("ResetButtonState", 1.5f); 
    }

    void ResetButtonState()
    {
        buttonMesh.material.color = origColor;
    }
}
