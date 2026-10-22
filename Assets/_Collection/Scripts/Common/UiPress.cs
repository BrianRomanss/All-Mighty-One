using UnityEngine;
using UnityEngine.EventSystems;

namespace AllMightyOne
{
    /// <summary> Squashes a button while it is held and springs back on release (works with any Selectable). </summary>
    public class UiPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float PressedScale = 0.92f;
        public float Speed = 18f;

        Vector3 _base;
        float _target = 1f, _current = 1f;

        void Awake() => _base = transform.localScale;
        void OnDisable() { _target = _current = 1f; if (_base != Vector3.zero) transform.localScale = _base; }

        public void OnPointerDown(PointerEventData e) => _target = PressedScale;
        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        void Update()
        {
            if (Mathf.Approximately(_current, _target)) return;
            _current = Mathf.MoveTowards(_current, _target, Speed * Time.unscaledDeltaTime * Mathf.Max(0.05f, Mathf.Abs(_target - _current)) * 4f);
            transform.localScale = _base * _current;
        }
    }
}
