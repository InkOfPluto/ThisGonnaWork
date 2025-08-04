using UnityEngine;
using Syntacts;

public class SawtoothToLibrary : MonoBehaviour
{
    public SyntactsHub syntacts;
    public int channel = 0;

    void Start()
    {
        double frequency = 440;
        double duration = 1.0;
        int harmonics = 10;

        Signal saw = new Signal();

        for (int n = 1; n <= harmonics; n++)
        {
            saw += new Sine(n * frequency, duration) * (1.0 / n);
        }

        saw *= new ASR(0.1, 0.8, 0.1); // 可选包络，让声音更自然

        // 播放
        syntacts.session.Play(channel, saw);

        // ✅ 保存到库，Syntacts Studio 左侧 Library 就能看到
        Library.SaveSignal(saw, "Saw_440Hz");
        Debug.Log("已保存信号到 Syntacts Library: Saw_440Hz");
    }
}
