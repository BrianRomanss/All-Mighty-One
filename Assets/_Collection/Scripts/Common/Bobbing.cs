using UnityEngine;

namespace AllMightyOne
{
    /// <summary> Gentle up/down bob and pulse, used for hint markers. </summary>
    public class Bobbing : MonoBehaviour
    {
        public float Height = 0.15f;
        public float Speed = 4f;
        public float Pulse = 0.12f;

        Vector3 _baseScale;
        float _baseY;
        bool _init;

        void OnEnable()
        {
            _baseScale = transform.localScale;
            _init = false;
        }

        void Update()
        {
            if (!_init) { _baseY = transform.position.y; _init = true; }
            float s = Mathf.Sin(Time.time * Speed);
            var p = transform.position;
            p.y = _baseY + (s * 0.5f + 0.5f) * Height;
            transform.position = p;
            transform.localScale = _baseScale * (1f + s * Pulse);
        }

        void OnDisable() { transform.localScale = _baseScale; }
    }
}
