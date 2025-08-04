using Syntacts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyntactsTrying : MonoBehaviour
{
    public string signalName = "my_signal";
    public SyntactsHub hub;


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Signal sig;
            if (Syntacts.Library.LoadSignal(out sig, signalName))
            {
                hub.session.Play(0, sig);  // 现在 hub 已赋值，可以正常播放
                Debug.Log("✅ Played Signal: " + signalName);
            }
            else
            {
                Debug.LogError("❌ Failed to load signal: " + signalName);
            }
        }

    }
}
