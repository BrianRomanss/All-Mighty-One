using System;
using AllMightyOne;
using UnityEngine;
using UnityEngine.Events;

namespace Solo.MOST_IN_ONE
{
    /// <summary>
    /// MOST_Action that shows an ad, so any MOST event can trigger ads from the inspector.
    /// </summary>
    [Serializable]
    public class MOSTAction_ShowAd : MOST_ActionCore
    {
        public enum AdKind { Interstitial, LevelEndInterstitial, Rewarded }

        public MOSTAction_ShowAd() { ActionName = "Show Ad"; }

        [Line]
        public AdKind Kind = AdKind.Rewarded;
        [Tooltip("Ad placement / analytics name")]
        public string Placement = "rewarded";

        [Tooltip("Rewarded: invoked when the reward must be granted. Interstitial: invoked after the ad closes.")]
        public UnityEvent OnRewardGranted;
        [Tooltip("Rewarded only: invoked when the ad was skipped or failed.")]
        public UnityEvent OnRewardFailed;

        protected override void Play()
        {
            IsPlaying = true;
            switch (Kind)
            {
                case AdKind.Interstitial:
                    AdsBridge.TryShowInterstitial(Placement, () => { IsPlaying = false; OnRewardGranted?.Invoke(); });
                    break;
                case AdKind.LevelEndInterstitial:
                    AdsBridge.OnLevelEnd(Placement);
                    IsPlaying = false;
                    break;
                default:
                    AdsBridge.ShowRewarded(Placement, ok =>
                    {
                        IsPlaying = false;
                        if (ok) OnRewardGranted?.Invoke(); else OnRewardFailed?.Invoke();
                    });
                    break;
            }
        }

        protected override void Stop() { IsPlaying = false; }
    }
}
