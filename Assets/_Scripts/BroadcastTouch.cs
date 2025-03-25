using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BroadcastTouch : MonoBehaviour
{
    public static event System.Action<GameObject> OnObjectTouched;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("idx"))
        {
            // Broadcast that this object was touched by the index finger
            OnObjectTouched?.Invoke(gameObject);
        }
    }

}
