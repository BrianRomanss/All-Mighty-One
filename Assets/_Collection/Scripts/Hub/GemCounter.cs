using Solo.MOST_IN_ONE;
using TMPro;
using UnityEngine;

namespace AllMightyOne.Hub
{
    /// <summary>
    /// The shared gem balance in a game's top corner, for the games whose kit UI has no wallet. A change counts up
    /// (or down) to the new balance and gives the pill a small bump; the bump is skipped with reduced motion.
    /// </summary>
    public class GemCounter : MonoBehaviour
    {
        public MOST_Database Database;
        public string CurrencyName = "Currency_Gems";
        public TMP_Text Text;
        public float CountTime = 0.5f;
        public float BumpScale = 1.18f;
        public float BumpTime = 0.3f;

        int _target = int.MinValue, _from, _shown;
        float _countT = -1f, _bumpT = -1f;
        Vector3 _base;

        void Awake() => _base = transform.localScale;

        void OnDisable()
        {
            _bumpT = -1f;
            transform.localScale = _base;
        }

        void Update()
        {
            int gems = Database ? Database.Get<IntData>(CurrencyName)?.Value ?? 0 : 0;
            if (gems != _target)
            {
                bool first = _target == int.MinValue;
                _target = gems;
                if (first) Show(gems); // the opening balance just appears
                else
                {
                    _from = _shown;
                    _countT = 0f;
                    if (!CollectionFeedback.ReducedMotion) _bumpT = 0f;
                }
            }

            float dt = Time.unscaledDeltaTime;
            if (_countT >= 0f)
            {
                _countT += dt;
                float k = Mathf.Clamp01(_countT / CountTime);
                Show(Mathf.RoundToInt(Mathf.Lerp(_from, _target, 1f - (1f - k) * (1f - k)))); // ease-out
                if (k >= 1f) _countT = -1f;
            }
            if (_bumpT >= 0f)
            {
                _bumpT += dt;
                float k = Mathf.Clamp01(_bumpT / BumpTime);
                transform.localScale = _base * Mathf.Lerp(1f, BumpScale, Mathf.Sin(k * Mathf.PI));
                if (k >= 1f) _bumpT = -1f;
            }
        }

        void Show(int gems)
        {
            if (gems == _shown && Text && Text.text.Length > 0) return;
            _shown = gems;
            if (Text) Text.text = gems.ToString("N0");
        }
    }
}
