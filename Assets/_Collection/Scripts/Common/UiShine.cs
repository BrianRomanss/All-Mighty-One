using UnityEngine;

namespace AllMightyOne
{
    /// <summary>
    /// A bright diagonal band that sweeps across a card (inside a RectMask2D). Plays on demand, and repeats every
    /// <see cref="Every"/> seconds when that is above zero.
    /// </summary>
    public class UiShine : MonoBehaviour
    {
        public RectTransform Band;
        public float Duration = 0.6f;
        [Tooltip("Seconds between automatic sweeps; 0 = only when Play is called.")]
        public float Every;

        float _t = -1f, _wait;

        void OnEnable()
        {
            _wait = Every > 0 ? Random.Range(0.5f, Every) : 0f;
            Place(-1f);
        }

        public void Play() => _t = 0f;

        void Update()
        {
            if (_t < 0f)
            {
                if (Every <= 0f) return;
                _wait -= Time.unscaledDeltaTime;
                if (_wait > 0f) return;
                _wait = Every;
                _t = 0f;
            }
            _t += Time.unscaledDeltaTime;
            float k = _t / Duration;
            Place(k);
            if (k >= 1f) _t = -1f;
        }

        // k: 0 = just off the left edge, 1 = just off the right edge
        void Place(float k)
        {
            if (!Band) return;
            bool on = k >= 0f && k < 1f;
            Band.gameObject.SetActive(on);
            if (!on) return;
            float w = ((RectTransform)transform).rect.width + Band.rect.width * 2f;
            Band.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.5f, w * 0.5f, k), 0f);
        }
    }
}
