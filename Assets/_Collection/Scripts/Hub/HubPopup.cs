using System.Collections;
using UnityEngine;

namespace AllMightyOne.Hub
{
    /// <summary> A modal panel: dimmed backdrop fades in, the window pops in; closing reverses it. </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HubPopup : MonoBehaviour
    {
        public RectTransform Window;
        public float Duration = 0.28f;

        CanvasGroup _group;
        Coroutine _anim;

        public bool IsOpen { get; private set; }

        void Awake() => _group = GetComponent<CanvasGroup>();

        public void Open()
        {
            if (!_group) Awake();
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(true));
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (!gameObject.activeInHierarchy) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(false));
        }

        IEnumerator Animate(bool open)
        {
            _group.blocksRaycasts = open;
            for (float t = 0; t < Duration; t += Time.unscaledDeltaTime)
            {
                Apply(open ? t / Duration : 1 - t / Duration, open);
                yield return null;
            }
            Apply(open ? 1 : 0, open);
            if (!open) gameObject.SetActive(false);
            _anim = null;
        }

        void Apply(float k, bool opening)
        {
            _group.alpha = Mathf.Clamp01(k * 1.6f);
            if (!Window) return;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = opening ? 1 + c3 * Mathf.Pow(k - 1, 3) + c1 * Mathf.Pow(k - 1, 2) : k; // overshoot when opening
            Window.localScale = Vector3.one * Mathf.LerpUnclamped(0.75f, 1f, e);
        }
    }
}
