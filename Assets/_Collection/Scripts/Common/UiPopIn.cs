using UnityEngine;

namespace AllMightyOne
{
    /// <summary> Pops the object in (overshooting scale and fade) every time it is enabled. </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiPopIn : MonoBehaviour
    {
        public float Delay;
        public float Duration = 0.35f;
        public float FromScale = 0.7f;

        CanvasGroup _group;
        Vector3 _base;
        float _t;
        bool _hasBase;

        void Awake() { _group = GetComponent<CanvasGroup>(); _base = transform.localScale; _hasBase = true; }

        void OnEnable()
        {
            if (!_hasBase) Awake();
            _t = -Delay;
            Apply(0);
        }

        /// <summary> Plays the pop again (e.g. a card that was just unlocked). </summary>
        public void Replay() { _t = 0; Apply(0); }

        void Update()
        {
            if (_t >= Duration) return;
            _t += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(_t / Duration));
        }

        void Apply(float k)
        {
            // ease-out-back
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = k <= 0 ? 0 : 1 + c3 * Mathf.Pow(k - 1, 3) + c1 * Mathf.Pow(k - 1, 2);
            transform.localScale = _base * Mathf.LerpUnclamped(FromScale, 1f, e);
            _group.alpha = Mathf.Clamp01(k * 2f);
        }
    }
}
