using UnityEngine;

[DisallowMultipleComponent]
public class CylinderVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ModeSwitch modeSwitch;   // 拖拽你的 ModeSwitch
    [SerializeField] private MeshRenderer[] renderers; // 可留空，自动抓取

    [Header("Debug (ReadOnly)")]
    [SerializeField, ReadOnly] private bool isInsideThreshold = false;
    [SerializeField, ReadOnly] private ModeSwitch.ExperimentMode cachedMode;

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<MeshRenderer>(true);

        if (modeSwitch == null)
            modeSwitch = FindObjectOfType<ModeSwitch>();

        cachedMode = modeSwitch != null ? modeSwitch.currentMode : ModeSwitch.ExperimentMode.Visual;
    }

    private void Start()
    {
        ApplyVisibility();
    }

    private void Update()
    {
        if (modeSwitch != null && modeSwitch.currentMode != cachedMode)
        {
            cachedMode = modeSwitch.currentMode;
            ApplyVisibility();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Threshold"))
        {
            isInsideThreshold = true;
            ApplyVisibility();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Threshold"))
        {
            isInsideThreshold = false;
            ApplyVisibility();
        }
    }

    private void ApplyVisibility()
    {
        bool visible;
        switch (cachedMode)
        {
            case ModeSwitch.ExperimentMode.Visual:
            case ModeSwitch.ExperimentMode.VisualHaptic:
                visible = true; // 始终显示
                break;
            case ModeSwitch.ExperimentMode.Haptic:
                visible = isInsideThreshold; // 阈值内才显示
                break;
            default:
                visible = true;
                break;
        }

        foreach (var r in renderers)
        {
            if (r != null) r.enabled = visible;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<MeshRenderer>(true);

        if (modeSwitch == null)
            modeSwitch = FindObjectOfType<ModeSwitch>();
    }
#endif
}
