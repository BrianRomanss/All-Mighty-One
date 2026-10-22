using UnityEngine;

namespace AllMightyOne
{
    /// <summary> Gentle bob (anchored position) and breathing scale for UI elements such as the logo. </summary>
    public class UiFloat : MonoBehaviour
    {
        public float Height = 12f;
        public float Speed = 1.6f;
        public float Breathe = 0.025f;

        RectTransform _rt;
        Vector2 _pos;
        Vector3 _scale;
        float _phase;

        void Awake() { _rt = (RectTransform)transform; _pos = _rt.anchoredPosition; _scale = _rt.localScale; _phase = Random.value * 6f; }

        void Update()
        {
            float s = Mathf.Sin(Time.unscaledTime * Speed + _phase);
            _rt.anchoredPosition = _pos + Vector2.up * s * Height;
            _rt.localScale = _scale * (1f + Mathf.Sin(Time.unscaledTime * Speed * 0.5f + _phase) * Breathe);
        }
    }
}
