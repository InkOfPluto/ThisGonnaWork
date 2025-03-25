using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropSound : MonoBehaviour
{
    AudioSource dropSound;

    private void Start()
    {
        dropSound = GameObject.FindWithTag("dropsound").GetComponent<AudioSource>();
    }

    private void OnCollisionEnter(Collision other)
    {
        if(other.gameObject.CompareTag("tabletop"))
        {
            dropSound.Play(); 
        }
    }
}
