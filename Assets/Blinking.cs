using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blinking : MonoBehaviour
{

    MeshRenderer _meshrend;
    float intensity = 0.01f;
    Color origColor; 

    void Start()
    {
        _meshrend = GetComponent<MeshRenderer>();
        origColor = _meshrend.material.color; 
    }

    void Update()
    {
        float newIntensity = Mathf.Sin(intensity);
        _meshrend.material.SetColor("_EmissionColor", new Vector4(origColor.r, origColor.g, origColor.b, 0f) * newIntensity);
        intensity += 0.1f;
    }
}
