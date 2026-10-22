using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AllMightyOne.Hub
{
    /// <summary> An on/off switch: the knob slides and the track changes colour. </summary>
    public class HubSwitch : MonoBehaviour, IPointerClickHandler
    {
        public Image Track;
        public RectTransform Knob;
        public Color OnColor = new(0.3f, 0.82f, 0.36f), OffColor = new(0.72f, 0.72f, 0.78f);
        public float Travel = 46f;
        public UnityEvent<bool> OnChanged = new();

        bool _on;
        float _k;

        public bool IsOn => _on;

        public void Set(bool on, bool instant = false)
        {
            _on = on;
            if (instant) { _k = on ? 1 : 0; Apply(); }
        }

        public void OnPointerClick(PointerEventData e) => Toggle();

        public void Toggle()
        {
            _on = !_on;
            OnChanged.Invoke(_on);
        }

        void Update()
        {
            float target = _on ? 1 : 0;
            if (Mathf.Approximately(_k, target)) return;
            _k = Mathf.MoveTowards(_k, target, Time.unscaledDeltaTime * 7f);
            Apply();
        }

        void Apply()
        {
            if (Track) Track.color = Color.Lerp(OffColor, OnColor, _k);
            if (Knob) Knob.anchoredPosition = new Vector2(Mathf.Lerp(-Travel, Travel, _k), Knob.anchoredPosition.y);
        }
    }
}
