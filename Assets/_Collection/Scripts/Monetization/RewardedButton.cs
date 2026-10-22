using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AllMightyOne
{
    /// <summary>
    /// Put on any UI Button: tapping it plays a rewarded ad and fires OnRewardGranted on success.
    /// Use for revive, x3 reward, free upgrade, hint, skip level, bigger start, free gems.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RewardedButton : MonoBehaviour
    {
        [Tooltip("Analytics / ad placement name, e.g. revive, triple_reward, free_upgrade")]
        public string Placement = "rewarded";

        [Tooltip("Disable the button after one successful reward (e.g. one revive per run).")]
        public bool OncePerScene = false;

        public UnityEvent OnRewardGranted;
        public UnityEvent OnRewardFailed;

        Button _button;
        bool _used;

        void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Show);
        }

        void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(Show);
        }

        public void Show()
        {
            if (_used) return;
            _button.interactable = false;
            AdsBridge.ShowRewarded(Placement, success =>
            {
                if (this == null) return;
                if (success)
                {
                    if (OncePerScene) { _used = true; gameObject.SetActive(false); }
                    else _button.interactable = true;
                    OnRewardGranted?.Invoke();
                }
                else
                {
                    _button.interactable = true;
                    OnRewardFailed?.Invoke();
                }
            });
        }
    }
}
