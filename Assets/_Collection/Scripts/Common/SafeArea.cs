using UnityEngine;

namespace AllMightyOne
{
    /// <summary> Fits a RectTransform to the device safe area (notches, rounded corners). </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect _last;

        void Update()
        {
            var area = Screen.safeArea;
            if (area == _last || Screen.width <= 0 || Screen.height <= 0) return;
            _last = area;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rt.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
