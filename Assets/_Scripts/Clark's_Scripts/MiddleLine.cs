using UnityEngine;

[DisallowMultipleComponent]
public class MiddleLineToggle : MonoBehaviour
{
    [Header("拖拽引用")]
    [Tooltip("把具有 Follow_Handtracking 的组件拖到这里")]
    [SerializeField] private Behaviour followHandtracking; // 直接把 Follow_Handtracking 组件拖拽到此
    [Tooltip("要显示/隐藏的 MiddleLine 对象")]
    [SerializeField] private GameObject middleLine;        // 拖拽 MiddleLine 对象

    private bool lastVisible;

    private void Awake()
    {
        // 如果没有手动拖 MiddleLine，尝试用名称查找（可选）
        if (middleLine == null)
        {
            var t = transform.Find("MiddleLine");
            if (t != null) middleLine = t.gameObject;
        }
        ForceRefresh();
    }

    private void Update()
    {
        if (followHandtracking == null || middleLine == null) return;
        bool shouldShow = followHandtracking.isActiveAndEnabled; // 同时考虑组件启用与物体激活
        if (shouldShow != lastVisible)
        {
            SetVisible(shouldShow);
            lastVisible = shouldShow;
        }
    }

    private void OnValidate()
    {
        ForceRefresh();
    }

    private void ForceRefresh()
    {
        if (followHandtracking == null || middleLine == null) return;
        lastVisible = followHandtracking.isActiveAndEnabled;
        SetVisible(lastVisible);
    }

    private void SetVisible(bool visible)
    {

        // 如果你只想“隐形”而不禁用GameObject，可改为以下做法：
        foreach (var r in middleLine.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
    }
}
